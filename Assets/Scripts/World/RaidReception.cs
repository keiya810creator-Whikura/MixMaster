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

        private void Awake() => GetComponent<MapBuildingAccess>().Configure(MapBuildingType.RaidReception);

        public MonsterSO GetBoss(int tier, MonsterCatalogSO catalog)
        {
            if (bosses != null && tier < bosses.Length && bosses[tier] != null) return bosses[tier];
            return catalog != null ? catalog.GetMonster($"MON_{30000 + tier}") : null;
        }

        public int GetBossLevel(int tier) =>
            bossLevels != null && tier < bossLevels.Length ? Mathf.Max(1, bossLevels[tier]) : 1;

        public int GetReward(int tier) =>
            (int)System.Math.Min(int.MaxValue, (long)Mathf.Max(1, materialsPerTier) * (tier + 1));
    }
}
