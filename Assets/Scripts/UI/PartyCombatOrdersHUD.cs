using System.Collections.Generic;
using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    /// <summary>
    /// Always-visible field HUD: a party-wide tactical override that
    /// leaves each monster's individual setting intact.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PartyCombatOrdersHUD : MonoBehaviour
    {
        private static readonly string[] Options =
        {
            "個別設定を優先",
            "敵対した敵を攻撃",
            "プレイヤーの攻撃範囲内の敵を攻撃",
            "自動狩りモード（マップ全体）",
            "攻撃しない"
        };

        [SerializeField] private TMP_Dropdown orderDropdown;
        private MonsterManager manager;

        /// <summary>
        /// Backwards-compatible runtime installation for scenes that already
        /// have the party circular HUD but have not used the editor menu yet.
        /// </summary>
        public static PartyCombatOrdersHUD EnsureOnCanvas(Canvas canvas)
        {
            if (canvas == null)
                return null;

            Canvas root = canvas.rootCanvas;
            PartyCombatOrdersHUD existing =
                root.GetComponentInChildren<PartyCombatOrdersHUD>(true);
            if (existing != null)
                return existing;

            GameObject instance = new GameObject(
                "PartyCombatOrdersHUD", typeof(RectTransform),
                typeof(Image), typeof(PartyCombatOrdersHUD));
            instance.transform.SetParent(root.transform, false);
            RectTransform rect = instance.GetComponent<RectTransform>();
            ConfigureRect(rect);
            instance.transform.SetAsLastSibling();
            return instance.GetComponent<PartyCombatOrdersHUD>();
        }

        public static void ConfigureRect(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-20f, -130f);
            rect.sizeDelta = new Vector2(435f, 112f);
        }

        public void BuildVisuals()
        {
            Image background = GetComponent<Image>();
            if (background != null)
            {
                background.color = new Color(
                    0.06f, 0.095f, 0.15f, 0.89f);
                background.raycastTarget = false;
            }

            if (orderDropdown != null)
                return;

            Transform old = transform.Find("PartyOrderDropdown");
            if (old != null)
                orderDropdown = old.GetComponent<TMP_Dropdown>();

            if (orderDropdown == null)
            {
                TMP_Text title = InventoryUiFactory.Text(
                    transform, "PartyOrdersTitle", "パーティ全体の行動",
                    new Vector2(14f, -7f), new Vector2(400f, 32f), 24f);
                title.color = new Color(1f, 0.84f, 0.52f);

                orderDropdown = InventoryUiFactory.Dropdown(
                    transform, "PartyOrderDropdown",
                    new Vector2(14f, -43f),
                    new Vector2(404f, 56f), Options);
            }

            // All five options fit within the 260px template, so clamp the
            // scroll instead of allowing scrolling past the final item.
            if (orderDropdown != null && orderDropdown.template != null)
            {
                ScrollRect scroller =
                    orderDropdown.template.GetComponent<ScrollRect>();
                if (scroller != null)
                {
                    scroller.vertical = false;
                    scroller.horizontal = false;
                    scroller.inertia = false;
                    scroller.scrollSensitivity = 0f;
                    scroller.movementType = ScrollRect.MovementType.Clamped;
                }
            }
        }

        private void Awake()
        {
            BuildVisuals();
            if (orderDropdown != null)
                orderDropdown.onValueChanged.AddListener(HandleSelection);
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            if (manager != null)
                manager.PartyCombatOrderChanged -= HandlePartyOrderChanged;
            manager = null;
        }

        private void OnDestroy()
        {
            if (orderDropdown != null)
                orderDropdown.onValueChanged.RemoveListener(HandleSelection);
        }

        private void Update()
        {
            if (manager == null)
            {
                Subscribe();
                Refresh();
            }
        }

        private void Subscribe()
        {
            if (manager != null)
                return;

            manager = FindFirstObjectByType<MonsterManager>();
            if (manager != null)
                manager.PartyCombatOrderChanged += HandlePartyOrderChanged;
        }

        private void HandlePartyOrderChanged(PartyCombatOrder order)
        {
            Refresh();
        }

        private void HandleSelection(int index)
        {
            if (manager == null)
                Subscribe();

            if (manager == null || index < 0 || index >= Options.Length)
                return;

            manager.SetPartyCombatOrder((PartyCombatOrder)index);
        }

        public void Refresh()
        {
            if (manager == null || orderDropdown == null)
                return;

            int current = (int)manager.CurrentPartyCombatOrder;
            orderDropdown.SetValueWithoutNotify(
                Mathf.Clamp(current, 0, Options.Length - 1));
        }
    }
}
