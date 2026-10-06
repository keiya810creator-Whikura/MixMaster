using System;
using UnityEngine;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    public sealed class PlayerManager : MonoBehaviour
    {
        [SerializeField] private CharacterStats stats = new CharacterStats();

        public CharacterStats Stats => stats;
        public long CurrentHp { get; private set; }
        public double DropRateBonus { get; private set; }

        public event Action<long, long> HpChanged;

        private void Awake()
        {
            CurrentHp = stats.maxHp;
        }

        public void RestoreFullHp()
        {
            CurrentHp = stats.maxHp;
            HpChanged?.Invoke(CurrentHp, stats.maxHp);
        }

        public void TakeDamage(long amount)
        {
            if (amount <= 0) return;
            CurrentHp = Math.Max(0L, CurrentHp - amount);
            HpChanged?.Invoke(CurrentHp, stats.maxHp);
        }

        public void Heal(long amount)
        {
            if (amount <= 0 || CurrentHp >= stats.maxHp) return;
            CurrentHp = Math.Min(stats.maxHp, LongMath.SaturatingAdd(CurrentHp, amount));
            HpChanged?.Invoke(CurrentHp, stats.maxHp);
        }

        public void SetDropRateBonus(double bonus)
        {
            DropRateBonus = Math.Max(0d, bonus);
        }
    }
}