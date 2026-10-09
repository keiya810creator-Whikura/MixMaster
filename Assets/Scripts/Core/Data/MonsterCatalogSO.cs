using System;
using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Core
{
    [CreateAssetMenu(
        fileName = "MonsterCatalog",
        menuName = "MixMaster/Data/Monster Catalog")]
    public sealed class MonsterCatalogSO : ScriptableObject
    {
        public const string ResourcesPath =
            "Data/MonsterCatalog";

        [SerializeField] private List<MonsterSO> monsters =
            new List<MonsterSO>();

        [SerializeField] private List<TitleSO> titles =
            new List<TitleSO>();

        private Dictionary<string, MonsterSO> monsterById;
        private Dictionary<string, TitleSO> titleById;

        private static MonsterCatalogSO cached;

        public IReadOnlyList<MonsterSO> Monsters => monsters;
        public IReadOnlyList<TitleSO> Titles => titles;

        public static MonsterCatalogSO Load()
        {
            if (cached == null)
            {
                cached =
                    Resources.Load<MonsterCatalogSO>(
                        ResourcesPath);
            }

            return cached;
        }

        public MonsterSO GetMonster(string monsterId)
        {
            if (string.IsNullOrWhiteSpace(monsterId))
                return null;

            EnsureLookup();

            monsterById.TryGetValue(
                monsterId,
                out MonsterSO monster);

            return monster;
        }

        public TitleSO GetTitle(string titleId)
        {
            if (string.IsNullOrWhiteSpace(titleId))
                return null;

            EnsureLookup();

            titleById.TryGetValue(
                titleId,
                out TitleSO title);

            return title;
        }

        public void SetData(
            List<MonsterSO> newMonsters,
            List<TitleSO> newTitles)
        {
            monsters =
                newMonsters ??
                new List<MonsterSO>();

            titles =
                newTitles ??
                new List<TitleSO>();

            monsterById = null;
            titleById = null;
        }

        private void EnsureLookup()
        {
            if (monsterById != null &&
                titleById != null)
            {
                return;
            }

            monsterById =
                new Dictionary<string, MonsterSO>(
                    StringComparer.OrdinalIgnoreCase);

            titleById =
                new Dictionary<string, TitleSO>(
                    StringComparer.OrdinalIgnoreCase);

            if (monsters != null)
            {
                for (int i = 0; i < monsters.Count; i++)
                {
                    MonsterSO monster = monsters[i];

                    if (monster == null ||
                        string.IsNullOrWhiteSpace(
                            monster.monsterId) ||
                        monsterById.ContainsKey(
                            monster.monsterId))
                    {
                        continue;
                    }

                    monsterById.Add(
                        monster.monsterId,
                        monster);
                }
            }

            if (titles != null)
            {
                for (int i = 0; i < titles.Count; i++)
                {
                    TitleSO title = titles[i];

                    if (title == null ||
                        string.IsNullOrWhiteSpace(
                            title.titleId) ||
                        titleById.ContainsKey(
                            title.titleId))
                    {
                        continue;
                    }

                    titleById.Add(
                        title.titleId,
                        title);
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            monsterById = null;
            titleById = null;
        }
#endif
    }
}
