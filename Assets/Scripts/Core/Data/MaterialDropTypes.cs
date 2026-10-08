using System;
using UnityEngine;

namespace MixMaster.Core
{
    public enum MaterialDropSourceType
    {
        Unknown,
        Player,
        Party1,
        Party2,
        Party3
    }

    [Serializable]
    public sealed class MaterialDropSourceInfo
    {
        public MaterialDropSourceType sourceType =
            MaterialDropSourceType.Unknown;

        public float dropRateBonus;

        public static MaterialDropSourceInfo Unknown()
        {
            return new MaterialDropSourceInfo();
        }

        public static MaterialDropSourceInfo Player(
            float dropRateBonus)
        {
            return new MaterialDropSourceInfo
            {
                sourceType = MaterialDropSourceType.Player,
                dropRateBonus = Mathf.Max(0f, dropRateBonus)
            };
        }

        public static MaterialDropSourceInfo Party(
            int followOrder,
            float dropRateBonus)
        {
            MaterialDropSourceType type;

            switch (Mathf.Clamp(followOrder, 0, 2))
            {
                case 0:
                    type = MaterialDropSourceType.Party1;
                    break;

                case 1:
                    type = MaterialDropSourceType.Party2;
                    break;

                default:
                    type = MaterialDropSourceType.Party3;
                    break;
            }

            return new MaterialDropSourceInfo
            {
                sourceType = type,
                dropRateBonus = Mathf.Max(0f, dropRateBonus)
            };
        }

        public MaterialDropSourceInfo Clone()
        {
            return new MaterialDropSourceInfo
            {
                sourceType = sourceType,
                dropRateBonus = dropRateBonus
            };
        }
    }

    [Serializable]
    public sealed class MaterialInventoryRecord
    {
        public string materialId;
        public long amount;
    }
}
