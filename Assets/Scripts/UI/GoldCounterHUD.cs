using MixMaster.Core;
using TMPro;
using UnityEngine;

namespace MixMaster.UI
{
    /// <summary>
    /// Displays the saved GameManager gold value using scene/prefab visuals.
    /// Layout, background and text appearance are authored in the Editor.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GoldCounterHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private string prefix = "Gold: ";
        [SerializeField] private string suffix = " G";
        private GameManager manager;
        private float retryAt;

        private void Reset()
        {
            valueText = GetComponentInChildren<TMP_Text>(true);
        }

        private void Awake()
        {
            if (valueText == null)
                valueText = GetComponentInChildren<TMP_Text>(true);
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
                valueText.text = prefix + value.ToString("N0") + suffix;
        }
    }
}
