using System;
using UnityEngine;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    public sealed class DisplaySettingsManager : MonoBehaviour
    {
        public static DisplaySettingsManager Instance { get; private set; }
        private DisplaySettingsData data = new DisplaySettingsData();
        private SaveManager saves;
        private bool saving;
        private float retryAt;

        public bool SavePending { get; private set; }
        public event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake() { if (Instance == null) Instance = this; }
        private void Start() => BindSave();
        private void OnDestroy()
        {
            if (saves != null) saves.Saved -= HandleSaved;
            if (Instance == this) Instance = null;
        }

        // Hot paths only read the singleton; never search the scene per hit.
        public static bool IsDisabled(DisplayOption option) =>
            Instance != null && Instance.data.IsDisabled(option);

        public bool SetDisabled(DisplayOption option, bool disabled)
        {
            if (!data.SetDisabled(option, disabled)) return false;
            SavePending = true;
            TrySave();
            Changed?.Invoke();
            return true;
        }

        public DisplaySettingsData Capture() => data.Copy();

        public void Restore(DisplaySettingsData saved)
        {
            data = saved != null ? saved.Copy() : new DisplaySettingsData();
            SavePending = false;
            Changed?.Invoke();
        }

        private void BindSave()
        {
            if (saves != null) return;
            saves = GetComponent<SaveManager>();
            if (saves != null) saves.Saved += HandleSaved;
        }

        private void TrySave()
        {
            BindSave();
            saving = true;
            try { SavePending = saves == null || !saves.SaveGame(); }
            finally { saving = false; }
            retryAt = Time.unscaledTime + 1f;
        }

        private void HandleSaved()
        {
            if (saving || !SavePending) return;
            SavePending = false;
            Changed?.Invoke();
        }

        private void Update()
        {
            if (!SavePending || Time.unscaledTime < retryAt) return;
            TrySave();
            if (!SavePending) Changed?.Invoke();
        }
    }
}
