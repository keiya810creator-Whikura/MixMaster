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
