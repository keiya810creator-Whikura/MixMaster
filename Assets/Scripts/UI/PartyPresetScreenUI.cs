using System;
using System.Collections.Generic;
using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    public sealed class PartyPresetScreenUI : MonoBehaviour
    {
        public const string ResourcePath = "UI/PartyPresets/PartyPresetPanel";
        [SerializeField] private RectTransform listContent;
        [SerializeField] private PartyPresetRowUI rowPrefab;
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private TMP_Text countText, selectedText, summaryText, messageText, saveLabel;
        [SerializeField] private ScrollRect summaryScroll;
        [SerializeField] private Button closeButton, saveButton, applyButton, renameButton, deleteButton;
        [Header("Confirmation")]
        [SerializeField] private GameObject confirmationRoot;
        [SerializeField] private TMP_Text confirmationTitle, confirmationText;
        [SerializeField] private Button confirmButton, cancelButton;
        private readonly List<PartyPresetRowUI> rows = new List<PartyPresetRowUI>();
        private PartyPresetManager manager;
        private MonsterManager monsters;
        private EquipmentManager equipment;
        private SkillProgressionManager skills;
        private MonsterCatalogSO catalog;
        private int selectedSlot, confirmationRevision, rowsRevision = -1;
        private Action confirmedAction;
        private bool opened, dirty;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (saveButton != null) saveButton.onClick.AddListener(Save);
            if (applyButton != null) applyButton.onClick.AddListener(Apply);
            if (renameButton != null) renameButton.onClick.AddListener(Rename);
            if (deleteButton != null) deleteButton.onClick.AddListener(Delete);
            if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
            if (cancelButton != null) cancelButton.onClick.AddListener(CancelConfirmation);
            CancelConfirmation();
        }
        private void Start() { if (!opened) gameObject.SetActive(false); }
        private void LateUpdate() { if (opened && dirty) { dirty = false; Refresh(false); } }
        private void OnDisable() { Unsubscribe(); CancelConfirmation(); opened = false; }

        public static PartyPresetScreenUI OpenDefault(Transform parent)
        {
            var screen = FindFirstObjectByType<PartyPresetScreenUI>(FindObjectsInactive.Include);
            if (screen == null)
            {
                var prefab = Resources.Load<GameObject>(ResourcePath);
                if (prefab == null || parent == null) { Debug.LogWarning("[PartyPresets] プリセットPrefabまたはCanvasが見つかりません。"); return null; }
                screen = Instantiate(prefab, parent, false).GetComponent<PartyPresetScreenUI>();
            }
            if (screen != null) screen.Open();
            return screen;
        }

        public void Open()
        {
            gameObject.SetActive(true); transform.SetAsLastSibling(); opened = true;
            Unsubscribe(); manager = PartyPresetManager.Instance;
            if (manager != null)
            {
                manager.Changed += MarkDirty;
                monsters = manager.GetComponent<MonsterManager>(); equipment = manager.GetComponent<EquipmentManager>(); skills = manager.GetComponent<SkillProgressionManager>();
                if (monsters != null)
                {
                    monsters.PartyChanged += MarkDirty; monsters.MonsterSold += MonsterSold;
                    monsters.MonstersBulkSold += MarkDirty; monsters.MonsterLeveledUp += MonsterChanged;
                }
                if (equipment != null) equipment.InventoryChanged += MarkDirty;
                if (skills != null) skills.Changed += MarkDirty;
            }
            catalog = MonsterCatalogSO.Load();
            CancelConfirmation(); SelectSlot(Mathf.Clamp(selectedSlot, 0, PartyPresetData.MaxPresets - 1));
        }

        private void Unsubscribe()
        {
            if (manager != null) manager.Changed -= MarkDirty;
            if (monsters != null)
            {
                monsters.PartyChanged -= MarkDirty; monsters.MonsterSold -= MonsterSold;
                monsters.MonstersBulkSold -= MarkDirty; monsters.MonsterLeveledUp -= MonsterChanged;
            }
            if (equipment != null) equipment.InventoryChanged -= MarkDirty;
            if (skills != null) skills.Changed -= MarkDirty;
            manager = null; monsters = null; equipment = null; skills = null;
        }
        private void MarkDirty() => dirty = true;
        private void MonsterSold(string _) => MarkDirty();
        private void MonsterChanged(OwnedMonsterRecord _) => MarkDirty();

        private void SelectSlot(int slot)
        {
            if (slot < 0 || slot >= PartyPresetData.MaxPresets) return;
            selectedSlot = slot; CancelConfirmation(); SetMessage(string.Empty); Refresh(true);
            if (summaryScroll != null) { summaryScroll.StopMovement(); summaryScroll.verticalNormalizedPosition = 1f; }
        }

        private void Refresh(bool resetName)
        {
            bool ready = manager != null;
            if (rowPrefab != null && listContent != null)
            {
                while (rows.Count < PartyPresetData.MaxPresets) rows.Add(Instantiate(rowPrefab, listContent, false));
                if (resetName || rowsRevision != (ready ? manager.Revision : -1))
                {
                    for (int i = 0; i < rows.Count; i++) rows[i].Bind(i, ready ? manager.GetPreset(i) : null, i == selectedSlot, catalog, SelectSlot);
                    rowsRevision = ready ? manager.Revision : -1;
                }
            }
            PartyPresetRecord preset = ready ? manager.GetPreset(selectedSlot) : null;
            if (countText != null) countText.text = "保存済み " + (ready ? manager.Count : 0) + " / " + PartyPresetData.MaxPresets;
            if (selectedText != null) selectedText.text = "選択中：" + (selectedSlot + 1).ToString("00") + "  " + (preset != null ? preset.name : "未保存");
            if (nameInput != null && resetName) nameInput.SetTextWithoutNotify(preset != null ? preset.name : PartyPresetData.DefaultName(selectedSlot));
            if (summaryText != null) summaryText.text = PartyPresetContent.Summary(preset, monsters, equipment, skills);
            if (saveLabel != null) saveLabel.text = preset == null ? "保存" : "上書き保存";
            if (saveButton != null) saveButton.interactable = ready;
            if (renameButton != null) renameButton.interactable = ready && preset != null;
            if (deleteButton != null) deleteButton.interactable = ready && preset != null;
            string reason = string.Empty;
            bool canApply = ready && preset != null && manager.CanApply(selectedSlot, out reason);
            if (applyButton != null) applyButton.interactable = canApply;
            if (!ready) SetMessage("パーティ設定を読み込めません。");
            else if (preset != null && !canApply) SetMessage(reason);
            if (resetName) Canvas.ForceUpdateCanvases();
        }

        private void Save()
        {
            if (manager == null) return;
            int slot = selectedSlot; string name = nameInput != null ? nameInput.text : string.Empty;
            Action action = () => { manager.SavePreset(slot, name, true, out string message); Refresh(true); SetMessage(message); };
            if (manager.IsSaved(slot)) Ask("上書き保存", "「" + manager.GetPreset(slot).name + "」を現在のパーティで上書きします。", action);
            else { manager.SavePreset(slot, name, false, out string message); Refresh(true); SetMessage(message); }
        }

        private void Apply()
        {
            if (manager == null) return;
            int slot = selectedSlot; var preset = manager.GetPreset(slot); if (preset == null) return;
            Ask("プリセットを呼び出す", "「" + preset.name + "」へ切り替えます。\nプレイヤーと仲間のスキル・装備も変更します。\n保存した装備を別の個体が使用中の場合は付け替えます。",
                () => { manager.ApplyPreset(slot, out string message); Refresh(false); SetMessage(message); });
        }

        private void Rename()
        {
            if (manager == null) return;
            manager.RenamePreset(selectedSlot, nameInput != null ? nameInput.text : string.Empty, out string message);
            Refresh(true); SetMessage(message);
        }

        private void Delete()
        {
            if (manager == null) return;
            int slot = selectedSlot; var preset = manager.GetPreset(slot); if (preset == null) return;
            Ask("プリセットを削除", "「" + preset.name + "」を削除します。\n所持モンスターや装備はそのまま残ります。",
                () => { manager.DeletePreset(slot, out string message); Refresh(true); SetMessage(message); });
        }

        private void Ask(string title, string text, Action action)
        {
            if (confirmationRoot == null) return;
            confirmationRevision = manager.Revision; confirmedAction = action;
            if (confirmationTitle != null) confirmationTitle.text = title;
            if (confirmationText != null) confirmationText.text = text;
            confirmationRoot.SetActive(true); confirmationRoot.transform.SetAsLastSibling();
        }
        private void Confirm()
        {
            Action action = confirmedAction; int revision = confirmationRevision; CancelConfirmation();
            if (manager == null || revision != manager.Revision) { SetMessage("プリセットが更新されました。選び直してください。"); Refresh(true); return; }
            action?.Invoke();
        }
        private void CancelConfirmation() { confirmedAction = null; if (confirmationRoot != null) confirmationRoot.SetActive(false); }
        private void SetMessage(string text) { if (messageText != null) messageText.text = text; }
        public void Close() { CancelConfirmation(); opened = false; gameObject.SetActive(false); }
    }
}
