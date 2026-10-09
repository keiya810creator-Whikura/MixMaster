using System;
using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Core
{
    [Serializable]
    public sealed class MonsterDropRollData
    {
        public MonsterSO monster;
        public MonsterIndividualValues individualValues =
            new MonsterIndividualValues();
        public TitleSO title;
    }

    public static class MonsterDropRoller
    {
        // Temporary base rate until the dungeon-material altar upgrade
        // is connected to title appearance rate.
        public const float BaseTitleChance = 0.01f;

        public static MonsterIndividualValues RollIndividualValues()
        {
            return new MonsterIndividualValues
            {
                hp = RollIv(),
                mp = RollIv(),
                attack = RollIv(),
                magic = RollIv(),
                defense = RollIv(),
                magicDefense = RollIv(),
                criticalRate = RollIv(),
                criticalMultiplier = RollIv(),
                moveSpeed = RollIv(),
                attackSpeed = RollIv(),
                attackRange = RollIv()
            };
        }

        public static TitleSO RollTitle(
            IReadOnlyList<TitleSO> titlePool,
            float titleChance = BaseTitleChance)
        {
            if (titlePool == null ||
                titlePool.Count == 0 ||
                UnityEngine.Random.value >=
                Mathf.Clamp01(titleChance))
            {
                return null;
            }

            float totalWeight = 0f;

            for (int i = 0; i < titlePool.Count; i++)
            {
                TitleSO title = titlePool[i];

                if (title == null)
                    continue;

                totalWeight +=
                    Mathf.Max(
                        0.0001f,
                        title.selectionWeight);
            }

            if (totalWeight <= 0f)
                return null;

            float roll =
                UnityEngine.Random.value *
                totalWeight;

            float accumulated = 0f;

            for (int i = 0; i < titlePool.Count; i++)
            {
                TitleSO title = titlePool[i];

                if (title == null)
                    continue;

                accumulated +=
                    Mathf.Max(
                        0.0001f,
                        title.selectionWeight);

                if (roll <= accumulated)
                    return title;
            }

            for (int i = titlePool.Count - 1; i >= 0; i--)
            {
                if (titlePool[i] != null)
                    return titlePool[i];
            }

            return null;
        }

        public static MonsterIndividualValues CloneIndividualValues(
            MonsterIndividualValues source)
        {
            if (source == null)
                return new MonsterIndividualValues();

            return new MonsterIndividualValues
            {
                hp = source.hp,
                mp = source.mp,
                attack = source.attack,
                magic = source.magic,
                defense = source.defense,
                magicDefense = source.magicDefense,
                criticalRate = source.criticalRate,
                criticalMultiplier = source.criticalMultiplier,
                moveSpeed = source.moveSpeed,
                attackSpeed = source.attackSpeed,
                attackRange = source.attackRange
            };
        }

        private static int RollIv()
        {
            // int Random.Range max is exclusive, hence 101.
            return UnityEngine.Random.Range(0, 101);
        }
    }
}
