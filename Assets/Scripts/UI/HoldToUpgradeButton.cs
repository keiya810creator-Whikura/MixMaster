using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MixMaster.UI
{
    /// <summary>
    /// Tap = single upgrade, press and hold = accelerating repeated upgrades.
    /// A failed upgrade (insufficient material / max level) stops the repeat.
    /// Saves only once at the end of each purchase batch, not for every tick.
    /// Keyboard/controller button submission remains a single purchase.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class HoldToUpgradeButton : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Header("Press-and-hold timing (unscaled seconds)")]
        [SerializeField, Min(0.05f)] private float initialDelay = 0.35f;
        [SerializeField, Min(0.02f)] private float startingInterval = 0.14f;
        [SerializeField, Min(0.01f)] private float fastInterval = 0.045f;
        [SerializeField, Min(0.1f)] private float accelerationDuration = 1.8f;
        [SerializeField, Range(1, 30)] private int maxPurchasesPerFrame = 12;

        private Button button;
        private Func<bool> tryPurchase;
        private Action saveBatch;
        private bool pressed;
        private bool repeated;
        private bool dirty;
        private bool suppressClick;
        private int suppressClickFrame;
        private float holdTime;
        private float nextTick;

        public static HoldToUpgradeButton Attach(Button target,
            Func<bool> purchase, Action save)
        {
            if (target == null) return null;

            HoldToUpgradeButton repeat =
                target.GetComponent<HoldToUpgradeButton>();
            if (repeat == null)
                repeat = target.gameObject.AddComponent<HoldToUpgradeButton>();
            repeat.Configure(target, purchase, save);
            return repeat;
        }

        public void Configure(Button target, Func<bool> purchase, Action save)
        {
            if (button != null)
                button.onClick.RemoveListener(HandleClick);
            button = target != null ? target : GetComponent<Button>();
            tryPurchase = purchase;
            saveBatch = save;
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClick);
                button.onClick.AddListener(HandleClick);
            }
        }

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();
        }

        private void Update()
        {
            // OnPointerUp precedes Button.onClick. Keep suppression until
            // the next frame so a long hold cannot purchase once more.
            if (suppressClick && Time.frameCount > suppressClickFrame)
                suppressClick = false;

            if (!pressed) return;
            if (button == null || !button.IsActive() ||
                !button.IsInteractable() || tryPurchase == null)
            {
                StopPress();
                return;
            }

            holdTime += Time.unscaledDeltaTime;
            int purchases = 0;
            while (holdTime >= nextTick &&
                purchases < maxPurchasesPerFrame)
            {
                repeated = true;
                suppressClick = true;
                suppressClickFrame = Time.frameCount;

                if (!PurchaseOnce())
                {
                    StopPress();
                    return;
                }
                purchases++;

                float progress = Mathf.Clamp01(
                    holdTime / accelerationDuration);
                float interval = Mathf.Lerp(
                    startingInterval, fastInterval, progress);
                nextTick += interval;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null ||
                eventData.button != PointerEventData.InputButton.Left ||
                button == null || !button.IsInteractable())
                return;

            pressed = true;
            repeated = false;
            suppressClick = false;
            dirty = false;
            holdTime = 0f;
            nextTick = initialDelay;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // A failed purchase can stop the hold several frames before
            // release. Suppress the release click even in that case.
            if (repeated)
            {
                suppressClick = true;
                suppressClickFrame = Time.frameCount;
            }
            StopPress();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Cancels when sliding a finger outside the button.
            StopPress();
        }

        private void HandleClick()
        {
            if (suppressClick) return;
            if (PurchaseOnce())
                FlushSave();
        }

        private bool PurchaseOnce()
        {
            if (button == null || !button.IsInteractable() ||
                tryPurchase == null) return false;
            bool success = tryPurchase();
            if (success) dirty = true;
            return success;
        }

        private void StopPress()
        {
            if (!pressed && !dirty) return;
            pressed = false;
            if (repeated)
            {
                suppressClick = true;
                suppressClickFrame = Time.frameCount;
            }
            FlushSave();
        }

        private void FlushSave()
        {
            if (!dirty) return;
            dirty = false;
            saveBatch?.Invoke();
        }

        private void OnDisable()
        {
            StopPress();
        }

        private void OnDestroy()
        {
            StopPress();
            if (button != null)
                button.onClick.RemoveListener(HandleClick);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            initialDelay = Mathf.Max(0.05f, initialDelay);
            startingInterval = Mathf.Max(0.02f, startingInterval);
            fastInterval = Mathf.Clamp(fastInterval, 0.01f, startingInterval);
            accelerationDuration = Mathf.Max(0.1f, accelerationDuration);
            maxPurchasesPerFrame = Mathf.Clamp(maxPurchasesPerFrame, 1, 30);
        }
#endif
    }
}
