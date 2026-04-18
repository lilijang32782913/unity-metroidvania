using DG.Tweening;
using Metroidvania.Serialization;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.UI;

namespace Metroidvania.SceneManagement {
    using Metroidvania.Diagnostics;

    public class SceneLoader : SingletonPersistent<SceneLoader> {
        public struct SceneTransitionData {
            public const string UseGameDataKey = "k_UseGameData";
            public static readonly SceneTransitionData UseGameData = FromSpawnPoint(UseGameDataKey);

            public const string MainMenuGameKey = "k_MainMenu";
            public static readonly SceneTransitionData MainMenu = FromSpawnPoint(MainMenuGameKey);

            public const string GameOverKey = "k_GameOver";
            public static SceneTransitionData GameOver = FromSpawnPoint(GameOverKey);

#if UNITY_EDITOR
            public const string EditorInitializationKey = "k_EditorInitialization";
            public static readonly SceneTransitionData EditorInitialization = FromSpawnPoint(EditorInitializationKey);
#endif

            public string spawnPoint;
            public GameData gameData;
            public SceneChannel currentScene;

            public static SceneTransitionData FromSpawnPoint(string spawnPoint) {
                return new SceneTransitionData() { spawnPoint = spawnPoint };
            }
        }

        public struct SceneUnloadData {
            public GameData gameData;
            public SceneChannel currentScene;
            public SceneChannel nextScene;
        }

        [SerializeField] private AssetReferenceSceneChannel m_mainMenuRef;

        [Header("Transition")]
        [SerializeField] private GameObject m_loadScreenPrefab;

        [Header("Events")]
        [SerializeField] private SceneEventChannel m_sceneLoaded;
        [SerializeField] private SceneEventChannel m_beforeSceneUnload;

        private GameObject _loadScreenObj;
        private Slider _progressSlider;

        public SceneChannel activeScene { get; private set; }
        private AsyncOperationHandle<SceneChannel> _sceneChannelAssetHandle;

        private List<ISceneTransistor> _sceneTransistors = new List<ISceneTransistor>();

        private void Start() {
            DiagLog.Info("[SceneLoader] Start.");

            if (FadeScreen.instance == null) {
                DiagLog.Error("[SceneLoader] FadeScreen.instance is null. Load screen cannot be created.");
                return;
            }

            if (m_loadScreenPrefab == null) {
                DiagLog.Error("[SceneLoader] m_loadScreenPrefab is null.");
                return;
            }

            _loadScreenObj = Instantiate(m_loadScreenPrefab, FadeScreen.instance.canvas.transform);
            _progressSlider = _loadScreenObj.GetComponentInChildren<Slider>();
            _loadScreenObj.SetActive(false);

            DiagLog.Info($"[SceneLoader] Load screen ready. sliderFound={_progressSlider != null}");
        }

        private void OnApplicationQuit() {
            OnUnloadScene(null);
        }

        public void LoadMainMenu() {
            DiagLog.Info("[SceneLoader] LoadMainMenu called.");
            LoadScene(m_mainMenuRef, SceneTransitionData.MainMenu);
        }

        public Coroutine LoadScene(AssetReferenceSceneChannel channelRef, SceneTransitionData transitionData) {
            DiagLog.Info($"[SceneLoader] LoadScene called. key={channelRef.RuntimeKey}");
            return StartCoroutine(DOSceneLoadWithTransition(channelRef, transitionData));
        }

        public Coroutine LoadSceneWithoutTransition(AssetReferenceSceneChannel channelRef, SceneTransitionData transitionData) {
            DiagLog.Info($"[SceneLoader] LoadSceneWithoutTransition called. key={channelRef.RuntimeKey}");
            return StartCoroutine(DoSceneLoad(LoadChannel(channelRef), transitionData));
        }

        private IEnumerator DOSceneLoadWithTransition(AssetReferenceSceneChannel channelRef, SceneTransitionData transitionData) {
            if (_progressSlider == null || _loadScreenObj == null) {
                DiagLog.Warning("[SceneLoader] Transition UI is not ready. Falling back to non-UI scene load flow.");
                yield return DoSceneLoad(LoadChannel(channelRef), transitionData);
                yield break;
            }

            _progressSlider.value = 0;
            _loadScreenObj.SetActive(true);
            yield return FadeScreen.instance.DOFadeIn().WaitForCompletion();
            yield return DoSceneLoad(LoadChannel(channelRef), transitionData);
            yield return FadeScreen.instance.DOFadeOut().WaitForCompletion();
            _loadScreenObj.SetActive(false);
        }

