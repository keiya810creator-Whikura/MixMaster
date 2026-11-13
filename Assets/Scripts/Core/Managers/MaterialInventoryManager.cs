using System;
using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    public sealed class MaterialInventoryManager : MonoBehaviour
    {
        public const long MaxMaterialAmount = 999999L;

        private readonly List<MaterialInventoryRecord> records =
            new List<MaterialInventoryRecord>();

        public IReadOnlyList<MaterialInventoryRecord> Records =>
            records;

        public event Action<string, long, long> MaterialChanged;
        public event Action MaterialsRestored;

        public void RestoreMaterials(
            IReadOnlyList<MaterialInventoryRecord> savedRecords)
        {
            records.Clear();

            if (savedRecords == null)
            {
                MaterialsRestored?.Invoke();
                return;
            }

            for (int i = 0; i < savedRecords.Count; i++)
            {
                MaterialInventoryRecord item = savedRecords[i];
                if (item == null || string.IsNullOrWhiteSpace(item.materialId))
                    continue;

                long value = Math.Min(MaxMaterialAmount,
                    Math.Max(0L, item.amount));

                MaterialInventoryRecord existing =
                    records.Find(x => x.materialId == item.materialId);

                if (existing != null)
                    existing.amount = Math.Min(MaxMaterialAmount,
                        LongMath.SaturatingAdd(existing.amount, value));
                else
                    records.Add(new MaterialInventoryRecord
                    {
                        materialId = item.materialId, amount = value
                    });
            }

            for (int i = 0; i < records.Count; i++)
            {
                MaterialInventoryRecord record = records[i];
                MaterialChanged?.Invoke(
                    record.materialId, 0L, record.amount);
            }
            MaterialsRestored?.Invoke();
        }

        public long GetAmount(string materialId)
        {
            if (string.IsNullOrWhiteSpace(materialId))
                return 0L;

            MaterialInventoryRecord record =
                records.Find(x => x.materialId == materialId);

            return record != null
                ? Math.Max(0L, record.amount)
                : 0L;
        }

        public long GetAmount(MaterialSO material)
        {
            return material == null
                ? 0L
                : GetAmount(material.materialId);
        }

        public long AddMaterial(
            MaterialSO material,
            long amount)
        {
            if (material == null || amount <= 0L)
                return 0L;

            return AddMaterial(
                material.materialId,
                amount);
        }

        public long AddMaterial(
            string materialId,
            long amount)
        {
            if (string.IsNullOrWhiteSpace(materialId) ||
                amount <= 0L)
            {
                return 0L;
            }

            MaterialInventoryRecord record =
                records.Find(x => x.materialId == materialId);

            if (record == null)
            {
                record = new MaterialInventoryRecord
                {
                    materialId = materialId,
                    amount = 0L
                };

                records.Add(record);
            }

            long before =
                Math.Min(
                    MaxMaterialAmount,
                    Math.Max(0L, record.amount));

            long after =
                LongMath.SaturatingAdd(
                    before,
                    amount);

            after =
                Math.Min(
                    MaxMaterialAmount,
                    Math.Max(0L, after));

            record.amount = after;

            long actualAdded =
                Math.Max(0L, after - before);

            if (actualAdded <= 0L)
                return 0L;

            MaterialChanged?.Invoke(
                materialId,
                actualAdded,
                record.amount);

            return actualAdded;
        }

        /// <summary>
        /// Validates and subtracts both material costs as one operation.
        /// If both parents drop the same material, the amounts are combined.
        /// Zero costs are allowed; no partial deduction on insufficient stock.
        /// </summary>
        public bool TryConsumeBreedingMaterials(
            string materialAId, long amountA,
            string materialBId, long amountB)
        {
            if (amountA < 0L || amountB < 0L)
                return false;
            if (amountA > 0L && string.IsNullOrWhiteSpace(materialAId))
                return false;
            if (amountB > 0L && string.IsNullOrWhiteSpace(materialBId))
                return false;

            bool same = amountA > 0L && amountB > 0L &&
                string.Equals(materialAId, materialBId,
                    StringComparison.OrdinalIgnoreCase);
            if (same)
            {
                if (amountA > long.MaxValue - amountB)
                    return false;
                amountA += amountB;
                amountB = 0L;
            }

            MaterialInventoryRecord a = amountA > 0L
                ? records.Find(x => string.Equals(x.materialId, materialAId,
                    StringComparison.OrdinalIgnoreCase)) : null;
            MaterialInventoryRecord b = amountB > 0L
                ? records.Find(x => string.Equals(x.materialId, materialBId,
                    StringComparison.OrdinalIgnoreCase)) : null;
            if ((amountA > 0L && (a == null || a.amount < amountA)) ||
                (amountB > 0L && (b == null || b.amount < amountB)))
                return false;

            if (amountA > 0L) a.amount -= amountA;
            if (amountB > 0L) b.amount -= amountB;

            if (amountA > 0L)
                MaterialChanged?.Invoke(materialAId, -amountA, a.amount);
            if (amountB > 0L)
                MaterialChanged?.Invoke(materialBId, -amountB, b.amount);
            return true;
        }

        /// <summary>Checks the entire set before any deduction or notification.</summary>
        internal bool TryConsumeSet(IReadOnlyList<MaterialSO> materials, long amount)
        {
            if (materials == null || materials.Count == 0 || amount <= 0L)
                return false;
            var costs = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (MaterialSO material in materials)
            {
                if (material == null || string.IsNullOrWhiteSpace(material.materialId))
                    return false;
                costs.TryGetValue(material.materialId, out long previous);
                if (previous > long.MaxValue - amount) return false;
                costs[material.materialId] = previous + amount;
            }
            foreach (var cost in costs)
                if (GetAmount(cost.Key) < cost.Value) return false;
            foreach (var cost in costs)
                records.Find(x => x.materialId == cost.Key).amount -= cost.Value;
            return true;
        }

        internal void NotifyConsumedSet(IReadOnlyList<MaterialSO> materials, long amount)
        {
            foreach (MaterialSO material in materials)
                MaterialChanged?.Invoke(material.materialId, -amount,
                    GetAmount(material.materialId));
        }

        public bool TryConsume(
            MaterialSO material,
            long amount)
        {
            if (material == null)
                return false;

            return TryConsume(
                material.materialId,
                amount);
        }

        public bool TryConsume(
            string materialId,
            long amount)
        {
            if (string.IsNullOrWhiteSpace(materialId) ||
                amount <= 0L)
            {
                return false;
            }

            MaterialInventoryRecord record =
                records.Find(x => x.materialId == materialId);

            if (record == null ||
                record.amount < amount)
            {
                return false;
            }

            record.amount -= amount;

            MaterialChanged?.Invoke(
                materialId,
                -amount,
                record.amount);

            return true;
        }
    }
}
