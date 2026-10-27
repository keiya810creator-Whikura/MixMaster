using System;
using System.Collections;
using System.Collections.Generic;
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
        [Header("Drop pool")]
        [SerializeField, Min(0)] private int prewarmPerType = 32;
        [SerializeField, Min(0)] private int retainedPerType = 128;
        private readonly Stack<MaterialDropPickup> materials = new Stack<MaterialDropPickup>();
        private readonly Stack<MonsterDropPickup> monsters = new Stack<MonsterDropPickup>();

        private IEnumerator Start()
        {
            // Spread warmup over frames rather than moving the combat spike to scene startup.
            int count = Mathf.Min(Mathf.Max(0, prewarmPerType), Mathf.Max(0, retainedPerType));
            for (int i = 0; i < count; i++)
            {
                if ((i & 1) == 0) yield return null;
                if (materials.Count < count) RecycleMaterial(CreateMaterialDrop(Vector3.zero));
                if (monsters.Count < count) RecycleMonster(CreateMonsterDrop(Vector3.zero));
            }
        }

        private void OnDestroy()
        {
            foreach (var drop in materials) if (drop != null) Destroy(drop.gameObject);
            foreach (var drop in monsters) if (drop != null) Destroy(drop.gameObject);
            materials.Clear(); monsters.Clear();
        }

        internal void RecycleMaterial(MaterialDropPickup pickup)
        {
            if (pickup == null) return;
            pickup.ClearForPool(); pickup.gameObject.SetActive(false);
            if (materials.Count < Mathf.Max(0, retainedPerType)) materials.Push(pickup);
            else Destroy(pickup.gameObject);
        }
        internal void RecycleMonster(MonsterDropPickup pickup)
        {
            if (pickup == null) return;
            pickup.ClearForPool(); pickup.gameObject.SetActive(false);
            if (monsters.Count < Mathf.Max(0, retainedPerType)) monsters.Push(pickup);
            else Destroy(pickup.gameObject);
        }


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
        /// Base material rate comes from MonsterSO and may exceed 1.0.
        /// Killer drop bonus is added directly and may exceed 100%.
        /// </summary>
        public long RollMaterialQuantity(double baseMaterialRate, double killerDropRateBonus)
        {
            double finalRate = CalculateAdditiveRate(
                baseMaterialRate,
                killerDropRateBonus);

            return RollQuantityFromRate(finalRate);
        }

        /// <summary>
        /// Monster acquisition uses only the species/map altar rate.
        /// Character drop-rate bonuses apply to materials, not monster bodies.
        /// </summary>
        public bool RollMonsterBodyDrop(double bodyDropRate)
        {
            if (bodyDropRate >= 1d)
                return true;

            if (bodyDropRate <= 0d)
                return false;

            return UnityEngine.Random.value <
                   bodyDropRate;
        }

        public void SpawnMaterialDrop(MaterialSO material, long quantity, Vector3 worldPosition, MaterialDropSourceInfo sourceInfo)
        {
            if (material == null || quantity <= 0L) return;
            MaterialDropPickup pickup = null;
            while (materials.Count > 0 && pickup == null) pickup = materials.Pop();
            if (pickup == null) pickup = CreateMaterialDrop(worldPosition);
            var go = pickup.gameObject;
            go.transform.position = worldPosition; go.transform.rotation = Quaternion.identity;
            go.name = "MaterialDrop_" + (!string.IsNullOrWhiteSpace(material.materialId) ? material.materialId : material.name);
            pickup.Initialize(material, quantity, sourceInfo);
            go.SetActive(true);
        }

        public void SpawnMonsterDrop(MonsterDropRollData rollData, Vector3 worldPosition)
        {
            if (rollData == null || rollData.monster == null) return;
            MonsterDropPickup pickup = null;
            while (monsters.Count > 0 && pickup == null) pickup = monsters.Pop();
            if (pickup == null) pickup = CreateMonsterDrop(worldPosition);
            var go = pickup.gameObject;
            go.transform.position = worldPosition; go.transform.rotation = Quaternion.identity;
            go.name = "MonsterDrop_" + (!string.IsNullOrWhiteSpace(rollData.monster.monsterId) ? rollData.monster.monsterId : rollData.monster.name);
            pickup.Initialize(rollData);
            go.SetActive(true);
        }

        private MaterialDropPickup CreateMaterialDrop(Vector3 position)
        {
            var prefab = GetMaterialDropPrefab();
            var go = prefab != null ? Instantiate(prefab, position, Quaternion.identity) : CreateRuntimeFallbackDrop(position);
            var pickup = go.GetComponent<MaterialDropPickup>() ?? go.AddComponent<MaterialDropPickup>();
            pickup.SetPoolOwner(this);
            return pickup;
        }

        private MonsterDropPickup CreateMonsterDrop(Vector3 position)
        {
            var prefab = GetMonsterDropPrefab();
            var go = prefab != null ? Instantiate(prefab, position, Quaternion.identity) : CreateRuntimeFallbackMonsterDrop(position);
            var pickup = go.GetComponent<MonsterDropPickup>() ?? go.AddComponent<MonsterDropPickup>();
            pickup.SetPoolOwner(this);
            return pickup;
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
