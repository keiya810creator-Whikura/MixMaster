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

        public void AddMaterial(
            MaterialSO material,
            long amount)
        {
            if (material == null || amount <= 0L)
                return;

            AddMaterial(
                material.materialId,
                amount);
        }

        public void AddMaterial(
            string materialId,
            long amount)
        {
            if (string.IsNullOrWhiteSpace(materialId) ||
                amount <= 0L)
            {
                return;
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
                return;

            MaterialChanged?.Invoke(
                materialId,
                actualAdded,
                record.amount);
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
