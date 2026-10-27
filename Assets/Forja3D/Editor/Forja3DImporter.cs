// Forja3D — importador de modelos para Unity 6.
// Prepara los FBX de Assets/Forja3D/Modelos/<nombre>/ (los sube el conector o este menú):
// rig Humanoid, nombres y bucles de clips, Animator Controller con blend tree y un prefab.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Networking;

namespace Forja3D.EditorTools
{
    [Serializable] public class Archivo { public string nombre; public string url; public string tipo; public string[] clips; }
    [Serializable] public class Manifiesto { public int forja3d; public bool personaje; public Archivo[] archivos; }

    public class Forja3DImporter : EditorWindow
    {
        public const string Raiz = "Assets/Forja3D/Modelos";
        string nombre = "MiPersonaje";
        string json = "";
        Vector2 scroll;

        public static string Limpio(string s) =>
            string.Concat((s ?? "modelo").Trim().Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_'));

        public static Manifiesto LeerManifiesto(string carpeta)
        {
            var ruta = Path.Combine(carpeta, "forja3d.json");
            return File.Exists(ruta) ? JsonUtility.FromJson<Manifiesto>(File.ReadAllText(ruta)) : null;
        }

        // ---------- Ventana manual ----------
        [MenuItem("Forja3D/Importar modelo")]
        static void Abrir() => GetWindow<Forja3DImporter>("Forja3D").minSize = new Vector2(420, 320);

        void OnGUI()
        {
            EditorGUILayout.LabelField("Importar desde el conector Forja3D", EditorStyles.boldLabel);
            nombre = EditorGUILayout.TextField("Nombre", nombre);
            EditorGUILayout.LabelField("Manifiesto (la línea JSON que te dio Claude):");
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(180));
            json = EditorGUILayout.TextArea(json, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(nombre)))
                if (GUILayout.Button("Descargar e importar", GUILayout.Height(32))) Importar(nombre.Trim(), json.Trim());
        }

        public static void Importar(string nombre, string json)
        {
            Manifiesto m;
            try { m = JsonUtility.FromJson<Manifiesto>(json); }
            catch (Exception e) { EditorUtility.DisplayDialog("Forja3D", "Manifiesto inválido: " + e.Message, "OK"); return; }
            if (m?.archivos == null || m.archivos.Length == 0) { EditorUtility.DisplayDialog("Forja3D", "El manifiesto no tiene archivos.", "OK"); return; }

            string carpeta = $"{Raiz}/{Limpio(nombre)}";
            Directory.CreateDirectory(carpeta);
            File.WriteAllText($"{carpeta}/forja3d.json", json); // antes de importar: el postprocesador lo lee
            try
            {
                for (int i = 0; i < m.archivos.Length; i++)
                {
                    var a = m.archivos[i];
                    if (string.IsNullOrEmpty(a.url)) continue;
                    EditorUtility.DisplayProgressBar("Forja3D", $"Descargando {a.nombre}…", (float)i / m.archivos.Length);
                    using var req = UnityWebRequest.Get(a.url);
                    var op = req.SendWebRequest();
                    while (!op.isDone) System.Threading.Thread.Sleep(50);
                    if (req.result != UnityWebRequest.Result.Success)
                        throw new Exception($"{a.nombre}: {req.error} (¿caducó el enlace? Pide a Claude que use guardar_asset)");
                    File.WriteAllBytes($"{carpeta}/{Limpio(a.nombre)}.fbx", req.downloadHandler.data);
                }
            }
            catch (Exception e) { EditorUtility.ClearProgressBar(); EditorUtility.DisplayDialog("Forja3D", e.Message, "OK"); return; }
            finally { EditorUtility.ClearProgressBar(); }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var prefab = PrepararCarpeta(carpeta, true);
            if (prefab) { Selection.activeObject = prefab; EditorGUIUtility.PingObject(prefab); }
            Debug.Log($"[Forja3D] {nombre} importado en {carpeta}");
        }

        // ---------- Preparación automática (la usa también la compilación en la nube) ----------
        public static void PrepararTodo()
        {
            if (!Directory.Exists(Raiz)) return;
            foreach (var dir in Directory.GetDirectories(Raiz))
            {
                var carpeta = dir.Replace('\\', '/');
                try { PrepararCarpeta(carpeta, false); }
                catch (Exception e) { Debug.LogError($"[Forja3D] No se pudo preparar {carpeta}: {e}"); }
            }
        }

