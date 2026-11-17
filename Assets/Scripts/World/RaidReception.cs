using MixMaster.Core;
using UnityEngine;

namespace MixMaster.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D), typeof(MapBuildingAccess))]
    public sealed class RaidReception : MonoBehaviour
    {
        [Tooltip("難易度1から順に設定します。空欄は MON_30000 ～ MON_30009 を検索します。")]
        [SerializeField] private MonsterSO[] bosses = new MonsterSO[MonsterManager.RaidTierCount];
        [Tooltip("難易度ごとのボスレベルです。未設定時は Lv1 になります。")]
        [SerializeField] private int[] bossLevels = new int[MonsterManager.RaidTierCount];
        [SerializeField, Min(1)] private int materialsPerTier = 5;

        [Tooltip("難易度順のベビー報酬。空欄は MON_30010 ～ MON_30019 を検索します。")]
        [SerializeField] private MonsterSO[] rewardBabies = new MonsterSO[MonsterManager.RaidTierCount];
        [SerializeField] private Sprite arenaFloor;
        internal Sprite ArenaFloor => arenaFloor;

        private void Awake() => GetComponent<MapBuildingAccess>().Configure(MapBuildingType.RaidReception);

        public MonsterSO GetBoss(int tier, MonsterCatalogSO catalog)
        {
            if (bosses != null && tier < bosses.Length && bosses[tier] != null) return bosses[tier];
            return catalog != null ? catalog.GetMonster($"MON_{30000 + tier}") : null;
        }

        internal MonsterSO GetRewardBaby(int tier, MonsterCatalogSO catalog)
        {
            if (catalog == null) return null;
            var configured = rewardBabies != null && tier < rewardBabies.Length ? rewardBabies[tier] : null;
            return catalog.GetMonster(configured != null ? configured.monsterId : $"MON_{30010 + tier}");
        }

        public int GetBossLevel(int tier) =>
            bossLevels != null && tier < bossLevels.Length ? Mathf.Max(1, bossLevels[tier]) : 1;

        public int GetReward(int tier) =>
            (int)System.Math.Min(int.MaxValue, (long)Mathf.Max(1, materialsPerTier) * (tier + 1));
    }
}
