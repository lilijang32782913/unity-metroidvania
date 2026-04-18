using System.IO;
using UnityEditor;
using UnityEditor.Build;

public static class CIBuilder
{
    public static void BuildWindows()
    {
        var outputDir = Path.GetFullPath("Build");
        Directory.CreateDirectory(outputDir);

        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Initialize.unity" },
            locationPathName = Path.Combine(outputDir, "Metroidvania.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            throw new BuildFailedException("Build failed. See Unity log for details.");
        }
    }
}