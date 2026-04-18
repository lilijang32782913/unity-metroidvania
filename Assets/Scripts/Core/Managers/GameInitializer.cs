using Metroidvania.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Metroidvania.Settings {
    using Metroidvania.Diagnostics;

    public class GameInitializer : MonoBehaviour {
#if UNITY_EDITOR
        public static event System.Action InitializationFinish;
#endif

        [Header("Scenes")]
        [SerializeField] private AssetReferenceSceneChannel m_mainMenuSceneRef;

        private IEnumerator Start() {
            DiagLog.Info($"[Init] GameInitializer.Start. mainMenuRefValid={m_mainMenuSceneRef.RuntimeKeyIsValid()}");

            if (!m_mainMenuSceneRef.RuntimeKeyIsValid()) {
                DiagLog.Error("Error on game initialization. Exiting the application.");
#if UNITY_EDITOR
                Debug.Break();
#else
                Application.Quit();
#endif
            }

            DiagLog.Info("[Init] Loading Scriptable Singleton assets...");
            AsyncOperationHandle<IList<ScriptableObject>> scriptableSingletonsHandle =
                Addressables.LoadAssetsAsync<ScriptableObject>("Scriptable Singleton", singleton => {
                    if (singleton is IInitializableSingleton initializableSingleton)
                        initializableSingleton.Initialize();
                });

            yield return scriptableSingletonsHandle;

            if (scriptableSingletonsHandle.Status != AsyncOperationStatus.Succeeded) {
                DiagLog.Error($"[Init] Failed to load Scriptable Singleton assets. Exception={scriptableSingletonsHandle.OperationException}");
            }
            else {
                DiagLog.Info($"[Init] Scriptable Singleton assets loaded. Count={scriptableSingletonsHandle.Result?.Count ?? 0}");
            }

            DiagLog.Info("[Init] Loading Persistent Singleton assets...");
            AsyncOperationHandle<IList<GameObject>> persistentSingletonsHandle =
                Addressables.LoadAssetsAsync<GameObject>("Persistent Singleton", persistentSingleton => Instantiate(persistentSingleton));

            yield return persistentSingletonsHandle;

            if (persistentSingletonsHandle.Status != AsyncOperationStatus.Succeeded) {
                DiagLog.Error($"[Init] Failed to load Persistent Singleton assets. Exception={persistentSingletonsHandle.OperationException}");
            }
            else {
                DiagLog.Info($"[Init] Persistent Singleton assets loaded. Count={persistentSingletonsHandle.Result?.Count ?? 0}");
            }

#if UNITY_EDITOR
            if (InitializationFinish != null) {
                InitializationFinish.Invoke();
                yield break;
            }
#endif
            // Prevents SceneLoader._progressSlider null exception
            yield return null;

            if (SceneLoader.instance == null) {
                DiagLog.Error("[Init] SceneLoader.instance is null. Cannot load main menu scene.");
                yield break;
            }

            DiagLog.Info("[Init] Loading main menu scene...");
            yield return SceneLoader.instance.LoadSceneWithoutTransition(m_mainMenuSceneRef, SceneLoader.SceneTransitionData.MainMenu);
            DiagLog.Info("[Init] Main menu scene load coroutine finished.");
        }
    }

    public interface IInitializableSingleton {
        void Initialize();
    }
}
