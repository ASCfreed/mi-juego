using System;
using System.IO;
using ParticulasRA;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Object = UnityEngine.Object;

/// <summary>
/// Menú «Partículas RA»: configura el Player y monta la escena completa (jerarquía, materiales,
/// prefab de plano y referencias entre componentes) con un clic.
/// </summary>
public static class SceneSetup
{
    // ===== VARIABLES (sección VARIABLES EDITABLES del superprompt) =====
    const string AppName = "Partículas RA";
    const string PackageName = "com.ejemplo.particulasra";
    const string Version = "1.0.0";
    const int VersionCode = 1;
    const float ChamberSize = 0.5f; // metros
    // ===================================================================

    const string MatDir = "Assets/Materials";
    const string PrefabDir = "Assets/Prefabs";
    const string SceneDir = "Assets/Scenes";
    const string ScenePath = SceneDir + "/Main.unity";

    [MenuItem("Partículas RA/1. Configurar Player Settings (Android)")]
    static void ConfigurePlayerMenu() => ConfigurePlayer(false);

    [MenuItem("Partículas RA/2. Crear escena")]
    static void CreateSceneMenu() => CreateScene(false);

    /// <summary>Configura el Player para Android. silencioso = sin diálogos (compilación en la nube).</summary>
    public static void ConfigurePlayer(bool silencioso)
    {
        PlayerSettings.productName = AppName;
        PlayerSettings.companyName = "Ejemplo";
        PlayerSettings.bundleVersion = Version;
        PlayerSettings.Android.bundleVersionCode = VersionCode;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        EditorUserBuildSettings.buildAppBundle = false;
        AssetDatabase.SaveAssets();
        Debug.Log("[Forja3D] Player Settings configurados para Android (IL2CPP, ARM64, GLES3, vertical).");
        if (silencioso) return;

        EditorUtility.DisplayDialog(AppName,
            "Player Settings configurados para Android.\n\nFalta (manual):\n" +
            "• Project Settings › XR Plug-in Management › Android › marcar ARCore\n" +
            "• Project Settings › XR Plug-in Management › Project Validation › Fix All\n" +
            "• Player › Active Input Handling = Input System Package (New) o Both", "Entendido");
    }

