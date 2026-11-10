using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class StageNameBannerUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI stageText;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField, Min(0f)] private float fadeInSeconds = 0.2f;
        [SerializeField, Min(0f)] private float holdSeconds = 1.8f;
        [SerializeField, Min(0f)] private float fadeOutSeconds = 0.6f;
        private Coroutine animationRoutine;

        public static void Show(string title)
        {
            if (!Application.isPlaying || string.IsNullOrWhiteSpace(title)) return;
            var banner = FindFirstObjectByType<StageNameBannerUI>();
            if (banner == null)
            {
                var prefab = Resources.Load<StageNameBannerUI>("UI/StageNameBanner");
                banner = prefab != null ? Instantiate(prefab) : CreateDefault();
            }
            banner.Initialize();
            if (banner.animationRoutine != null) banner.StopCoroutine(banner.animationRoutine);
            banner.stageText.text = title;
            banner.animationRoutine = banner.StartCoroutine(banner.Animate());
        }

        // A scene/prefab instance takes priority; its RectTransforms remain editable.
        public static StageNameBannerUI CreateDefault()
        {
            var root = new GameObject("StageNameBanner", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            var banner = root.AddComponent<StageNameBannerUI>();
            banner.Initialize();
            return banner;
        }

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            if (stageText == null)
            {
                var child = new GameObject("StageName", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                child.transform.SetParent(transform, false);
                var rect = (RectTransform)child.transform;
                rect.anchorMin = new Vector2(0.05f, 0.5f);
                rect.anchorMax = new Vector2(0.95f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(0f, 200f);
                stageText = child.GetComponent<TextMeshProUGUI>();
                stageText.font = TMP_Settings.defaultFontAsset;
                stageText.fontSize = 72f;
                stageText.fontSizeMin = 32f;
                stageText.fontSizeMax = 72f;
                stageText.enableAutoSizing = true;
                stageText.fontStyle = FontStyles.Bold;
                stageText.alignment = TextAlignmentOptions.Center;
                stageText.color = Color.white;
                stageText.raycastTarget = false;
                stageText.richText = false;
                var outline = child.AddComponent<Shadow>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
                outline.effectDistance = new Vector2(2f, -2f);
            }
            stageText.raycastTarget = false;
            if (animationRoutine == null) canvasGroup.alpha = 0f;
        }

        private IEnumerator Animate()
        {
            yield return Fade(0f, 1f, fadeInSeconds);
            float elapsed = 0f;
            while (elapsed < holdSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return Fade(1f, 0f, fadeOutSeconds);
            animationRoutine = null;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            canvasGroup.alpha = from;
            for (float elapsed = 0f; elapsed < duration;)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, duration <= 0f ? 1f : elapsed / duration);
                yield return null;
            }
            canvasGroup.alpha = to;
        }

        private void OnDisable()
        {
            if (animationRoutine != null) StopCoroutine(animationRoutine);
            animationRoutine = null;
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }
    }
}
