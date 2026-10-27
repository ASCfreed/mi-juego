// Forja3D — construye escenas jugables a partir de los niveles JSON de Assets/Forja3D/Niveles
// (los escribe el conector con la herramienta crear_nivel). Cada carácter del "mapa" es una celda.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Forja3D.EditorTools
{
    [Serializable] public class Simbolo { public string simbolo; public string tipo; public string modelo; public int vida = 2; public float escala = 1f; public string color; }
    [Serializable] public class JugadorDef { public string modelo; public int vida = 5; }
    [Serializable] public class TerrenoDef { public string tipo = "pasto"; public float colinas = 0.5f; public int semilla = 1; }
    [Serializable]
    public class Nivel
    {
        public string titulo = "Nivel";
        public float celda = 2f;
        public string[] mapa;
        public Simbolo[] leyenda;
        public JugadorDef jugador = new JugadorDef();
        public TerrenoDef terreno = new TerrenoDef();
        public string objetivo = "meta";
        public string cielo = "#8ec9f0";
        public bool niebla;
        public float altura_muros = 3f;
        public string color_muros = "#8c8478";
    }

    public static class Forja3DLevelBuilder
    {
        public const string RutaNiveles = "Assets/Forja3D/Niveles";
        public const string RutaGenerado = "Assets/Forja3D/Generado";

        const string Demo = @"{""titulo"":""Nivel de demostración"",""objetivo"":""meta"",""mapa"":[
""##############"",
""#P....#......#"",
""#..C..#..E...#"",
""#.....#......#"",
""#..T......C..#"",
""#.....####...#"",
""#..E.....T...#"",
""#.........C.M#"",
""##############""]}";

        [MenuItem("Forja3D/Construir niveles")]
        public static void Menu()
        {
            var escenas = ConstruirTodos();
            EditorUtility.DisplayDialog("Forja3D", $"{escenas.Count} nivel(es) construidos en {RutaGenerado}/Escenas", "OK");
            if (escenas.Count > 0) EditorSceneManager.OpenScene(escenas[0]);
        }

        public static List<string> ConstruirTodos()
        {
            if (AssetDatabase.IsValidFolder(RutaGenerado)) AssetDatabase.DeleteAsset(RutaGenerado);
            Directory.CreateDirectory($"{RutaGenerado}/Escenas");
            Directory.CreateDirectory($"{RutaGenerado}/Datos");
            AssetDatabase.Refresh();

            var jsons = Directory.Exists(RutaNiveles)
                ? Directory.GetFiles(RutaNiveles, "*.json").OrderBy(f => f, StringComparer.Ordinal).ToList()
                : new List<string>();
            var escenas = new List<string>();
            if (jsons.Count == 0)
            {
                Debug.Log("[Forja3D] No hay niveles: construyo el nivel de demostración.");
                escenas.Add(Construir(JsonUtility.FromJson<Nivel>(Demo), "00_demo"));
            }
            else foreach (var j in jsons)
                escenas.Add(Construir(JsonUtility.FromJson<Nivel>(File.ReadAllText(j)), Path.GetFileNameWithoutExtension(j)));
            return escenas;
        }

        // ---------- Construcción ----------
        static readonly Dictionary<char, Simbolo> Base = new Dictionary<char, Simbolo>
        {
            ['#'] = new Simbolo { tipo = "muro" }, ['.'] = new Simbolo { tipo = "suelo" }, [' '] = new Simbolo { tipo = "suelo" },
            ['P'] = new Simbolo { tipo = "jugador" }, ['M'] = new Simbolo { tipo = "meta" }, ['E'] = new Simbolo { tipo = "enemigo" },
            ['C'] = new Simbolo { tipo = "moneda" }, ['T'] = new Simbolo { tipo = "arbol" }, ['R'] = new Simbolo { tipo = "roca" },
        };

        static string Construir(Nivel n, string id)
        {
            if (n.mapa == null || n.mapa.Length == 0) throw new Exception($"Nivel {id}: falta el mapa.");
            var leyenda = new Dictionary<char, Simbolo>(Base);
            if (n.leyenda != null) foreach (var s in n.leyenda) if (!string.IsNullOrEmpty(s.simbolo)) leyenda[s.simbolo[0]] = s;

            var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            float c = Mathf.Max(1f, n.celda);
            int filas = n.mapa.Length, cols = n.mapa.Max(f => f.Length);
            Vector3 Pos(int f, int col) => new Vector3(col * c, 0, (filas - 1 - f) * c);

            // Ambiente
            var luz = new GameObject("Sol").AddComponent<Light>();
            luz.type = LightType.Directional; luz.shadows = LightShadows.Soft; luz.intensity = 1.1f;
            luz.transform.rotation = Quaternion.Euler(50, -35, 0);
            var cielo = Hex(n.cielo, new Color(0.56f, 0.79f, 0.94f));
            var sky = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            if (sky) { var m = new Material(sky); if (m.HasProperty("_SkyTint")) m.SetColor("_SkyTint", cielo); Guardar(m, $"cielo_{id}"); RenderSettings.skybox = m; }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = cielo; RenderSettings.ambientEquatorColor = cielo * 0.7f; RenderSettings.ambientGroundColor = new Color(0.3f, 0.28f, 0.25f);
            RenderSettings.fog = n.niebla; RenderSettings.fogColor = cielo; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 15; RenderSettings.fogEndDistance = 60;

            var nivel = new GameObject("Nivel");
            CrearTerreno(n, id, filas, cols, c, nivel.transform);

            // Muros: une celdas seguidas de cada fila en un solo bloque
            var matMuro = Mat("muro_" + id, Hex(n.color_muros, new Color(0.55f, 0.52f, 0.47f)));
            for (int f = 0; f < filas; f++)
                for (int col = 0; col < cols; col++)
                {
                    if (Tipo(n, leyenda, f, col) != "muro") continue;
                    int fin = col;
                    while (fin + 1 < cols && Tipo(n, leyenda, f, fin + 1) == "muro") fin++;
                    var muro = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    muro.name = "Muro"; muro.transform.SetParent(nivel.transform);
                    muro.transform.position = (Pos(f, col) + Pos(f, fin)) / 2f + Vector3.up * n.altura_muros / 2f;
                    muro.transform.localScale = new Vector3((fin - col + 1) * c, n.altura_muros, c);
                    muro.GetComponent<Renderer>().sharedMaterial = matMuro;
                    GameObjectUtility.SetStaticEditorFlags(muro, StaticEditorFlags.BatchingStatic);
                    col = fin;
                }

            // Decoración (obstáculos para el NavMesh)
            for (int f = 0; f < filas; f++)
                for (int col = 0; col < cols; col++)
                {
                    var s = Sim(n, leyenda, f, col);
                    if (s == null || (s.tipo != "arbol" && s.tipo != "roca" && s.tipo != "decoracion")) continue;
                    var d = Decoracion(s, c, id);
                    d.transform.SetParent(nivel.transform);
                    d.transform.position = Pos(f, col);
                    d.transform.rotation = Quaternion.Euler(0, (f * 73 + col * 37) % 360, 0);
                }

            // NavMesh antes de poner personajes y objetos dinámicos
            var surf = nivel.AddComponent<NavMeshSurface>();
            surf.collectObjects = CollectObjects.Children;
            surf.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surf.BuildNavMesh();
            if (surf.navMeshData) AssetDatabase.CreateAsset(surf.navMeshData, $"{RutaGenerado}/Datos/navmesh_{id}.asset");

            Vector3? inicio = null, meta = null;
            var dinamicos = new GameObject("Dinamicos").transform;
            for (int f = 0; f < filas; f++)
                for (int col = 0; col < cols; col++)
                {
                    var s = Sim(n, leyenda, f, col);
                    if (s == null) continue;
                    var p = Pos(f, col);
                    switch (s.tipo)
                    {
                        case "jugador": inicio = p; Jugador(n, p, dinamicos); break;
                        case "enemigo": Enemigo(s, p, dinamicos, id); break;
                        case "moneda": Moneda(p, dinamicos, id); break;
                        case "meta": meta = p; Meta(p, c, dinamicos, id); break;
                    }
                }
            if (inicio == null) throw new Exception($"Nivel {id}: falta la P del jugador.");
            if (meta == null) throw new Exception($"Nivel {id}: falta la M de la meta.");

            // Validación: debe existir camino del jugador a la meta
            var camino = new NavMeshPath();
            if (NavMesh.SamplePosition(inicio.Value, out var a, 2f, NavMesh.AllAreas) &&
                NavMesh.SamplePosition(meta.Value, out var b, 2f, NavMesh.AllAreas))
            {
                NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, camino);
                if (camino.status != NavMeshPathStatus.PathComplete)
                    throw new Exception($"Nivel {id}: no hay camino de P a M (estado {camino.status}). Revisa los muros.");
            }
            else throw new Exception($"Nivel {id}: el jugador o la meta quedaron fuera del NavMesh.");

            // Cámara y reglas
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>(); cam.farClipPlane = 400; camGo.AddComponent<AudioListener>();
            camGo.transform.position = inicio.Value + new Vector3(0, 4, -5);
            camGo.AddComponent<Forja3DCamera>().objetivo = GameObject.FindWithTag("Player").transform;
            var gm = new GameObject("Forja3D").AddComponent<Forja3DGameManager>();
            gm.titulo = n.titulo;
            gm.objetivo = n.objetivo == "enemigos" ? Objetivo.Enemigos : n.objetivo == "monedas" ? Objetivo.Monedas : Objetivo.Meta;

            string ruta = $"{RutaGenerado}/Escenas/{id}.unity";
            EditorSceneManager.SaveScene(escena, ruta);
            Debug.Log($"[Forja3D] Nivel construido: {ruta}");
            return ruta;
        }

        static Simbolo Sim(Nivel n, Dictionary<char, Simbolo> l, int f, int col)
        {
            var fila = n.mapa[f];
            char ch = col < fila.Length ? fila[col] : ' ';
            return l.TryGetValue(ch, out var s) ? s : null;
        }
        static string Tipo(Nivel n, Dictionary<char, Simbolo> l, int f, int col) => Sim(n, l, f, col)?.tipo;

        // ---------- Terreno: plano en la zona jugable, colinas alrededor ----------
        static void CrearTerreno(Nivel n, string id, int filas, int cols, float c, Transform padre)
        {
            float margen = 30f, alturaMax = 18f;
            float ancho = cols * c + margen * 2, largo = filas * c + margen * 2;
            var td = new TerrainData { heightmapResolution = 257 };
            td.size = new Vector3(ancho, alturaMax, largo);
            int res = td.heightmapResolution;
            var h = new float[res, res];
            var rnd = new System.Random(n.terreno?.semilla ?? 1);
            float ox = (float)rnd.NextDouble() * 1000, oz = (float)rnd.NextDouble() * 1000;
            float colinas = Mathf.Clamp01(n.terreno?.colinas ?? 0.5f);
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                {
                    float wx = x / (float)(res - 1) * ancho - margen - c / 2f;
                    float wz = z / (float)(res - 1) * largo - margen - c / 2f;
                    float dx = Mathf.Max(0, -wx, wx - cols * c), dz = Mathf.Max(0, -wz, wz - filas * c);
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    float k = Mathf.Clamp01((d - 1f) / (margen * 0.6f));
                    float ruido = Mathf.PerlinNoise(ox + wx * 0.05f, oz + wz * 0.05f);
                    h[z, x] = k * k * colinas * (0.4f + 0.6f * ruido);
                }
            td.SetHeights(0, 0, h);

            var (col1, col2) = (n.terreno?.tipo ?? "pasto") switch
            {
                "arena" => (new Color(0.86f, 0.78f, 0.55f), new Color(0.78f, 0.68f, 0.45f)),
                "nieve" => (new Color(0.93f, 0.95f, 0.98f), new Color(0.82f, 0.86f, 0.92f)),
                "piedra" => (new Color(0.5f, 0.5f, 0.5f), new Color(0.4f, 0.4f, 0.42f)),
                "lava" => (new Color(0.25f, 0.2f, 0.2f), new Color(0.45f, 0.15f, 0.08f)),
                _ => (new Color(0.36f, 0.6f, 0.28f), new Color(0.28f, 0.5f, 0.22f)),
            };
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, true);
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                    tex.SetPixel(x, y, UnityEngine.Color.Lerp(col1, col2, Mathf.PerlinNoise(x * 0.15f, y * 0.15f)));
            tex.Apply();
            var rutaTex = $"{RutaGenerado}/Datos/suelo_{id}.png";
            File.WriteAllBytes(rutaTex, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(rutaTex);
            var capa = new TerrainLayer { diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(rutaTex), tileSize = new Vector2(6, 6) };
            AssetDatabase.CreateAsset(capa, $"{RutaGenerado}/Datos/capa_{id}.terrainlayer");
            td.terrainLayers = new[] { capa };
            AssetDatabase.CreateAsset(td, $"{RutaGenerado}/Datos/terreno_{id}.asset");

            var t = Terrain.CreateTerrainGameObject(td);
            t.name = "Terreno"; t.transform.SetParent(padre);
            t.transform.position = new Vector3(-margen - c / 2f, 0, -margen - c / 2f);
        }

        // ---------- Entidades ----------
        static GameObject InstanciarModelo(string modelo)
        {
            var pf = Forja3DImporter.RutaPrefab(modelo);
            if (!pf) { if (!string.IsNullOrEmpty(modelo)) Debug.LogWarning($"[Forja3D] Modelo '{modelo}' no encontrado: uso una figura simple."); return null; }
            var g = (GameObject)PrefabUtility.InstantiatePrefab(pf);
            PrefabUtility.UnpackPrefabInstance(g, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            return g;
        }

        static GameObject Capsula(string nombre, Color color, string id)
        {
            var raiz = new GameObject(nombre);
            var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            UnityEngine.Object.DestroyImmediate(cap.GetComponent<Collider>());
            cap.transform.SetParent(raiz.transform); cap.transform.localPosition = Vector3.up;
            cap.GetComponent<Renderer>().sharedMaterial = Mat(nombre.ToLowerInvariant() + "_" + id, color);
            var ojo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.DestroyImmediate(ojo.GetComponent<Collider>());
            ojo.transform.SetParent(raiz.transform); ojo.transform.localPosition = new Vector3(0, 1.5f, 0.45f); ojo.transform.localScale = new Vector3(0.6f, 0.15f, 0.1f);
            ojo.GetComponent<Renderer>().sharedMaterial = Mat("ojos", UnityEngine.Color.black);
            return raiz;
        }

        static void Jugador(Nivel n, Vector3 p, Transform padre)
        {
            var g = InstanciarModelo(n.jugador?.modelo) ?? Capsula("Jugador", new Color(0.2f, 0.45f, 0.95f), "j");
            g.name = "Jugador"; g.tag = "Player"; g.transform.SetParent(padre);
            g.transform.position = p + Vector3.up * 0.05f;
            var cc = g.GetComponent<CharacterController>();
            if (!cc) { cc = g.AddComponent<CharacterController>(); cc.height = 2f; cc.radius = 0.4f; cc.center = Vector3.up; }
            var h = g.AddComponent<Forja3DHealth>(); h.esJugador = true; h.vidaMaxima = Mathf.Max(1, n.jugador?.vida ?? 5);
            g.AddComponent<Forja3DPlayerController>().crearCamaraAutomatica = false;
        }

        static void Enemigo(Simbolo s, Vector3 p, Transform padre, string id)
        {
            var g = InstanciarModelo(s.modelo) ?? Capsula("Enemigo", Hex(s.color, new Color(0.85f, 0.2f, 0.2f)), id);
            g.name = "Enemigo"; g.transform.SetParent(padre);
            g.transform.position = p; g.transform.localScale *= Mathf.Max(0.2f, s.escala);
            var cc = g.GetComponent<CharacterController>();
            float alto = cc ? cc.height : 2f, radio = cc ? cc.radius : 0.4f;
            if (cc) UnityEngine.Object.DestroyImmediate(cc);
            var col = g.AddComponent<CapsuleCollider>(); col.height = alto; col.radius = radio; col.center = Vector3.up * alto / 2f;
            var ag = g.AddComponent<NavMeshAgent>(); ag.height = alto; ag.radius = radio;
            g.AddComponent<Forja3DHealth>().vidaMaxima = Mathf.Max(1, s.vida);
            g.AddComponent<Forja3DEnemy>();
        }

        static void Moneda(Vector3 p, Transform padre, string id)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = "Moneda"; g.transform.SetParent(padre);
            g.transform.position = p + Vector3.up; g.transform.rotation = Quaternion.Euler(90, 0, 0);
            g.transform.localScale = new Vector3(0.7f, 0.06f, 0.7f);
            var m = Mat("moneda", new Color(1f, 0.82f, 0.15f)); m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(0.5f, 0.35f, 0f));
            g.GetComponent<Renderer>().sharedMaterial = m;
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            var t = g.AddComponent<SphereCollider>(); t.isTrigger = true; t.radius = 1.2f;
            g.AddComponent<Forja3DCollectible>();
        }

        static void Meta(Vector3 p, float c, Transform padre, string id)
        {
            var g = new GameObject("Meta"); g.transform.SetParent(padre); g.transform.position = p;
            var col = g.AddComponent<BoxCollider>(); col.isTrigger = true; col.size = new Vector3(c * 0.9f, 3f, c * 0.9f); col.center = Vector3.up * 1.5f;
            var m = Mat("meta", new Color(0.2f, 0.95f, 0.4f)); m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(0.1f, 0.6f, 0.2f));
            var basePlat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            UnityEngine.Object.DestroyImmediate(basePlat.GetComponent<Collider>());
            basePlat.transform.SetParent(g.transform); basePlat.transform.localPosition = Vector3.up * 0.05f; basePlat.transform.localScale = new Vector3(c * 0.8f, 0.05f, c * 0.8f);
            basePlat.GetComponent<Renderer>().sharedMaterial = m;
            var rayo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.DestroyImmediate(rayo.GetComponent<Collider>());
            rayo.transform.SetParent(g.transform); rayo.transform.localPosition = Vector3.up * 2.5f; rayo.transform.localScale = new Vector3(0.3f, 5f, 0.3f);
            rayo.GetComponent<Renderer>().sharedMaterial = m;
            g.AddComponent<Forja3DGoal>();
        }

        static GameObject Decoracion(Simbolo s, float c, string id)
        {
            var g = InstanciarModelo(s.modelo);
            if (g)
            {
                // Ajusta el modelo generado al tamaño de la celda
                var b = Forja3DImporter.Limites(g);
                float lado = Mathf.Max(b.size.x, b.size.z, 0.01f);
                float esc = c * 0.9f / lado * Mathf.Max(0.2f, s.escala);
                if (s.tipo == "arbol") esc = Mathf.Max(esc, 3.5f / Mathf.Max(b.size.y, 0.01f));
                g.transform.localScale *= esc;
                return g;
            }
            g = new GameObject(s.tipo == "roca" ? "Roca" : "Arbol");
            if (s.tipo == "roca")
            {
                var r = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                r.transform.SetParent(g.transform); r.transform.localPosition = Vector3.up * 0.4f; r.transform.localScale = new Vector3(c * 0.8f, c * 0.5f, c * 0.7f);
                r.GetComponent<Renderer>().sharedMaterial = Mat("roca", Hex(s.color, new Color(0.5f, 0.5f, 0.52f)));
            }
            else
            {
                var tronco = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tronco.transform.SetParent(g.transform); tronco.transform.localPosition = Vector3.up * 1f; tronco.transform.localScale = new Vector3(0.4f, 1f, 0.4f);
                tronco.GetComponent<Renderer>().sharedMaterial = Mat("tronco", new Color(0.45f, 0.3f, 0.18f));
                var copa = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                UnityEngine.Object.DestroyImmediate(copa.GetComponent<Collider>());
                copa.transform.SetParent(g.transform); copa.transform.localPosition = Vector3.up * 2.6f; copa.transform.localScale = Vector3.one * 2f;
                copa.GetComponent<Renderer>().sharedMaterial = Mat("hojas", Hex(s.color, new Color(0.2f, 0.55f, 0.25f)));
            }
            g.transform.localScale *= Mathf.Max(0.2f, s.escala);
            return g;
        }

        // ---------- Utilidades ----------
        static readonly Dictionary<string, Material> cacheMat = new Dictionary<string, Material>();
        static Material Mat(string nombre, Color color)
        {
            var ruta = $"{RutaGenerado}/Datos/mat_{nombre}.mat";
            if (cacheMat.TryGetValue(ruta, out var m) && m) return m;
            m = new Material(Shader.Find("Standard")) { color = color };
            m.SetFloat("_Glossiness", 0.15f);
            AssetDatabase.CreateAsset(m, ruta);
            return cacheMat[ruta] = m;
        }
        static void Guardar(UnityEngine.Object o, string nombre) => AssetDatabase.CreateAsset(o, $"{RutaGenerado}/Datos/{nombre}.mat");

        static Color Hex(string hex, Color def) =>
            !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c) ? c : def;
    }
}
#endif
