using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class EncyclopediaMenuUI : MonoBehaviour
    {
        public const string ResourcePath = "UI/Encyclopedia/EncyclopediaMenu";
        [SerializeField] private GameObject selectionRoot;
        [SerializeField] private Transform panelRoot;
        [SerializeField] private Button monsterButton, equipmentButton, closeButton;
        [SerializeField] private EncyclopediaPanelUI monsterPrefab, equipmentPrefab;
        private EncyclopediaPanelUI monsters, equipment;
        [SerializeField] private Button rewardsButton;
        private ProgressionPanelUI rewardPanel;

        private void Awake()
        {
            if (monsterButton != null) monsterButton.onClick.AddListener(ShowMonsters);
            if (equipmentButton != null) equipmentButton.onClick.AddListener(ShowEquipment);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (rewardsButton == null && selectionRoot != null)
                rewardsButton = InventoryUiFactory.Button(selectionRoot.transform, "CollectionRewards", "図鑑達成報酬",
                    new Vector2(60, -410), new Vector2(700, 60));
            if (rewardsButton != null)
            {
                rewardsButton.onClick.AddListener(ShowRewards);
                var source = equipmentButton != null ? equipmentButton.GetComponentInChildren<TMP_Text>(true) : null;
                if (source != null) rewardsButton.GetComponentInChildren<TMP_Text>(true).font = source.font;
            }
            if (selectionRoot != null) selectionRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (monsterButton != null) monsterButton.onClick.RemoveListener(ShowMonsters);
            if (equipmentButton != null) equipmentButton.onClick.RemoveListener(ShowEquipment);
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        }

        private void Start()
        {
            // A manually placed prefab starts closed; a button may have
            // already opened it in the same frame, so preserve that state.
            if (selectionRoot != null && !selectionRoot.activeSelf && monsters == null && equipment == null)
                gameObject.SetActive(false);
        }

        public static EncyclopediaMenuUI OpenDefault(Transform parent)
        {
            var menu = FindFirstObjectByType<EncyclopediaMenuUI>(FindObjectsInactive.Include);
            if (menu == null)
            {
                var prefab = Resources.Load<GameObject>(ResourcePath);
                if (prefab == null || parent == null)
                {
                    Debug.LogWarning("[Encyclopedia] 図鑑PrefabまたはCanvasが見つかりません。");
                    return null;
                }
                menu = Instantiate(prefab, parent, false).GetComponent<EncyclopediaMenuUI>();
            }
            if (menu != null) menu.Open();
            return menu;
        }

        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ShowSelection();
        }

        private void ShowSelection()
        {
            if (monsters != null) monsters.Hide();
            if (equipment != null) equipment.Hide();
            if (selectionRoot != null) { selectionRoot.SetActive(true); selectionRoot.transform.SetAsLastSibling(); }
        }

        public void ShowMonsters()
        {
            if (monsters == null) monsters = FindPlacedPanel(EncyclopediaKind.Monster);
            if (monsters == null && monsterPrefab != null)
                monsters = Instantiate(monsterPrefab, panelRoot != null ? panelRoot : transform, false);
            ShowPanel(monsters);
        }

        public void ShowEquipment()
        {
            if (equipment == null) equipment = FindPlacedPanel(EncyclopediaKind.Equipment);
            if (equipment == null && equipmentPrefab != null)
                equipment = Instantiate(equipmentPrefab, panelRoot != null ? panelRoot : transform, false);
            ShowPanel(equipment);
        }

        private static EncyclopediaPanelUI FindPlacedPanel(EncyclopediaKind kind)
        {
            foreach (var panel in FindObjectsByType<EncyclopediaPanelUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (panel.Kind == kind) return panel;
            return null;
        }

        private void ShowPanel(EncyclopediaPanelUI panel)
        {
            if (panel == null) return;
            if (monsters != null && monsters != panel) monsters.Hide();
            if (equipment != null && equipment != panel) equipment.Hide();
            if (selectionRoot != null) selectionRoot.SetActive(false);
            panel.Open(ShowSelection);
        }

        private void ShowRewards()
        {
            if (rewardPanel == null) rewardPanel = ProgressionPanelUI.Create(transform, "CollectionRewards");
            var manager = MixMaster.Core.EncyclopediaManager.Instance;
            var rows = new System.Collections.Generic.List<ProgressionPanelUI.Item>();
            if (manager != null)
                foreach (MixMaster.Core.CollectionGroup group in System.Enum.GetValues(typeof(MixMaster.Core.CollectionGroup)))
                {
                    manager.CollectionCount(group, out int owned, out int total);
                    foreach (int percent in MixMaster.Core.CollectionRewards.Milestones)
                    {
                        bool claimed = manager.RewardClaimed(group, percent);
                        bool ready = MixMaster.Core.CollectionRewards.Reached(owned, total, percent) && !claimed;
                        var currentGroup = group;
                        int milestone = percent;
                        rows.Add(new ProgressionPanelUI.Item(
                            $"{MixMaster.Core.CollectionRewards.Name(group)}　獲得 {owned}/{total}種　目標 {percent}%\n" +
                            $"報酬 {manager.CollectionRewardGold(percent):N0} G　" + (claimed ? "受取済み" : ready ? "タップで受取" : "未達成"),
                            ready ? () => { manager.ClaimCollectionReward(currentGroup, milestone); ShowRewards(); } : (System.Action)null));
                    }
                }
            rewardPanel.Show("図鑑達成報酬", rows, "獲得履歴を集計します。各報酬は1回ずつ受け取れます。", keepPage: true);
        }

        public void Close()
        {
            if (rewardPanel != null) rewardPanel.Close();
            if (monsters != null) monsters.Hide();
            if (equipment != null) equipment.Hide();
            if (selectionRoot != null) selectionRoot.SetActive(false);
            gameObject.SetActive(false);
        }
    }
}
