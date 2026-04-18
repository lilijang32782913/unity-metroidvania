using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Metroidvania.Diagnostics {
    public static class DiagLog {
        private static readonly object s_lock = new object();
        private static string s_logPath;
        private static string s_fallbackLogPath;
        private static string s_snapshotPath;
        private static string s_fallbackSnapshotPath;
        private static bool s_initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void Initialize() {
            if (s_initialized)
                return;

            s_initialized = true;
            try {
                string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                string runDirectory = !string.IsNullOrEmpty(exePath)
                    ? Path.GetDirectoryName(exePath)
                    : Directory.GetParent(Application.dataPath)?.FullName;

                if (!string.IsNullOrEmpty(runDirectory))
                {
                    Directory.CreateDirectory(runDirectory);
                    s_logPath = Path.Combine(runDirectory, "runtime-diag.log");
                    s_snapshotPath = Path.Combine(runDirectory, "runtime-diag-error-snapshot.log");
                }

                // Fallback in case run directory is not writable.
                s_fallbackLogPath = Path.Combine(Application.persistentDataPath, "runtime-diag.log");
                s_fallbackSnapshotPath = Path.Combine(Application.persistentDataPath, "runtime-diag-error-snapshot.log");

                Append("INFO", "=== Runtime diagnostics started ===");
                Append("INFO", $"unity={Application.unityVersion}, product={Application.productName}, platform={Application.platform}");
                Append("INFO", $"diagLogPath={s_logPath}");
                Append("INFO", $"diagLogFallbackPath={s_fallbackLogPath}");
                Append("INFO", $"diagSnapshotPath={s_snapshotPath}");
                Append("INFO", $"diagSnapshotFallbackPath={s_fallbackSnapshotPath}");
            }
            catch (Exception ex) {
                Debug.LogError($"[Diag] Failed to initialize file logger: {ex}");
            }
        }

        public static void Info(string message) {
            Debug.Log(message);
            Append("INFO", message);
        }

        public static void Warning(string message) {
            Debug.LogWarning(message);
            Append("WARN", message);
        }

        public static void Error(string message) {
            Debug.LogError(message);
            Append("ERROR", message);
        }

        public static void SaveErrorSnapshot(string reason, int tailLines = 200) {
            try {
                if (!s_initialized)
                    Initialize();

                string sourcePath = ResolveReadablePath(s_logPath, s_fallbackLogPath);
                if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                    return;

                string[] lines = File.ReadAllLines(sourcePath);
                int takeCount = Mathf.Min(Mathf.Max(tailLines, 1), lines.Length);
                int start = lines.Length - takeCount;

                List<string> snapshot = new List<string>(takeCount + 6) {
                    "============================================================",
                    $"SnapshotTime={DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}",
                    $"Reason={reason}",
                    $"Source={sourcePath}",
                    "-------------------- Recent Logs --------------------"
                };

                for (int i = start; i < lines.Length; i++) {
                    snapshot.Add(lines[i]);
                }

                snapshot.Add("============================================================");
                snapshot.Add(string.Empty);

                string output = string.Join(System.Environment.NewLine, snapshot) + System.Environment.NewLine;

                lock (s_lock) {
                    bool written = TryAppendToPath(s_snapshotPath, output);
                    if (!written)
                        TryAppendToPath(s_fallbackSnapshotPath, output);
                }
            }
            catch {
                // Never break gameplay because of diagnostics logging.
            }
        }

        private static void Append(string level, string message) {
            try {
                if (!s_initialized)
                    Initialize();

                if (string.IsNullOrEmpty(s_logPath))
                    return;

                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{System.Environment.NewLine}";
                lock (s_lock) {
                    bool written = TryAppendToPath(s_logPath, line);
                    if (!written)
                        TryAppendToPath(s_fallbackLogPath, line);
                }
            }
            catch {
                // Never break gameplay because of diagnostics logging.
            }
        }

        private static string ResolveReadablePath(string primary, string fallback) {
            if (!string.IsNullOrEmpty(primary) && File.Exists(primary))
                return primary;
            if (!string.IsNullOrEmpty(fallback) && File.Exists(fallback))
                return fallback;
            return null;
        }

        private static bool TryAppendToPath(string path, string content) {
            try {
                if (string.IsNullOrEmpty(path))
                    return false;

                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                File.AppendAllText(path, content);
                return true;
            }
            catch {
                return false;
            }
        }
    }
}