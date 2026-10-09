using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MixMaster.Core;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class MonsterObtainLogUI : MonoBehaviour
    {
        private sealed class LogEntry
        {
            public GameObject gameObject;
            public TMP_Text text;
        }

        [Header("Log")]
        [SerializeField, Min(1)] private int maxLines = 4;
        [SerializeField, Min(0.5f)] private float visibleSeconds = 4.5f;
        [SerializeField, Min(0.1f)] private float fadeSeconds = 0.7f;
        [SerializeField, Min(10f)] private float fontSize = 30f;

        private readonly List<LogEntry> entries =
            new List<LogEntry>();

        private RectTransform contentRoot;
        private MonsterManager monsterManager;
        private bool subscribed;

        private void Awake()
        {
            BuildUiIfNeeded();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void Start()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (subscribed)
                return;

            if (monsterManager == null)
            {
                monsterManager =
                    FindFirstObjectByType<MonsterManager>();
            }

            if (monsterManager == null)
                return;

            monsterManager.MonsterObtained +=
                HandleMonsterObtained;

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed ||
                monsterManager == null)
            {
                return;
            }

            monsterManager.MonsterObtained -=
                HandleMonsterObtained;

            subscribed = false;
        }

        private void HandleMonsterObtained(
            OwnedMonsterRecord record)
        {
            if (record == null)
                return;

            BuildUiIfNeeded();

            MonsterCatalogSO catalog =
                MonsterCatalogSO.Load();

            MonsterSO monster =
                catalog != null
                    ? catalog.GetMonster(record.monsterId)
                    : null;

            TitleSO title =
                catalog != null
                    ? catalog.GetTitle(record.titleId)
                    : null;

            string monsterName =
                monster != null &&
                !string.IsNullOrWhiteSpace(
                    monster.displayName)
                    ? monster.displayName
                    : record.monsterId;

            string titlePrefix =
                title != null
                    ? "【" +
                      (!string.IsNullOrWhiteSpace(
                          title.displayName)
                          ? title.displayName
                          : title.titleId) +
                      "】"
                    : string.Empty;

            GameObject lineObject =
                new GameObject(
                    "MonsterObtainLog_" +
                    record.uniqueId,
                    typeof(RectTransform));

            lineObject.transform.SetParent(
                contentRoot,
                false);

            TextMeshProUGUI text =
                lineObject.AddComponent<TextMeshProUGUI>();

            text.text =
                titlePrefix +
                monsterName +
                "が仲間になった！";

            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment =
                TextAlignmentOptions.BottomLeft;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            text.color =
                new Color(
                    1f,
                    0.88f,
                    0.45f,
                    1f);

            RectTransform rect =
                lineObject.GetComponent<RectTransform>();

            rect.sizeDelta =
                new Vector2(
                    700f,
                    fontSize + 16f);

            LogEntry entry =
                new LogEntry
                {
                    gameObject = lineObject,
                    text = text
                };

            entries.Add(entry);

            while (entries.Count > maxLines)
                RemoveEntry(entries[0]);

            StartCoroutine(
                FadeAndRemove(entry));
        }

        private IEnumerator FadeAndRemove(
            LogEntry entry)
        {
            yield return
                new WaitForSeconds(
                    visibleSeconds);

            if (entry == null ||
                entry.text == null)
            {
                yield break;
            }

            float elapsed = 0f;
            Color startColor =
                entry.text.color;

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

        private void RemoveEntry(
            LogEntry entry)
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
                    "MonsterObtainLogCanvas",
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

            canvas.sortingOrder = 210;

            CanvasScaler scaler =
                canvasObject.GetComponent<CanvasScaler>();

            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;

            scaler.referenceResolution =
                new Vector2(
                    1080f,
                    1920f);

            scaler.matchWidthOrHeight = 0.5f;

            GameObject content =
                new GameObject(
                    "MonsterObtainLog",
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
                new Vector2(
                    24f,
                    350f);

            contentRoot.sizeDelta =
                new Vector2(
                    720f,
                    240f);

            VerticalLayoutGroup layout =
                content.GetComponent<VerticalLayoutGroup>();

            layout.childAlignment =
                TextAnchor.LowerLeft;

            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
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