    /// <summary>Monta la escena completa. silencioso = sin diálogos (compilación en la nube).</summary>
    public static void CreateScene(bool silencioso)
    {
        if (!silencioso && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // Shader propio (Assets/Shaders/PR_Unlit.shader): funciona en el pipeline integrado y en URP
        Shader unlit = Shader.Find("ParticulasRA/Unlit");
        Shader sprites = Shader.Find("Sprites/Default");
        if (unlit == null || sprites == null)
            throw new Exception("[Forja3D] ERROR: falta el shader ParticulasRA/Unlit (Assets/Shaders/PR_Unlit.shader) o Sprites/Default.");

        EnsureFolder(MatDir);
        EnsureFolder(PrefabDir);
        EnsureFolder(SceneDir);

        // --- Materiales (assets: así sus variantes de shader entran en el build) ---
        var core = Mat(MatDir + "/PR_Core.mat", unlit, m =>
        {
            ProceduralAssets.SetColor(m, Color.white);
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.Zero);
            m.SetFloat("_ZWrite", 1f);
            m.SetFloat("_Cull", 0f);
            m.renderQueue = (int)RenderQueue.Geometry;
            m.enableInstancing = true;
        });
        var glow = Mat(MatDir + "/PR_Glow.mat", unlit, m =>
        {
            MakeTransparent(m, true);
            ProceduralAssets.SetColor(m, Color.white);
            m.enableInstancing = true;
        });
        var planeMat = Mat(MatDir + "/PR_Plane.mat", unlit, m =>
        {
            MakeTransparent(m, false);
            ProceduralAssets.SetColor(m, new Color(0.31f, 0.82f, 1f, 0.12f));
        });
        var trail = Mat(MatDir + "/PR_Trail.mat", sprites, m => m.color = Color.white);
        var dash = Mat(MatDir + "/PR_TrailDash.mat", sprites, m => m.color = Color.white);
        AssetDatabase.SaveAssets();

        var planePrefab = CreatePlanePrefab(planeMat, trail);

        // --- Escena nueva ---
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateARSession();
        var origin = CreateOrigin();
        var originGO = origin.gameObject;
        var cam = origin.Camera;
        if (cam == null) throw new Exception("[Forja3D] ERROR: el XR Origin no tiene cámara asignada.");
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 30f;
        if (!cam.CompareTag("MainCamera")) cam.tag = "MainCamera";

        var planeMgr = GetOrAdd<ARPlaneManager>(originGO);
        planeMgr.requestedDetectionMode = PlaneDetectionMode.Horizontal;
        planeMgr.planePrefab = planePrefab;
        var rayMgr = GetOrAdd<ARRaycastManager>(originGO);
        GetOrAdd<ARAnchorManager>(originGO);

        // --- Simulación ---
        var simGO = new GameObject("Simulacion");
        var engine = simGO.AddComponent<SimulationEngine>();
        var reactions = simGO.AddComponent<ReactionSystem>();
        var presets = simGO.AddComponent<ExperimentPresets>();

        // --- Cámara de burbujas ---
        var chamberGO = new GameObject("CamaraBurbujas");
        var simRootGO = new GameObject("SimRoot");
        simRootGO.transform.SetParent(chamberGO.transform, false);
        simRootGO.transform.localScale = Vector3.one * (ChamberSize * 0.5f);
        var trails = simRootGO.AddComponent<TrailManager>();
        var renderer = simRootGO.AddComponent<ParticleRenderer>();
        var field = simRootGO.AddComponent<FieldVisualizer>();
        var flashes = simRootGO.AddComponent<FlashEffects>();
        var chamberVis = simRootGO.AddComponent<ChamberVisual>();

        // --- Interfaz ---
        var uiGO = new GameObject("Interfaz");
        var ui = uiGO.AddComponent<UIController>();
        var tutorial = uiGO.AddComponent<TutorialController>();

        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<InputSystemUIInputModule>();

        // --- Controladores ---
        var ctrlGO = new GameObject("Controladores");
        var placement = ctrlGO.AddComponent<ARPlacementController>();
        var launch = ctrlGO.AddComponent<LaunchInput>();

        // --- Referencias ---
        Set(engine, "reactions", reactions);
        Set(engine, "trails", trails);
        Set(presets, "engine", engine);

        Set(trails, "trailMaterial", trail);
        Set(trails, "dashMaterial", dash);

        Set(renderer, "engine", engine);
        Set(renderer, "coreMaterial", core);
        Set(renderer, "glowMaterial", glow);
        Set(renderer, "viewCamera", cam);

        Set(field, "engine", engine);
        Set(field, "glowMaterial", glow);

        Set(flashes, "engine", engine);
        Set(flashes, "glowMaterial", glow);
        Set(flashes, "viewCamera", cam);

        Set(chamberVis, "glowMaterial", glow);
        Set(chamberVis, "lineMaterial", trail);

        Set(ui, "engine", engine);
        Set(ui, "presets", presets);
        Set(ui, "trails", trails);
        Set(tutorial, "ui", ui);

        Set(placement, "raycastManager", rayMgr);
        Set(placement, "planeManager", planeMgr);
        Set(placement, "arCamera", cam);
        Set(placement, "chamber", chamberGO.transform);
        Set(placement, "simRoot", simRootGO.transform);
        Set(placement, "engine", engine);
        Set(placement, "ui", ui);
        Set(placement, "presets", presets);
        Set(placement, "lineMaterial", trail);
        SetFloat(placement, "chamberSize", ChamberSize);

        Set(launch, "engine", engine);
        Set(launch, "placement", placement);
        Set(launch, "ui", ui);
        Set(launch, "arCamera", cam);
        Set(launch, "simRoot", simRootGO.transform);
        Set(launch, "lineMaterial", trail);

        chamberGO.SetActive(false);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("[Forja3D] Escena creada en " + ScenePath);
        if (silencioso) return;

        EditorUtility.DisplayDialog(AppName,
            "Escena creada en " + ScenePath + " y añadida a Build Settings.\n\n" +
            "Comprueba en XR Plug-in Management › Project Validation que no quedan avisos " +
            "(si usas URP, que el renderer tenga «AR Background Renderer Feature»).", "OK");
    }

