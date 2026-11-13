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

        private void Awake()
        {
            if (monsterButton != null) monsterButton.onClick.AddListener(ShowMonsters);
            if (equipmentButton != null) equipmentButton.onClick.AddListener(ShowEquipment);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
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

        public void Close()
        {
            if (monsters != null) monsters.Hide();
            if (equipment != null) equipment.Hide();
            if (selectionRoot != null) selectionRoot.SetActive(false);
            gameObject.SetActive(false);
        }
    }
}
