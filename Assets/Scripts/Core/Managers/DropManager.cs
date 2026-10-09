using System;
using UnityEngine;
using MixMaster.World;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    public sealed class DropManager : MonoBehaviour
    {
        private const string MaterialDropResourcePath =
            "Prefabs/MaterialDrop";

        private const string MonsterDropResourcePath =
            "Prefabs/MonsterDrop";

        private GameObject cachedMaterialDropPrefab;
        private GameObject cachedMonsterDropPrefab;

        /// <summary>
        /// Generic 0..1 probability roll.
        /// </summary>
        public bool Roll(float probability)
        {
            probability = Mathf.Clamp01(probability);
            return UnityEngine.Random.value < probability;
        }

        /// <summary>
        /// New project rule: drop-rate bonuses are additive.
        /// Example: base 0.10 + bonus 2.00 = final 2.10 (210%).
        /// </summary>
        public double ApplyRateBonus(double baseRate, double bonusRate)
        {
            return CalculateAdditiveRate(baseRate, bonusRate);
        }

        public double CalculateAdditiveRate(double baseRate, double bonusRate)
        {
            baseRate = Math.Max(0d, baseRate);
            bonusRate = Math.Max(0d, bonusRate);
            return baseRate + bonusRate;
        }

        /// <summary>
        /// Converts an uncapped rate into quantity.
        /// 2.10 => 2 guaranteed + 10% chance of one more.
        /// </summary>
        public long RollQuantityFromRate(double finalRate)
        {
            if (finalRate <= 0d)
                return 0L;

            if (finalRate >= long.MaxValue)
                return long.MaxValue;

            long guaranteed = (long)Math.Floor(finalRate);
            double remainder = finalRate - guaranteed;

            if (remainder > 0d && UnityEngine.Random.value < remainder)
                guaranteed = LongMath.SaturatingAdd(guaranteed, 1L);

            return guaranteed;
        }

        /// <summary>
        /// Material drop helper for the current specification.
        /// Base material rate comes from EnemySO.
        /// Killer drop bonus is added directly and may exceed 100%.
        /// </summary>
        public long RollMaterialQuantity(double baseMaterialRate, double killerDropRateBonus)
        {
            double finalRate = CalculateAdditiveRate(
                baseMaterialRate,
                killerDropRateBonus);

            return RollQuantityFromRate(finalRate);
        }

        public bool RollMonsterBodyDrop(
            double bodyDropRate,
            double killerDropRateBonus)
        {
            double finalRate =
                CalculateAdditiveRate(
                    bodyDropRate,
                    killerDropRateBonus);

            if (finalRate >= 1d)
                return true;

            if (finalRate <= 0d)
                return false;

            return UnityEngine.Random.value <
                   finalRate;
        }

        public void SpawnMaterialDrop(
            MaterialSO material,
            long quantity,
            Vector3 worldPosition,
            MaterialDropSourceInfo sourceInfo)
        {
            if (material == null || quantity <= 0L)
                return;

            GameObject prefab =
                GetMaterialDropPrefab();

            GameObject dropObject;

            if (prefab != null)
            {
                dropObject =
                    Instantiate(
                        prefab,
                        worldPosition,
                        Quaternion.identity);
            }
            else
            {
                dropObject =
                    CreateRuntimeFallbackDrop(
                        worldPosition);
            }

            if (dropObject == null)
                return;

            dropObject.name =
                "MaterialDrop_" +
                (!string.IsNullOrWhiteSpace(material.materialId)
                    ? material.materialId
                    : material.name);

            MaterialDropPickup pickup =
                dropObject.GetComponent<MaterialDropPickup>();

            if (pickup == null)
            {
                pickup =
                    dropObject.AddComponent<MaterialDropPickup>();
            }

            pickup.Initialize(
                material,
                quantity,
                sourceInfo);
        }

        public void SpawnMonsterDrop(
            MonsterDropRollData rollData,
            Vector3 worldPosition)
        {
            if (rollData == null ||
                rollData.monster == null)
            {
                return;
            }

            GameObject prefab =
                GetMonsterDropPrefab();

            GameObject dropObject;

            if (prefab != null)
            {
                dropObject =
                    Instantiate(
                        prefab,
                        worldPosition,
                        Quaternion.identity);
            }
            else
            {
                dropObject =
                    CreateRuntimeFallbackMonsterDrop(
                        worldPosition);
            }

            if (dropObject == null)
                return;

            dropObject.name =
                "MonsterDrop_" +
                (!string.IsNullOrWhiteSpace(
                    rollData.monster.monsterId)
                    ? rollData.monster.monsterId
                    : rollData.monster.name);

            MonsterDropPickup pickup =
                dropObject.GetComponent<MonsterDropPickup>();

            if (pickup == null)
            {
                pickup =
                    dropObject.AddComponent<MonsterDropPickup>();
            }

            pickup.Initialize(
                rollData);
        }

        private GameObject GetMaterialDropPrefab()
        {
            if (cachedMaterialDropPrefab != null)
                return cachedMaterialDropPrefab;

            cachedMaterialDropPrefab =
                Resources.Load<GameObject>(
                    MaterialDropResourcePath);

            return cachedMaterialDropPrefab;
        }

        private GameObject GetMonsterDropPrefab()
        {
            if (cachedMonsterDropPrefab != null)
                return cachedMonsterDropPrefab;

            cachedMonsterDropPrefab =
                Resources.Load<GameObject>(
                    MonsterDropResourcePath);

            return cachedMonsterDropPrefab;
        }

        private static GameObject CreateRuntimeFallbackDrop(
            Vector3 worldPosition)
        {
            GameObject drop =
                new GameObject("MaterialDrop");

            drop.transform.position =
                worldPosition;

            SpriteRenderer renderer =
                drop.AddComponent<SpriteRenderer>();

            renderer.sortingOrder = 40;

            drop.AddComponent<MaterialDropPickup>();

            return drop;
        }

        private static GameObject CreateRuntimeFallbackMonsterDrop(
            Vector3 worldPosition)
        {
            GameObject drop =
                new GameObject("MonsterDrop");

            drop.transform.position =
                worldPosition;

            SpriteRenderer renderer =
                drop.AddComponent<SpriteRenderer>();

            renderer.sortingOrder = 41;

            drop.AddComponent<MonsterDropPickup>();

            return drop;
        }

        public long ApplyQuantityMultiplier(long baseAmount, double multiplier)
        {
            if (baseAmount <= 0 || multiplier <= 0d)
                return 0L;

            double value = baseAmount * multiplier;

            if (value >= long.MaxValue)
                return long.MaxValue;

            return (long)value;
        }
    }
}
