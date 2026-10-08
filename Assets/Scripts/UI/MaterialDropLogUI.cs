using System.Collections;
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
        }

        [Header("Log")]
        [SerializeField, Min(1)] private int maxLines = 6;
        [SerializeField, Min(0.5f)] private float visibleSeconds = 3.5f;
        [SerializeField, Min(0.1f)] private float fadeSeconds = 0.6f;
        [SerializeField, Min(10f)] private float fontSize = 28f;

        private readonly List<LogEntry> entries =
            new List<LogEntry>();

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

            GameObject lineObject =
                new GameObject(
                    "MaterialLog_" + material.materialId,
                    typeof(RectTransform));

            lineObject.transform.SetParent(
                contentRoot,
                false);

            TextMeshProUGUI text =
                lineObject.AddComponent<TextMeshProUGUI>();

            text.text =
                materialName +
                "×" +
                quantity;

            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment =
                TextAlignmentOptions.BottomLeft;

            text.enableWordWrapping = false;
            text.raycastTarget = false;
            text.color = GetSourceColor(sourceType);

            RectTransform rect =
                lineObject.GetComponent<RectTransform>();

            rect.sizeDelta =
                new Vector2(520f, fontSize + 12f);

            LogEntry entry =
                new LogEntry
                {
                    gameObject = lineObject,
                    text = text
                };

            entries.Add(entry);

            while (entries.Count > maxLines)
            {
                RemoveEntry(entries[0]);
            }

            StartCoroutine(
                FadeAndRemove(entry));
        }

        private IEnumerator FadeAndRemove(
            LogEntry entry)
        {
            yield return
                new WaitForSeconds(visibleSeconds);

            if (entry == null ||
                entry.text == null)
            {
                yield break;
            }

            float elapsed = 0f;
            Color startColor = entry.text.color;

            while (elapsed < fadeSeconds &&
                   entry.text != null)
            {
                elapsed += Time.deltaTime;

                float t =
                    Mathf.Clamp01(
                        elapsed / fadeSeconds);

                Color color = startColor;
                color.a = 1f - t;
                entry.text.color = color;

                yield return null;
            }

            RemoveEntry(entry);
        }

        private void RemoveEntry(LogEntry entry)
        {
            if (entry == null)
                return;

            entries.Remove(entry);

            if (entry.gameObject != null)
                Destroy(entry.gameObject);
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
                canvasObject.transform,
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

        private static Color GetSourceColor(
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