        public static GameObject RutaPrefab(string modelo)
        {
            if (string.IsNullOrEmpty(modelo) || !Directory.Exists(Raiz)) return null;
            var dir = Directory.GetDirectories(Raiz).FirstOrDefault(d =>
                string.Equals(Path.GetFileName(d), Limpio(modelo), StringComparison.OrdinalIgnoreCase));
            if (dir == null) return null;
            var p = Directory.GetFiles(dir, "*.prefab").FirstOrDefault();
            return p == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(p.Replace('\\', '/'));
        }

        public static GameObject PrepararCarpeta(string carpeta, bool forzar)
        {
            var m = LeerManifiesto(carpeta);
            if (m?.archivos == null) return null;
            string nombre = Path.GetFileName(carpeta);
            string rutaPrefab = $"{carpeta}/{nombre}.prefab";
            if (!forzar && File.Exists(rutaPrefab)) return AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab);

            var archivos = m.archivos.Where(a => a != null)
                .Select(a => (a, ruta: $"{carpeta}/{Limpio(a.nombre)}.fbx"))
                .Where(x => File.Exists(x.ruta)).ToList();
            var modelo = archivos.FirstOrDefault(x => x.a.tipo == "modelo").ruta ?? archivos.FirstOrDefault().ruta;
            var go = modelo != null ? AssetDatabase.LoadAssetAtPath<GameObject>(modelo) : null;
            if (!go) { Debug.LogWarning($"[Forja3D] {carpeta}: no hay FBX del modelo."); return null; }

            AnimatorController ctrl = m.personaje ? CrearAnimator(carpeta, archivos) : null;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(go);
            var b = Limites(inst);
            if (m.personaje)
            {
                var anim = inst.GetComponent<Animator>();
                if (!anim) anim = inst.AddComponent<Animator>();
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                var cc = inst.GetComponent<CharacterController>();
                if (!cc) cc = inst.AddComponent<CharacterController>();
                float h = Mathf.Max(b.size.y, 0.5f);
                cc.height = h; cc.radius = Mathf.Clamp(Mathf.Max(b.size.x, b.size.z) * 0.3f, 0.15f, h / 2f);
                cc.center = new Vector3(0, h / 2f, 0);
            }
            else if (!inst.GetComponent<Collider>())
            {
                var box = inst.AddComponent<BoxCollider>();
                box.center = inst.transform.InverseTransformPoint(b.center); box.size = b.size;
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(inst, rutaPrefab);
            DestroyImmediate(inst);
            return prefab;
        }

        public static Bounds Limites(GameObject g)
        {
            var rs = g.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.one);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        // Animator: locomoción (idle/caminar/correr por "Speed") + triggers de acciones + muerte.
        static AnimatorController CrearAnimator(string carpeta, List<(Archivo a, string ruta)> archivos)
        {
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var (_, ruta) in archivos)
                foreach (var c in AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<AnimationClip>())
                    if (!c.name.StartsWith("__preview__")) clips[c.name.ToLowerInvariant()] = c;

            AnimationClip Buscar(params string[] claves) =>
                claves.Select(k => clips.TryGetValue(k, out var exacto) ? exacto
                    : clips.Where(kv => kv.Key.Contains(k)).OrderBy(kv => kv.Key.Length).Select(kv => kv.Value).FirstOrDefault())
                    .FirstOrDefault(c => c != null);

            string rutaCtrl = $"{carpeta}/{Path.GetFileName(carpeta)}_Animator.controller";
            AssetDatabase.DeleteAsset(rutaCtrl);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(rutaCtrl);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            var sm = ctrl.layers[0].stateMachine;

            var loco = ctrl.CreateBlendTreeInController("Locomocion", out var arbol);
            arbol.blendParameter = "Speed";
            arbol.useAutomaticThresholds = false;
            var idle = Buscar("idle") ?? Buscar("caminar", "walk");
            if (idle) arbol.AddChild(idle, 0f);
            var caminar = Buscar("caminar", "walk"); if (caminar) arbol.AddChild(caminar, 2f);
            var correr = Buscar("correr_rapido", "correr", "run"); if (correr) arbol.AddChild(correr, 5.5f);
            sm.defaultState = loco;

