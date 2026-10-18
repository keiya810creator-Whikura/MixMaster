using System;
using UnityEngine;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameMode CurrentMode { get; private set; } = GameMode.Exploration;
        public string CurrentMapId { get; private set; } = string.Empty;
        public long Gold { get; private set; }

        public event Action<GameMode> ModeChanged;
        public event Action<long> GoldChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        public void SetMode(GameMode mode)
        {
            if (CurrentMode == mode) return;
            CurrentMode = mode;
            ModeChanged?.Invoke(CurrentMode);
        }

        public void SetCurrentMap(string mapId)
        {
            CurrentMapId = mapId ?? string.Empty;
        }

        public void RestoreGold(long value)
        {
            Gold = Math.Max(0L, value);
            GoldChanged?.Invoke(Gold);
        }

        public void AddGold(long amount)
        {
            Gold = LongMath.SaturatingAdd(Gold, amount);
            if (Gold < 0) Gold = 0;
            GoldChanged?.Invoke(Gold);
        }

        public bool TrySpendGold(long amount)
        {
            return TrySpendGold(amount, true);
        }

        // Resource transactions publish only after gold, materials and the
        // equipment level have all been committed.
        internal bool TrySpendGold(long amount, bool notify)
        {
            if (amount < 0 || Gold < amount) return false;
            Gold -= amount;
            if (notify) NotifyGoldChanged();
            return true;
        }

        internal void NotifyGoldChanged() => GoldChanged?.Invoke(Gold);
    }
}
