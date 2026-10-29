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

    /// <summary>XR Plug-in Management: ARCore como loader de Android, inicializado al arrancar.</summary>
    static void ActivarARCore()
    {
        EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget porTarget);
        if (porTarget == null)
        {
            porTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            if (!AssetDatabase.IsValidFolder("Assets/XR")) AssetDatabase.CreateFolder("Assets", "XR");
            AssetDatabase.CreateAsset(porTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, porTarget, true);
        }

        if (!porTarget.HasSettingsForBuildTarget(BuildTargetGroup.Android))
            porTarget.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
        if (!porTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
            porTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);

        var general = porTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
        var manager = porTarget.ManagerSettingsForBuildTarget(BuildTargetGroup.Android);
        general.InitManagerOnStart = true;

        bool yaEsta = manager.activeLoaders.Any(l => l != null && l.GetType().FullName == LoaderARCore);
        if (!yaEsta && !XRPackageMetadataStore.AssignLoader(manager, LoaderARCore, BuildTargetGroup.Android))
            throw new System.Exception("[Forja3D] ERROR: no pude activar ARCore en XR Plug-in Management (¿falta el paquete com.unity.xr.arcore?).");

        EditorUtility.SetDirty(porTarget);
        EditorUtility.SetDirty(general);
        EditorUtility.SetDirty(manager);
        AssetDatabase.SaveAssets();
        Debug.Log("[Forja3D] ARCore activado en XR Plug-in Management (Android).");
    }
}
