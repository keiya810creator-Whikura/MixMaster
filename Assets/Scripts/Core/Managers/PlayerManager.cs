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
        public long CurrentMp { get; private set; }
        public float DropRateBonus => Mathf.Max(0f, stats.dropRateBonus);

        public event Action<long, long> HpChanged;
        public event Action<long, long> MpChanged;

        private void Awake()
        {
            CurrentHp = Math.Max(1L, stats.maxHp);
            CurrentMp = Math.Max(0L, stats.maxMp);
        }

        public void RestoreFullHp()
        {
            CurrentHp = Math.Max(1L, stats.maxHp);
            HpChanged?.Invoke(CurrentHp, stats.maxHp);
        }

        public void RestoreFullMp()
        {
            CurrentMp = Math.Max(0L, stats.maxMp);
            MpChanged?.Invoke(CurrentMp, stats.maxMp);
        }

        public void RestoreFullResources()
        {
            RestoreFullHp();
            RestoreFullMp();
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

            CurrentHp = Math.Min(
                stats.maxHp,
                LongMath.SaturatingAdd(CurrentHp, amount));

            HpChanged?.Invoke(CurrentHp, stats.maxHp);
        }

        public bool TrySpendMp(long amount)
        {
            if (amount <= 0)
                return true;

            if (CurrentMp < amount)
                return false;

            CurrentMp -= amount;
            MpChanged?.Invoke(CurrentMp, stats.maxMp);
            return true;
        }

        public void RecoverMp(long amount)
        {
            if (amount <= 0 || CurrentMp >= stats.maxMp)
                return;

            CurrentMp = Math.Min(
                stats.maxMp,
                LongMath.SaturatingAdd(CurrentMp, amount));

            MpChanged?.Invoke(CurrentMp, stats.maxMp);
        }

        public void SetDropRateBonus(float bonus)
        {
            stats.dropRateBonus = Mathf.Max(0f, bonus);
        }
    }
}
