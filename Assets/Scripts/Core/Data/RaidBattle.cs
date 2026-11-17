using System;
using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Core
{
    internal sealed class RaidBattle
    {
        internal sealed class Fighter
        {
            public readonly MonsterSO Species;
            public readonly CharacterStats Stats;
            public long Hp;
            public float Cooldown;
            public float LastAttack = -1f;

            public Fighter(MonsterSO species, CharacterStats stats)
            {
                Species = species;
                Stats = stats;
                Hp = Math.Max(1L, stats.maxHp);
            }

            public bool CanAttack(float delta)
            {
                if (Hp <= 0) return false;
                Cooldown -= delta;
                if (Cooldown > 0f) return false;
                Cooldown += 1f / Mathf.Clamp(Stats.attackSpeed, .1f, 20f);
                return true;
            }

            public void Attack(Fighter target, float time)
            {
                LastAttack = time;
                bool magic = Stats.magic > Stats.attack;
                long damage = DamageCalculator.Calculate(
                    Math.Max(1L, magic ? Stats.magic : Stats.attack),
                    magic ? target.Stats.magicDefense : target.Stats.defense);
                if (UnityEngine.Random.value < Stats.criticalRate)
                {
                    double criticalDamage = damage * (double)Stats.criticalMultiplier;
                    damage = criticalDamage >= long.MaxValue ? long.MaxValue : (long)criticalDamage;
                }
                damage = DamageCalculator.Randomize(damage);
                target.Hp = Math.Max(0L, target.Hp - damage);
            }
        }

        public readonly List<Fighter> Allies = new List<Fighter>(MonsterManager.MaxFarmSize);
        public readonly Fighter Boss;
        public const float TimeLimit = 120f;
        public float Elapsed { get; private set; }
        public bool Won => Boss.Hp == 0;
        public bool Finished { get; private set; }

        public RaidBattle(MonsterSO boss, int level, IReadOnlyList<OwnedMonsterRecord> farm,
            MonsterCatalogSO catalog, EquipmentManager equipment)
        {
            Boss = new Fighter(boss, StatCalculator.CalculateEnemyStats(boss, level, new MonsterIndividualValues()));
            foreach (var member in farm)
            {
                var species = catalog != null ? catalog.GetMonster(member.monsterId) : null;
                if (species == null) continue;
                var title = catalog.GetTitle(member.titleId);
                var modifiers = equipment?.GetEquippedModifiers(member.equippedItemUniqueIds);
                Allies.Add(new Fighter(species, StatCalculator.CalculateOwnedMonsterStats(species, member, title, modifiers)));
            }
            Finished = Allies.Count == 0;
        }

        public void Step(float delta)
        {
            if (Finished) return;
            Elapsed += delta;
            if (Elapsed >= TimeLimit)
            {
                Finished = true;
                return;
            }

            foreach (var ally in Allies)
            {
                if (ally.CanAttack(delta)) ally.Attack(Boss, Elapsed);
                if (!Won) continue;
                Finished = true;
                return;
            }

            // A raid boss hits the entire formation; knocked-out allies stop attacking.
            if (Boss.CanAttack(delta))
                foreach (var ally in Allies)
                    if (ally.Hp > 0) Boss.Attack(ally, Elapsed);

            Finished = Allies.TrueForAll(ally => ally.Hp == 0);
        }
    }
}