        private IEnumerator DoSceneLoad(SceneChannel scene, SceneTransitionData transitionData) {
            if (scene == null) {
                DiagLog.Error("[SceneLoader] SceneChannel is null.");
                yield break;
            }

            DiagLog.Info($"[SceneLoader] Begin DoSceneLoad. targetSceneType={scene.sceneType}");

            if (activeScene?.operation.IsValid() == true)
                OnUnloadScene(scene);

            if (_sceneChannelAssetHandle.IsValid())
                Addressables.ReleaseInstance(_sceneChannelAssetHandle);

            activeScene = scene;
            transitionData.currentScene = activeScene;
            transitionData.gameData = DataManager.instance.gameData;

            Stopwatch loadSceneCallWatch = Stopwatch.StartNew();
            DiagLog.Info($"[SceneLoader] Calling LoadSceneAsync. sceneType={scene.sceneType}, runtimeKey={scene.sceneReference.RuntimeKey}");
            AsyncOperationHandle<SceneInstance> handle = scene.sceneReference.LoadSceneAsync();
            loadSceneCallWatch.Stop();
            DiagLog.Info($"[SceneLoader] LoadSceneAsync returned. elapsedMs={loadSceneCallWatch.ElapsedMilliseconds}, isDone={handle.IsDone}, status={handle.Status}, percent={handle.PercentComplete:0.000}");
            scene.operation = handle;

            handle.Completed += (op) => {
                DiagLog.Info($"[SceneLoader] Scene load completed. status={op.Status}, exception={op.OperationException}");

                if (op.Status != AsyncOperationStatus.Succeeded) {
                    DiagLog.Error($"[SceneLoader] Scene load failed. exception={op.OperationException}");
                    return;
                }

                foreach (GameObject root in op.Result.Scene.GetRootGameObjects())
                    _sceneTransistors.AddRange(root.GetComponentsInChildren<ISceneTransistor>(true));

                _sceneTransistors.ForEach(sceneTransistor => sceneTransistor.OnSceneTransition(transitionData));

                m_sceneLoaded?.Raise(scene);
            };

            float elapsed = 0f;
            float stalledTime = 0f;
            float lastProgress = -1f;

            while (!handle.IsDone) {
                if (_progressSlider != null)
                    _progressSlider.normalizedValue = handle.PercentComplete;

                if (Mathf.Abs(handle.PercentComplete - lastProgress) < 0.0001f)
                    stalledTime += Time.unscaledDeltaTime;
                else {
                    stalledTime = 0f;
                    lastProgress = handle.PercentComplete;
                }

                elapsed += Time.unscaledDeltaTime;
                if (elapsed > 2f) {
                    elapsed = 0f;
                    DiagLog.Info($"[SceneLoader] Loading... progress={handle.PercentComplete:0.000}, status={handle.Status}, stalled={stalledTime:0.00}s");
                }

                if (stalledTime > 15f) {
                    DiagLog.Error($"[SceneLoader] Loading appears stalled. progress={handle.PercentComplete:0.000}, status={handle.Status}");
                    DiagLog.SaveErrorSnapshot("SceneLoader stalled >15s", 260);
                    stalledTime = 0f;
                }

                yield return null;
            }

            DiagLog.Info("[SceneLoader] DoSceneLoad finished.");
        }

        private SceneChannel LoadChannel(AssetReferenceSceneChannel reference) {
            if (reference == null) {
                DiagLog.Error("[SceneLoader] AssetReferenceSceneChannel is null.");
                return null;
            }

            DiagLog.Info($"[SceneLoader] Loading SceneChannel asset. key={reference.RuntimeKey}");
            _sceneChannelAssetHandle = reference.LoadAssetAsync();
            SceneChannel loadedChannel = _sceneChannelAssetHandle.WaitForCompletion();

            if (_sceneChannelAssetHandle.Status != AsyncOperationStatus.Succeeded || loadedChannel == null) {
                DiagLog.Error($"[SceneLoader] Failed to load SceneChannel. status={_sceneChannelAssetHandle.Status}, exception={_sceneChannelAssetHandle.OperationException}");
            }
            else {
                DiagLog.Info($"[SceneLoader] SceneChannel loaded. sceneType={loadedChannel.sceneType}");
            }

            return loadedChannel;
        }

        private void OnUnloadScene(SceneChannel nextScene) {
            SceneUnloadData unloadData = new SceneUnloadData() {
                gameData = DataManager.instance.gameData,
                currentScene = activeScene,
                nextScene = nextScene,
            };
            _sceneTransistors.ForEach(sceneTransistor => sceneTransistor.BeforeUnload(unloadData));

            m_beforeSceneUnload?.Raise(activeScene);

            _sceneTransistors.Clear();
        }
    }
}