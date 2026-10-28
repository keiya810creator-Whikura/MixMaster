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
        public bool isSkill;
        public int attackSequence;
        public ElementType attackElement;
        public bool isCritical;
        private static int nextAttackSequence;

        // Exact owned-individual identity is carried through projectiles.
        // Party slot alone is insufficient if members are rearranged.
        public string ownedMonsterUniqueId;

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
                attackSequence = ++nextAttackSequence,
                dropRateBonus = Mathf.Max(0f, dropRateBonus)
            };
        }

        public static MaterialDropSourceInfo Party(
            int followOrder,
            float dropRateBonus,
            string individualUniqueId = null)
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
                attackSequence = ++nextAttackSequence,
                dropRateBonus = Mathf.Max(0f, dropRateBonus),
                ownedMonsterUniqueId = individualUniqueId
            };
        }

        public MaterialDropSourceInfo Clone()
        {
            return new MaterialDropSourceInfo
            {
                sourceType = sourceType,
                isSkill = isSkill,
                attackSequence = attackSequence,
                attackElement = attackElement,
                isCritical = isCritical,
                dropRateBonus = dropRateBonus,
                ownedMonsterUniqueId = ownedMonsterUniqueId
            };
        }

        public MaterialDropSourceInfo WithAttack(ElementType element, bool critical)
        {
            attackElement = element;
            isCritical = critical;
            return this;
        }
    }

    [Serializable]
    public sealed class MaterialInventoryRecord
    {
        public string materialId;
        public long amount;
    }
}
