using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    /// <summary>
    /// Runtime battle HUD that displays the saved GameManager gold value.
    /// Add it once per screen Canvas; existing party HUDs create it for you.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GoldCounterHUD : MonoBehaviour
    {
        private TMP_Text valueText;
        private GameManager manager;
        private float retryAt;

        public static GoldCounterHUD EnsureOnCanvas(Canvas canvas)
        {
            if (canvas == null) return null;
            canvas = canvas.rootCanvas;
            GoldCounterHUD found =
                canvas.GetComponentInChildren<GoldCounterHUD>(true);
            if (found != null) return found;

            GameObject root = new GameObject("GoldCounterHUD",
                typeof(RectTransform), typeof(Image), typeof(GoldCounterHUD));
            root.transform.SetParent(canvas.transform, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-20f, -253f);
            rect.sizeDelta = new Vector2(340f, 55f);
            return root.GetComponent<GoldCounterHUD>();
        }

        private void Awake()
        {
            Image bg = GetComponent<Image>();
            if (bg != null)
            {
                bg.color = new Color(0.10f, 0.12f, 0.17f, 0.86f);
                bg.raycastTarget = false;
            }

            valueText = GetComponentInChildren<TMP_Text>();
            if (valueText == null)
            {
                valueText = InventoryUiFactory.Text(
                    transform, "GoldValue", "Gold: 0 G",
                    Vector2.zero, new Vector2(340f, 55f), 25f);
                InventoryUiFactory.Stretch(valueText.rectTransform);
                valueText.alignment = TextAlignmentOptions.Center;
                valueText.color = new Color(1f, 0.83f, 0.45f);
            }
        }

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void OnDisable()
        {
            if (manager != null)
                manager.GoldChanged -= OnGoldChanged;
            manager = null;
        }

        private void Update()
        {
            if (manager == null && Time.unscaledTime >= retryAt)
            {
                retryAt = Time.unscaledTime + 0.5f;
                TrySubscribe();
            }
        }

        private void TrySubscribe()
        {
            if (manager != null) return;
            manager = FindFirstObjectByType<GameManager>();
            if (manager == null) return;
            manager.GoldChanged -= OnGoldChanged;
            manager.GoldChanged += OnGoldChanged;
            OnGoldChanged(manager.Gold);
        }

        private void OnGoldChanged(long value)
        {
            if (valueText != null)
                valueText.text = "Gold: " + value.ToString("N0") + " G";
        }
    }
}
