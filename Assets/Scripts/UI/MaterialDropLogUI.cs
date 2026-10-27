using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MixMaster.Core;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class MaterialDropLogUI : MonoBehaviour
    {
        private sealed class LogEntry
        {
            public GameObject gameObject;
            public TMP_Text text;
            public float expiresAt;
        }

        [Header("Log")]
        [SerializeField, Min(1)] private int maxLines = 6;
        [SerializeField, Min(0.5f)] private float visibleSeconds = 3.5f;
        [SerializeField, Min(0.1f)] private float fadeSeconds = 0.6f;
        [SerializeField, Min(10f)] private float fontSize = 28f;

        private readonly List<LogEntry> entries =
            new List<LogEntry>();
        private readonly Queue<LogEntry> available = new Queue<LogEntry>();

        private RectTransform contentRoot;

        private void Awake()
        {
            BuildUiIfNeeded();
        }

        public void AddMaterialLog(
            MaterialSO material,
            long quantity,
            MaterialDropSourceType sourceType)
        {
            if (material == null || quantity <= 0L)
                return;

            BuildUiIfNeeded();

            string materialName =
                !string.IsNullOrWhiteSpace(material.displayName)
                    ? material.displayName
                    : material.materialId;

            LogEntry entry;
            if (entries.Count >= Mathf.Max(1, maxLines))
            {
                entry = entries[0];
                entries.RemoveAt(0);
            }
            else if (available.Count > 0) entry = available.Dequeue();
            else entry = CreateEntry();
            entry.gameObject.name = "MaterialLog_" + material.materialId;
            entry.gameObject.SetActive(true);
            entry.gameObject.transform.SetAsLastSibling();
            entry.text.text = materialName + "×" + quantity;
            entry.text.color = GetSourceColor(sourceType);
            entry.text.fontSize = fontSize;
            ((RectTransform)entry.gameObject.transform).sizeDelta = new Vector2(520f, fontSize + 12f);
            entry.expiresAt = Time.time + visibleSeconds;
            entries.Add(entry);
            while (entries.Count > Mathf.Max(1, maxLines)) RemoveEntry(entries[0]);
        }

        private LogEntry CreateEntry()
        {
            GameObject lineObject =
                new GameObject(
                    "MaterialLog",
                    typeof(RectTransform));

            lineObject.transform.SetParent(
                contentRoot,
                false);

            TextMeshProUGUI text =
                lineObject.AddComponent<TextMeshProUGUI>();

            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment =
                TextAlignmentOptions.BottomLeft;

            text.enableWordWrapping = false;
            text.raycastTarget = false;

            RectTransform rect =
                lineObject.GetComponent<RectTransform>();

            rect.sizeDelta =
                new Vector2(520f, fontSize + 12f);

            return
                new LogEntry
                {
                    gameObject = lineObject,
                    text = text
                };

        }

        private void Update()
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                float elapsed = Time.time - entry.expiresAt;
                if (elapsed < 0f) continue;
                if (entry.text == null || elapsed >= Mathf.Max(.1f, fadeSeconds))
                { RemoveEntry(entry); continue; }
                Color color = entry.text.color;
                color.a = 1f - Mathf.Clamp01(elapsed / Mathf.Max(.1f, fadeSeconds));
                entry.text.color = color;
            }
        }

        private void RemoveEntry(LogEntry entry)
        {
            if (entry == null)
                return;

            entries.Remove(entry);

            if (entry.gameObject != null)
            {
                entry.gameObject.SetActive(false);
                if (available.Count < Mathf.Max(1, maxLines)) available.Enqueue(entry);
                else Destroy(entry.gameObject);
            }
        }

        private void BuildUiIfNeeded()
        {
            if (contentRoot != null)
                return;

            GameObject canvasObject =
                new GameObject(
                    "MaterialDropLogCanvas",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster));

            canvasObject.transform.SetParent(
                transform,
                false);

            Canvas canvas =
                canvasObject.GetComponent<Canvas>();

            canvas.renderMode =
                RenderMode.ScreenSpaceOverlay;

            canvas.sortingOrder = 200;

            CanvasScaler scaler =
                canvasObject.GetComponent<CanvasScaler>();

            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;

            scaler.referenceResolution =
                new Vector2(1080f, 1920f);

            scaler.matchWidthOrHeight = 0.5f;

            GameObject content =
                new GameObject(
                    "MaterialDropLog",
                    typeof(RectTransform),
                    typeof(VerticalLayoutGroup));

            content.transform.SetParent(
                SafeAreaLayout.CreateContent(canvasObject.transform),
                false);

            contentRoot =
                content.GetComponent<RectTransform>();

            contentRoot.anchorMin =
                new Vector2(0f, 0f);

            contentRoot.anchorMax =
                new Vector2(0f, 0f);

            contentRoot.pivot =
                new Vector2(0f, 0f);

            contentRoot.anchoredPosition =
                new Vector2(24f, 24f);

            contentRoot.sizeDelta =
                new Vector2(540f, 300f);

            VerticalLayoutGroup layout =
                content.GetComponent<VerticalLayoutGroup>();

            layout.childAlignment =
                TextAnchor.LowerLeft;

            layout.spacing = 2f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        public static Color GetSourceColor(
            MaterialDropSourceType sourceType)
        {
            switch (sourceType)
            {
                case MaterialDropSourceType.Party1:
                    return new Color(
                        1f,
                        0.35f,
                        0.35f,
                        1f);

                case MaterialDropSourceType.Party2:
                    return new Color(
                        0.35f,
                        0.70f,
                        1f,
                        1f);

                case MaterialDropSourceType.Party3:
                    return new Color(
                        0.35f,
                        1f,
                        0.45f,
                        1f);

                case MaterialDropSourceType.Player:
                case MaterialDropSourceType.Unknown:
                default:
                    return Color.white;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            maxLines = Mathf.Max(1, maxLines);
            visibleSeconds = Mathf.Max(0.5f, visibleSeconds);
            fadeSeconds = Mathf.Max(0.1f, fadeSeconds);
            fontSize = Mathf.Max(10f, fontSize);
        }
#endif
    }
}