            var acciones = new (string trigger, string[] claves)[] {
                ("Jump", new[]{"saltar","jump"}), ("Attack", new[]{"ataque","attack"}), ("Hit", new[]{"golpeado","hit"}),
                ("Skill1", new[]{"habilidad_1"}), ("Skill2", new[]{"habilidad_2"}), ("Skill3", new[]{"habilidad_3"}),
                ("Dance", new[]{"bailar"}), ("Alert", new[]{"alerta"}),
            };
            foreach (var (trigger, claves) in acciones)
            {
                var clip = Buscar(claves); if (!clip) continue;
                ctrl.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
                var st = sm.AddState(trigger); st.motion = clip;
                var ida = sm.AddAnyStateTransition(st);
                ida.AddCondition(AnimatorConditionMode.If, 0, trigger);
                ida.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
                ida.duration = 0.1f; ida.canTransitionToSelf = false;
                var vuelta = st.AddTransition(loco); vuelta.hasExitTime = true; vuelta.exitTime = 0.9f; vuelta.duration = 0.15f;
            }
            var muerte = Buscar("muerte", "dead");
            if (muerte)
            {
                var st = sm.AddState("Muerte"); st.motion = muerte;
                var t = sm.AddAnyStateTransition(st);
                t.AddCondition(AnimatorConditionMode.If, 0, "Dead"); t.duration = 0.1f; t.canTransitionToSelf = false;
            }
            AssetDatabase.SaveAssets();
            return ctrl;
        }

        // ---------- Escena rápida para probar un personaje en el editor ----------
        [MenuItem("Forja3D/Crear escena de prueba")]
        static void EscenaPrueba()
        {
            var suelo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            suelo.name = "Suelo"; suelo.transform.localScale = new Vector3(10, 1, 10);
            if (!UnityEngine.Object.FindObjectOfType<Light>())
            {
                var luz = new GameObject("Sol").AddComponent<Light>();
                luz.type = LightType.Directional; luz.transform.rotation = Quaternion.Euler(50, -30, 0);
            }
            var prefab = AssetDatabase.FindAssets("t:Prefab", new[] { Raiz }).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
            if (prefab == null) { Debug.Log("[Forja3D] Importa un personaje primero."); return; }
            var p = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefab));
            p.tag = "Player";
            if (!p.GetComponent<CharacterController>()) p.AddComponent<CharacterController>();
            if (!p.GetComponent<Forja3DHealth>()) p.AddComponent<Forja3DHealth>().esJugador = true;
            if (!p.GetComponent<Forja3DPlayerController>()) p.AddComponent<Forja3DPlayerController>();
            new GameObject("Forja3D").AddComponent<Forja3DGameManager>();
            Selection.activeObject = p;
        }
    }

    // Configura automáticamente los FBX de Assets/Forja3D/Modelos
    public class Forja3DPostprocessor : AssetPostprocessor
    {
        static readonly string[] EnBucle = { "idle", "caminar", "correr", "walk", "run", "bailar", "combate" };

        bool EsNuestro => assetPath.StartsWith(Forja3DImporter.Raiz) && assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);

        void OnPreprocessModel()
        {
            if (!EsNuestro) return;
            var mi = (ModelImporter)assetImporter;
            var m = Forja3DImporter.LeerManifiesto(Path.GetDirectoryName(assetPath));
            if (m != null && !m.personaje)
            {
                mi.animationType = ModelImporterAnimationType.None; mi.importAnimation = false;
                mi.isReadable = false;
                return;
            }
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        }

        void OnPreprocessAnimation()
        {
            if (!EsNuestro) return;
            var mi = (ModelImporter)assetImporter;
            var m = Forja3DImporter.LeerManifiesto(Path.GetDirectoryName(assetPath));
            if (m == null || !m.personaje) return;
            var archivo = Path.GetFileNameWithoutExtension(assetPath);
            var a = m.archivos?.FirstOrDefault(x => x != null && Forja3DImporter.Limpio(x.nombre) == archivo);
            if (a == null) return;
            var clips = mi.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            // Nombra los clips en el mismo orden en que el conector los pidió
            string[] nombres = a.clips != null && a.clips.Length == clips.Length ? a.clips : clips.Length == 1 ? new[] { a.nombre } : null;
            for (int i = 0; i < clips.Length; i++)
            {
                if (nombres != null) clips[i].name = nombres[i];
                var n = clips[i].name.ToLowerInvariant();
                clips[i].loopTime = EnBucle.Any(k => n.Contains(k));
                clips[i].lockRootRotation = clips[i].lockRootHeightY = clips[i].lockRootPositionXZ = true;
                clips[i].keepOriginalOrientation = clips[i].keepOriginalPositionY = clips[i].keepOriginalPositionXZ = true;
            }
            mi.clipAnimations = clips;
        }
    }
}
#endif
