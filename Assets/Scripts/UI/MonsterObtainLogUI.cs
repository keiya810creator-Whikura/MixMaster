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
            public float expiresAt;
        }

        [Header("Log")]
        [SerializeField, Min(1)] private int maxLines = 4;
        [SerializeField, Min(0.5f)] private float visibleSeconds = 4.5f;
        [SerializeField, Min(0.1f)] private float fadeSeconds = 0.7f;
        [SerializeField, Min(10f)] private float fontSize = 30f;

        private readonly List<LogEntry> entries =
            new List<LogEntry>();

        private readonly Queue<LogEntry> available = new Queue<LogEntry>();
        [Header("Position (from safe-area bottom-left)")]
        [SerializeField] private Vector2 logPosition = new Vector2(24f, 350f);

        private RectTransform contentRoot;
        private bool usingSceneContent;
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
                    ? "《" +
                      (!string.IsNullOrWhiteSpace(
                          title.displayName)
                          ? title.displayName
                          : title.titleId) +
                      "》"
                    : string.Empty;

            LogEntry entry;
            if (entries.Count >= Mathf.Max(1, maxLines))
            {
                entry = entries[0]; entries.RemoveAt(0);
            }
            else if (available.Count > 0) entry = available.Dequeue();
            else entry = CreateEntry();
            entry.gameObject.name = "MonsterObtainLog_" + record.uniqueId;
            entry.gameObject.SetActive(true);
            entry.gameObject.transform.SetAsLastSibling();
            entry.text.text = titlePrefix + monsterName + "が仲間になった！";
            entry.text.color = new Color(1f, .88f, .45f, 1f);
            entry.text.fontSize = fontSize;
            ((RectTransform)entry.gameObject.transform).sizeDelta = new Vector2(700f, fontSize + 16f);
            entry.expiresAt = Time.time + visibleSeconds;
            entries.Add(entry);
            while (entries.Count > Mathf.Max(1, maxLines)) RemoveEntry(entries[0]);
        }

        private LogEntry CreateEntry()
        {
            var go = new GameObject("MonsterObtainLog", typeof(RectTransform));
            go.transform.SetParent(contentRoot, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontStyle = FontStyles.Bold; text.alignment = TextAlignmentOptions.BottomLeft;
            text.enableWordWrapping = false; text.raycastTarget = false;
            return new LogEntry { gameObject = go, text = text };
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
                var color = entry.text.color;
                color.a = 1f - Mathf.Clamp01(elapsed / Mathf.Max(.1f, fadeSeconds));
                entry.text.color = color;
            }
        }

        private void RemoveEntry(LogEntry entry)
        {
            if (entry == null) return;
            entries.Remove(entry);
            if (entry.gameObject == null) return;
            entry.gameObject.SetActive(false);
            if (available.Count < Mathf.Max(1, maxLines)) available.Enqueue(entry);
            else Destroy(entry.gameObject);
        }

        public void UseSceneContent(RectTransform root)
        {
            if (root == null || contentRoot == root) return;
            Canvas oldCanvas = contentRoot != null ? contentRoot.GetComponentInParent<Canvas>() : null;
            while (entries.Count > 0) RemoveEntry(entries[entries.Count - 1]);
            while (available.Count > 0)
            {
                var entry = available.Dequeue();
                if (entry.gameObject != null) Destroy(entry.gameObject);
            }
            contentRoot = root; usingSceneContent = true;
            RuntimeUiLayoutSettings.Queue(root);
            if (oldCanvas != null && oldCanvas.transform.IsChildOf(transform)) Destroy(oldCanvas.gameObject);
        }
        public void ReleaseSceneContent(RectTransform root)
        {
            if (contentRoot != root) return;
            while (entries.Count > 0) RemoveEntry(entries[entries.Count - 1]);
            while (available.Count > 0)
            {
                var entry = available.Dequeue();
                if (entry.gameObject != null) Destroy(entry.gameObject);
            }
            contentRoot = null; usingSceneContent = false;
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
                logPosition;

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
            if (contentRoot != null && !usingSceneContent) contentRoot.anchoredPosition = logPosition;
        }
#endif
    }
}
