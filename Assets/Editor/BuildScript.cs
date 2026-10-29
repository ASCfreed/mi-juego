using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Compilación del APK desde el menú o en batchmode:
/// Unity -batchmode -quit -projectPath &lt;ruta&gt; -buildTarget Android -executeMethod BuildScript.BuildAndroid -logFile -
/// </summary>
public static class BuildScript
{
    const string OutputPath = "Builds/ParticulasRA.apk";

    [MenuItem("Partículas RA/3. Compilar APK")]
    public static void BuildAndroid()
    {
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0) scenes = new[] { "Assets/Scenes/Main.unity" };

        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath) ?? "Builds");
        EditorUserBuildSettings.buildAppBundle = false;

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        bool ok = report.summary.result == BuildResult.Succeeded;

        if (ok)
            Debug.Log($"[BuildScript] APK generado: {Path.GetFullPath(OutputPath)} ({report.summary.totalSize / (1024 * 1024)} MB)");
        else
            Debug.LogError($"[BuildScript] La compilación falló: {report.summary.result} ({report.summary.totalErrors} errores)");

        if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        else if (ok) EditorUtility.RevealInFinder(OutputPath);
    }
}
