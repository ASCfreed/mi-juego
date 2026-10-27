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
            bool aab = Arg("-formato", "apk") == "aab";
            string ext = aab ? ".aab" : ".apk";
            string salida = Arg("-customBuildPath", "build/Android/juego" + ext);
            if (salida.EndsWith(".apk", StringComparison.OrdinalIgnoreCase) || salida.EndsWith(".aab", StringComparison.OrdinalIgnoreCase))
                salida = Path.ChangeExtension(salida, ext);
            else salida = Path.Combine(salida, "juego" + ext);
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
            // Google Play exige apuntar a Android 16 (API 36) desde el 31-ago-2026
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
            // Texturas comprimidas en ASTC (menos peso y memoria en móvil)
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = PlayerSettings.allowedAutorotateToLandscapeRight = true;
            EditorUserBuildSettings.buildAppBundle = aab;
            if (aab)
            {
                // Firma con la clave de subida (el workflow decodifica el keystore desde los secretos de GitHub)
                string ks = Path.GetFullPath(Arg("-keystorePath", "android.keystore"));
                if (!File.Exists(ks)) throw new Exception("Falta el keystore para firmar el AAB. Agrega los secretos ANDROID_KEYSTORE_BASE64, ANDROID_KEYSTORE_PASS, ANDROID_KEYALIAS_NAME y ANDROID_KEYALIAS_PASS.");
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = ks;
                PlayerSettings.Android.keystorePass = Arg("-keystorePass", "");
                PlayerSettings.Android.keyaliasName = Arg("-keyaliasName", "");
                PlayerSettings.Android.keyaliasPass = Arg("-keyaliasPass", "");
            }
            else PlayerSettings.Android.useCustomKeystore = false;

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
            Debug.Log($"[Forja3D] Formato: {(aab ? "AAB (Google Play)" : "APK")} · API destino 36");
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
