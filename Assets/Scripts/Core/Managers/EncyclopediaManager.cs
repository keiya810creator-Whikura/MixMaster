using System;
using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    public sealed class EncyclopediaManager : MonoBehaviour
    {
        public static EncyclopediaManager Instance { get; private set; }
        private readonly HashSet<string> defeated =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [SerializeField, Min(1)] private long collectionGoldPerPercent = 1000;
        private readonly HashSet<string> claimedRewards = new HashSet<string>(StringComparer.Ordinal);
        private bool rewardSavePending;
        private float rewardSaveAt;
        private bool claimingReward;
        public event Action Changed;

        public long CollectionRewardGold(int percent) => LongMath.SaturatingMultiply(Math.Max(1L, collectionGoldPerPercent), percent);
        public bool RewardClaimed(CollectionGroup group, int percent) => claimedRewards.Contains(CollectionRewards.Key(group, percent));
        public List<string> CaptureRewardClaims() => new List<string>(claimedRewards);
        public void RestoreRewardClaims(IReadOnlyList<string> ids)
        {
            claimedRewards.Clear();
            if (ids != null) foreach (string id in ids) if (!string.IsNullOrEmpty(id)) claimedRewards.Add(id);
            rewardSavePending = false;
            Changed?.Invoke();
        }
        public void CollectionCount(CollectionGroup group, out int owned, out int total)
        {
            var monsters = GetComponent<MonsterManager>();
            var equipment = GetComponent<EquipmentManager>();
            CollectionRewards.Count(group, MonsterCatalogSO.Load(), EquipmentCatalogSO.Load(),
                id => monsters != null && monsters.HasAcquiredMonster(id),
                id => equipment != null && equipment.HasAcquiredEquipment(id), out owned, out total);
        }

        public bool ClaimCollectionReward(CollectionGroup group, int percent)
        {
            if (claimingReward || !Enum.IsDefined(typeof(CollectionGroup), group) ||
                Array.IndexOf(CollectionRewards.Milestones, percent) < 0 || RewardClaimed(group, percent)) return false;
            var game = GetComponent<GameManager>();
            if (game == null) return false;
            CollectionCount(group, out int owned, out int total);
            if (!CollectionRewards.Reached(owned, total, percent)) return false;
            claimingReward = true;
            try
            {
                claimedRewards.Add(CollectionRewards.Key(group, percent));
                rewardSavePending = true;
                rewardSaveAt = Time.unscaledTime;
                game.AddGold(CollectionRewardGold(percent));
                Changed?.Invoke();
                return true;
            }
            finally { claimingReward = false; }
        }

        private void Update()
        {
            if (!rewardSavePending || Time.unscaledTime < rewardSaveAt) return;
            rewardSavePending = GetComponent<SaveManager>()?.SaveGame() != true;
            rewardSaveAt = Time.unscaledTime + 1f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance() => Instance = null;

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public bool IsUnlocked(string monsterId) =>
            !string.IsNullOrWhiteSpace(monsterId) && defeated.Contains(monsterId);

        public bool RecordDefeat(string monsterId)
        {
            if (string.IsNullOrWhiteSpace(monsterId) || !defeated.Add(monsterId))
                return false;
            Changed?.Invoke();
            return true;
        }

        public List<string> Capture()
        {
            var result = new List<string>(defeated);
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        public void Restore(IReadOnlyList<string> monsterIds)
        {
            defeated.Clear();
            if (monsterIds != null)
                for (int i = 0; i < monsterIds.Count; i++)
                    if (!string.IsNullOrWhiteSpace(monsterIds[i]))
                        defeated.Add(monsterIds[i]);
            Changed?.Invoke();
        }
    }
}
