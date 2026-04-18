using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Metroidvania.Diagnostics {
    public static class RuntimeDiagnostics {
        private static float s_lastSnapshotTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void Install() {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            string pipelineName = pipeline != null ? pipeline.name : "BuiltIn";

            DiagLog.Info($"[Diag] Boot. unity={Application.unityVersion}, product={Application.productName}, platform={Application.platform}, quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}, renderPipeline={pipelineName}");

            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.logMessageReceived += OnLogMessageReceived;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            Camera[] cameras = Object.FindObjectsOfType<Camera>(true);
            StringBuilder cameraSummary = new StringBuilder();

            for (int i = 0; i < cameras.Length; i++) {
                Camera cam = cameras[i];
                cameraSummary.Append($"[{i}] name={cam.name}, enabled={cam.enabled}, active={cam.gameObject.activeInHierarchy}, depth={cam.depth}, clear={cam.clearFlags}");
                if (i < cameras.Length - 1)
                    cameraSummary.Append(" | ");
            }

            DiagLog.Info($"[Diag] SceneLoaded. name={scene.name}, mode={mode}, isLoaded={scene.isLoaded}, rootCount={scene.rootCount}, cameraCount={cameras.Length}, cameras={cameraSummary}");
        }

        private static void OnLogMessageReceived(string condition, string stackTrace, LogType type) {
            if (type != LogType.Exception && type != LogType.Error)
                return;

            bool isRenderPipelineError = condition.Contains("UniversalRenderPipeline.SetupPerFrameShaderConstants")
                || stackTrace.Contains("UniversalRenderPipeline.SetupPerFrameShaderConstants")
                || condition.Contains("UnityEngine.Rendering.Universal")
                || stackTrace.Contains("UnityEngine.Rendering.Universal");

            if (isRenderPipelineError) {
                RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
                string pipelineName = pipeline != null ? pipeline.name : "BuiltIn";
                DiagLog.Error($"[Diag] URP frame setup exception detected. currentRenderPipeline={pipelineName}, quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}");

                // Avoid generating dozens of snapshot files on per-frame spam errors.
                if (Time.realtimeSinceStartup - s_lastSnapshotTime > 3f) {
                    s_lastSnapshotTime = Time.realtimeSinceStartup;
                    DiagLog.SaveErrorSnapshot("Render pipeline exception", 200);
                }
            }
        }
    }
}
