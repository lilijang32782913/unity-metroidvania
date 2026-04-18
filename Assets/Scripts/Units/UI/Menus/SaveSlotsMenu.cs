using Metroidvania.InputSystem;
using Metroidvania.Diagnostics;
using Metroidvania.SceneManagement;
using Metroidvania.UI;
using Metroidvania.UI.Menus;
using UnityEngine;

namespace Metroidvania.Serialization.Menus {
    public class SaveSlotsMenu : CanvasMenuBase, IMenuScreen {
        [SerializeField] private CanvasGroup m_canvasGroup;

        [SerializeField] private AssetReferenceSceneChannel m_sceneLevel0;

        public event System.Action OnMenuDisable;
        private SaveSlot[] _saveSlots;

        private void Start() {
            _saveSlots = GetComponentsInChildren<SaveSlot>();
            DiagLog.Info($"[SaveSlots] Start. slotCount={_saveSlots.Length}");

            foreach (SaveSlot saveSlot in _saveSlots) {
                GameData saveUserData = DataManager.instance.dataHandler.Deserialize(saveSlot.GetUserId());
                saveSlot.SetData(saveUserData);
                DiagLog.Info($"[SaveSlots] Slot init. userId={saveSlot.GetUserId()}, hasData={saveUserData != null}");
                saveSlot.button.onClick.AddListener(() => OnSaveSlotClick(saveSlot));
            }
        }

        public void OnSaveSlotClick(SaveSlot saveSlot) {
            DiagLog.Info($"[SaveSlots] Click slot. userId={saveSlot.GetUserId()}");
            DataManager.instance.ChangeSelectedUser(saveSlot.GetUserId());
            GameData slotData = saveSlot.GetData();
            if (slotData != null)
                ContinueGame(slotData);
            else
                NewGame(saveSlot.GetUserId());
        }

        private void NewGame(int userId) {
            // new game operations
            DiagLog.Info($"[SaveSlots] NewGame. userId={userId}, sceneLevel0Key={m_sceneLevel0.RuntimeKey}");
            if (GameDebugger.instance.debugSerialization)
                GameDebugger.Log($"Started a new game at user {userId}");

            SceneLoader.instance.LoadScene(m_sceneLevel0, SceneLoader.SceneTransitionData.UseGameData);
            InputReader.instance.EnableGameplayInput();
            DiagLog.Info("[SaveSlots] NewGame requested SceneLoader.LoadScene and enabled gameplay input.");
        }

        private void ContinueGame(GameData data) {
            // load game operations
            DiagLog.Info($"[SaveSlots] ContinueGame. userId={data.userId}");
            if (GameDebugger.instance.debugSerialization)
                GameDebugger.Log($"Continued a game {data.userId}");

            data.LoadCurrentScene();
        }

        public void ActiveMenu() {
            menuEnabled = true;
            m_canvasGroup.FadeGroup(true, UIUtility.TransitionTime, SetFirstSelected);
        }

        public void DesactiveMenu() {
            menuEnabled = false;
            m_canvasGroup.FadeGroup(false, UIUtility.TransitionTime, () => OnMenuDisable?.Invoke());
        }
    }
}