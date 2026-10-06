using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;

namespace MixMaster.UI
{
    /// <summary>
    /// Floating mobile joystick.
    /// Touch/click anywhere inside the touch area to spawn the joystick there.
    /// Dragging sends a Vector2 value to the configured Input System control
    /// (normally <Gamepad>/leftStick).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloatingJoystick : OnScreenControl,
        IPointerDownHandler,
        IDragHandler,
        IPointerUpHandler
    {
        [Header("Input System")]
        [InputControl(layout = "Vector2")]
        [SerializeField] private string controlPath = "<Gamepad>/leftStick";

        [Header("UI References")]
        [Tooltip("Pointer events are accepted inside this RectTransform. Usually a transparent Image covering the left half of the screen.")]
        [SerializeField] private RectTransform touchArea;

        [Tooltip("Background/base of the joystick. This object is moved to the position that was touched.")]
        [SerializeField] private RectTransform joystickRoot;

        [Tooltip("Movable knob/handle.")]
        [SerializeField] private RectTransform handle;

        [Header("Joystick")]
        [SerializeField, Min(1f)] private float movementRange = 100f;

        [Tooltip("Hide the joystick until the player touches the control area.")]
        [SerializeField] private bool hideWhenReleased = true;

        private int activePointerId = int.MinValue;
        private bool isPressed;

        protected override string controlPathInternal
        {
            get => controlPath;
            set => controlPath = value;
        }

        private void Awake()
        {
            if (touchArea == null)
                touchArea = transform as RectTransform;

            ResetJoystick();

            if (hideWhenReleased && joystickRoot != null)
                joystickRoot.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            ReleaseControl();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (isPressed)
                return;

            if (touchArea == null || joystickRoot == null || handle == null)
                return;

            activePointerId = eventData.pointerId;
            isPressed = true;

            joystickRoot.gameObject.SetActive(true);

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    touchArea,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                joystickRoot.anchoredPosition = localPoint;
            }

            handle.anchoredPosition = Vector2.zero;
            SendValueToControl(Vector2.zero);

            UpdateJoystick(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isPressed || eventData.pointerId != activePointerId)
                return;

            UpdateJoystick(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!isPressed || eventData.pointerId != activePointerId)
                return;

            ReleaseControl();
        }

        private void UpdateJoystick(PointerEventData eventData)
        {
            if (touchArea == null || joystickRoot == null || handle == null)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    touchArea,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 pointerLocalPosition))
            {
                return;
            }

            Vector2 delta = pointerLocalPosition - joystickRoot.anchoredPosition;
            Vector2 input = Vector2.ClampMagnitude(delta / movementRange, 1f);

            handle.anchoredPosition = input * movementRange;
            SendValueToControl(input);
        }

        private void ReleaseControl()
        {
            if (isPressed)
                SendValueToControl(Vector2.zero);

            isPressed = false;
            activePointerId = int.MinValue;

            ResetJoystick();

            if (hideWhenReleased && joystickRoot != null)
                joystickRoot.gameObject.SetActive(false);
        }

        private void ResetJoystick()
        {
            if (handle != null)
                handle.anchoredPosition = Vector2.zero;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            movementRange = Mathf.Max(1f, movementRange);

            if (string.IsNullOrWhiteSpace(controlPath))
                controlPath = "<Gamepad>/leftStick";
        }
#endif
    }
}
