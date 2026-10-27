// Forja3D — compilación automática para Android (la llama GitHub Actions / GameCI).
// También: menú Forja3D → Compilar APK (local).
using System;
using System.IO;
using System.Linq;
using Forja3D.EditorTools;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class Forja3DBuild
{
    const string RutaApp = "ProjectSettings/Forja3DApp.json";
    [Serializable] class DatosApp { public string nombre; public string version; public int codigo = 1; }

    static string Arg(string nombre, string porDefecto)
    {
        var a = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(a, nombre);
        return i >= 0 && i + 1 < a.Length && !a[i + 1].StartsWith("-") ? a[i + 1] : porDefecto;
    }

    [MenuItem("Forja3D/Compilar APK")]
    static void Menu() => BuildAndroid();

    public static void BuildAndroid()
    {
        try
        {
            string salida = Arg("-customBuildPath", "build/Android/juego.apk");
            if (!salida.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) salida = Path.Combine(salida, "juego.apk");
            // Nombre/versión: los escribe el workflow en ProjectSettings/Forja3DApp.json
            var app = File.Exists(RutaApp) ? JsonUtility.FromJson<DatosApp>(File.ReadAllText(RutaApp)) : new DatosApp();
            string nombreApp = string.IsNullOrWhiteSpace(app.nombre) ? "Juego Forja3D" : app.nombre;
            string version = string.IsNullOrWhiteSpace(app.version) ? "0.1.0" : app.version;
            int codigo = Mathf.Max(1, app.codigo);

            Debug.Log("[Forja3D] 1/3 Preparando modelos…");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Forja3DImporter.PrepararTodo();

            Debug.Log("[Forja3D] 2/3 Construyendo niveles…");
            var escenas = Forja3DLevelBuilder.ConstruirTodos();
            EditorBuildSettings.scenes = escenas.Select(e => new EditorBuildSettingsScene(e, true)).ToArray();

            Debug.Log("[Forja3D] 3/3 Compilando APK…");
            var t = NamedBuildTarget.Android;
            PlayerSettings.productName = nombreApp;
            PlayerSettings.companyName = "Forja3D";
            var slug = new string(nombreApp.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
            if (string.IsNullOrEmpty(slug) || char.IsDigit(slug[0])) slug = "juego" + slug;
            PlayerSettings.SetApplicationIdentifier(t, "com.forja3d." + slug);
            PlayerSettings.bundleVersion = version;
            PlayerSettings.Android.bundleVersionCode = codigo;
            PlayerSettings.SetScriptingBackend(t, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = PlayerSettings.allowedAutorotateToLandscapeRight = true;
            EditorUserBuildSettings.buildAppBundle = false;

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(salida)));
            var reporte = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = escenas.ToArray(),
                locationPathName = salida,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });
            var r = reporte.summary;
            Debug.Log($"[Forja3D] Resultado: {r.result} · {r.totalSize / (1024f * 1024f):0.0} MB · {r.totalTime} · errores {r.totalErrors}");
            if (r.result != BuildResult.Succeeded) Salir(1);
        }
        catch (Exception e)
        {
            Debug.LogError("[Forja3D] ERROR: " + e);
            Salir(1);
        }
    }

    static void Salir(int codigo)
    {
        if (Application.isBatchMode) EditorApplication.Exit(codigo);
        else if (codigo != 0) EditorUtility.DisplayDialog("Forja3D", "La compilación falló. Revisa la consola.", "OK");
    }
}
