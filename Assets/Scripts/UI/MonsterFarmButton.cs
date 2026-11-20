using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class MonsterFarmButton : MonoBehaviour
    {
        [SerializeField] private MonsterFarmPanelUI panel;
        private Button button;
        [SerializeField] private TMP_Text readyBadge;
        private MonsterManager monsters;
        private float nextRefresh;

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .5f;
            if (monsters == null) monsters = FindFirstObjectByType<MonsterManager>();
            int count = monsters != null ? monsters.FarmNotificationCount() : 0;
            if (readyBadge == null && count > 0)
            {
                readyBadge = InventoryUiFactory.Text(transform, "FarmReadyBadge", "", Vector2.zero, Vector2.zero, 22);
                var rect = readyBadge.rectTransform;
                rect.anchorMin = new Vector2(0, .7f);
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                readyBadge.alignment = TextAlignmentOptions.Center;
                readyBadge.color = new Color(1f, .85f, .2f);
                var source = GetComponentInChildren<TMP_Text>();
                if (source != null) readyBadge.font = source.font;
            }
            if (readyBadge != null)
            {
                readyBadge.text = $"育成完了 {count}";
                readyBadge.gameObject.SetActive(count > 0);
            }
        }

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(Open);
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(Open);
        }

        public void Open()
        {
            if (panel == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas == null) return;
                Canvas root = canvas.rootCanvas;
                Transform parent = SafeAreaLayout.ContentRoot(root.transform);
                panel = root.GetComponentInChildren<MonsterFarmPanelUI>(true);
                if (panel == null)
                {
                    MonsterFarmPanelUI prefab = Resources.Load<MonsterFarmPanelUI>("UI/MonsterFarmPanel");
                    if (prefab == null)
                    {
                        Debug.LogError("[MonsterFarm] ファームパネルのPrefabが見つかりません。", this);
                        return;
                    }
                    panel = Instantiate(prefab, parent, false);
                }
            }

            panel.Open();
        }
    }
}
