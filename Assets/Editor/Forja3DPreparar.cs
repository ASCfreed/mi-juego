using System.Linq;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;

/// <summary>
/// Gancho de Forja3D: el Forja3DBuild genérico (plantilla "basica") llama a Preparar() antes de compilar.
/// Deja el proyecto listo sin tocar nada a mano: Input System, ARCore en XR Plug-in Management,
/// Player Settings y la escena completa. También está en el menú «Partículas RA/0. Preparar todo».
/// </summary>
public static class Forja3DPreparar
{
    const string LoaderARCore = "UnityEngine.XR.ARCore.ARCoreLoader";

    [MenuItem("Partículas RA/0. Preparar todo (Input, ARCore, Player y escena)")]
    static void Menu()
    {
        Preparar();
        EditorUtility.DisplayDialog("Partículas RA", "Proyecto preparado: Input System, ARCore, Player Settings y escena Main.", "OK");
    }

    public static void Preparar()
    {
        Debug.Log("[Forja3D] Partículas RA: preparando el proyecto…");
        ActivarInputSystem();
        ActivarARCore();
        SceneSetup.ConfigurePlayer(true);
        SceneSetup.CreateScene(true);
        AssetDatabase.SaveAssets();
        Debug.Log("[Forja3D] Partículas RA: listo para compilar.");
    }

    /// <summary>Active Input Handling = Both (2) si estaba en el sistema antiguo.</summary>
    static void ActivarInputSystem()
    {
        var ps = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault(o => o is PlayerSettings);
        if (ps == null) { Debug.LogWarning("[Forja3D] No pude abrir ProjectSettings.asset para activar el Input System."); return; }
        var so = new SerializedObject(ps);
        var p = so.FindProperty("activeInputHandler");
        if (p == null) { Debug.LogWarning("[Forja3D] ProjectSettings no tiene activeInputHandler."); return; }
        if (p.intValue == 0)
        {
            p.intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[Forja3D] Active Input Handling → Both (Input System activado).");
        }
    }

    /// <summary>
    /// XR Plug-in Management: ARCore como loader de Android, inicializado al arrancar.
    /// Se asegura de que los ajustes, el manager y el loader queden GUARDADOS como assets (si solo
    /// viven en memoria, el APK sale sin XR y la cámara nunca se abre). Si algo falta, falla la compilación.
    /// </summary>
    static void ActivarARCore()
    {
        const string ruta = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
        if (!AssetDatabase.IsValidFolder("Assets/XR")) AssetDatabase.CreateFolder("Assets", "XR");

        EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget porTarget);
        if (porTarget == null) porTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(ruta);
        if (porTarget == null)
        {
            porTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(porTarget, ruta);
        }
        string rutaPorTarget = AssetDatabase.GetAssetPath(porTarget);
        EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, porTarget, true);

        if (!porTarget.HasSettingsForBuildTarget(BuildTargetGroup.Android))
            porTarget.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
        var general = porTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
        if (general == null) throw new System.Exception("[Forja3D] ERROR: no se pudieron crear los ajustes XR de Android.");
        if (!AssetDatabase.Contains(general)) { general.name = "Android Settings"; AssetDatabase.AddObjectToAsset(general, rutaPorTarget); }

        if (general.Manager == null)
        {
            if (!porTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
                porTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            general.Manager = porTarget.ManagerSettingsForBuildTarget(BuildTargetGroup.Android);
        }
        var manager = general.Manager;
        if (manager == null)
        {
            manager = ScriptableObject.CreateInstance<XRManagerSettings>();
            general.Manager = manager;
        }
        if (!AssetDatabase.Contains(manager)) { manager.name = "Android Providers"; AssetDatabase.AddObjectToAsset(manager, rutaPorTarget); }

        general.InitManagerOnStart = true;
        EditorUtility.SetDirty(porTarget);
        EditorUtility.SetDirty(general);
        EditorUtility.SetDirty(manager);
        AssetDatabase.SaveAssets();

        bool yaEsta = manager.activeLoaders.Any(l => l != null && l.GetType().FullName == LoaderARCore);
        if (!yaEsta && !XRPackageMetadataStore.AssignLoader(manager, LoaderARCore, BuildTargetGroup.Android))
            throw new System.Exception("[Forja3D] ERROR: no pude activar ARCore en XR Plug-in Management (¿falta el paquete com.unity.xr.arcore?).");
        EditorUtility.SetDirty(manager);
        AssetDatabase.SaveAssets();

        // ---------- Verificación: si falla, mejor que falle la compilación que un APK sin RA ----------
        EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget check);
        var g = check != null ? check.SettingsForBuildTarget(BuildTargetGroup.Android) : null;
        var loader = g?.Manager != null ? g.Manager.activeLoaders.FirstOrDefault(l => l != null && l.GetType().FullName == LoaderARCore) : null;
        string estado = $"config={(check != null)} ajustes={(g != null && AssetDatabase.Contains(g))} " +
                        $"manager={(g?.Manager != null && AssetDatabase.Contains(g.Manager))} loader={(loader != null && AssetDatabase.Contains(loader))} " +
                        $"initOnStart={(g != null && g.InitManagerOnStart)} loaders=[{(g?.Manager != null ? string.Join(",", g.Manager.activeLoaders.Where(l => l != null).Select(l => l.GetType().Name)) : "")}]";
        Debug.Log("[Forja3D] XR Android: " + estado);
        if (check == null || g == null || !AssetDatabase.Contains(g) || g.Manager == null || !AssetDatabase.Contains(g.Manager) ||
            loader == null || !AssetDatabase.Contains(loader) || !g.InitManagerOnStart)
            throw new System.Exception("[Forja3D] ERROR: la configuración XR de Android no quedó guardada (" + estado + ").");
    }
}