    // ------------------------------------------------------------------
    static void CreateARSession()
    {
        if (Object.FindFirstObjectByType<ARSession>() != null) return;
        Selection.activeObject = null;
        if (EditorApplication.ExecuteMenuItem("GameObject/XR/AR Session") && Object.FindFirstObjectByType<ARSession>() != null) return;

        var go = new GameObject("AR Session");
        go.AddComponent<ARSession>();
        // ARInputManager existe en AR Foundation 4/5; en versiones más nuevas puede no existir.
        var inputMgr = Type.GetType("UnityEngine.XR.ARFoundation.ARInputManager, Unity.XR.ARFoundation");
        if (inputMgr != null) go.AddComponent(inputMgr);
    }

    static XROrigin CreateOrigin()
    {
        var existing = Object.FindFirstObjectByType<XROrigin>();
        if (existing != null) return existing;

        foreach (var item in new[] { "GameObject/XR/XR Origin (Mobile AR)", "GameObject/XR/XR Origin (AR)" })
        {
            Selection.activeObject = null;
            if (!EditorApplication.ExecuteMenuItem(item)) continue;
            var o = Object.FindFirstObjectByType<XROrigin>();
            if (o != null && o.Camera != null) return o;
        }
        return BuildOriginManually();
    }

    /// <summary>Alternativa si los menús de AR Foundation no están disponibles.</summary>
    static XROrigin BuildOriginManually()
    {
        var originGO = new GameObject("XR Origin");
        var origin = originGO.AddComponent<XROrigin>();
        var offset = new GameObject("Camera Offset");
        offset.transform.SetParent(originGO.transform, false);

        var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
        camGO.transform.SetParent(offset.transform, false);
        var cam = camGO.AddComponent<Camera>();
        camGO.AddComponent<ARCameraManager>();
        camGO.AddComponent<ARCameraBackground>();

        var tpd = camGO.AddComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
        var posAction = new InputAction("Position", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
        posAction.AddBinding("<HandheldARInputDevice>/devicePosition");
        var rotAction = new InputAction("Rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
        rotAction.AddBinding("<HandheldARInputDevice>/deviceRotation");
        tpd.positionInput = new InputActionProperty(posAction);
        tpd.rotationInput = new InputActionProperty(rotAction);
        tpd.trackingType = UnityEngine.InputSystem.XR.TrackedPoseDriver.TrackingType.RotationAndPosition;
        tpd.updateType = UnityEngine.InputSystem.XR.TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;

        origin.Camera = cam;
        origin.CameraFloorOffsetObject = offset;
        return origin;
    }

    static GameObject CreatePlanePrefab(Material planeMat, Material lineMat)
    {
        string path = PrefabDir + "/PlanoRA.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        var go = new GameObject("PlanoRA");
        go.AddComponent<ARPlane>();
        go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = planeMat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.widthMultiplier = 0.004f;
        lr.sharedMaterial = lineMat;
        lr.startColor = lr.endColor = new Color(0.31f, 0.82f, 1f, 0.6f);
        lr.shadowCastingMode = ShadowCastingMode.Off;
        go.AddComponent<ARPlaneMeshVisualizer>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    /// <summary>Configura el material como transparente: aditivo (brillos) o mezcla alfa.</summary>
    static void MakeTransparent(Material m, bool additive)
    {
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
    }

    static Material Mat(string path, Shader shader, Action<Material> setup)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader);
            setup(m);
            AssetDatabase.CreateAsset(m, path);
        }
        else
        {
            m.shader = shader;
            setup(m);
            EditorUtility.SetDirty(m);
        }
        return m;
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static void Set(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[SceneSetup] Campo «{field}» no encontrado en {target.GetType().Name}"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetFloat(Object target, string field, float value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[SceneSetup] Campo «{field}» no encontrado en {target.GetType().Name}"); return; }
        p.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
