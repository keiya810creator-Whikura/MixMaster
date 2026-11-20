using System;
using System.Collections.Generic;
using MixMaster.Combat;
using MixMaster.Core;
using MixMaster.Monsters;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MixMaster.World
{
    internal sealed class RaidBattlefield : MonoBehaviour
    {
        internal enum Outcome { Fighting, Victory, Defeat, Timeout }
        internal const float TimeLimit = 120f;
        private const float HalfSize = 7f;
        // RenderTexture camera looks away from the main map and its world-space UI.
        private static readonly Vector3 Center = new Vector3(2048f, 2048f, 0f);

        private sealed class Ally
        {
            public PartyMemberCombat combat;
            public Transform hpFill;
            public SkillActor skillActor;
            public Action<long> healingHandler;
            public Action deathHandler;
            public CombatPerformanceRecord performance;
        }

        private readonly List<Ally> allies = new List<Ally>(MonsterManager.MaxFarmSize);
        private readonly Dictionary<string, CombatPerformanceRecord> performanceByOwner =
            new Dictionary<string, CombatPerformanceRecord>(StringComparer.Ordinal);
        private Scene arenaScene;
        private float duration;
        internal bool IsTraining { get; private set; }
        internal float Elapsed => Mathf.Max(0, duration - Remaining);
        internal IReadOnlyList<CombatPerformanceRecord> Performance
        {
            get
            {
                var result = new List<CombatPerformanceRecord>(performanceByOwner.Values);
                result.Sort((a, b) =>
                {
                    int damage = b.damage.CompareTo(a.damage);
                    return damage != 0 ? damage : StringComparer.Ordinal.Compare(a.ownerId, b.ownerId);
                });
                return result;
            }
        }
        private GameObject actors;
        private EnemyHealth boss;
        private Camera arenaCamera;
        private Sprite square;
        private RenderTexture view;

        internal RenderTexture View => view;
        internal Outcome Result { get; private set; }
        internal float Remaining { get; private set; } = TimeLimit;
        internal int LivingAllies { get; private set; }
        internal int AllyCount => allies.Count;
        internal long BossHp => boss != null ? boss.CurrentHp : 0;
        internal long BossMaxHp { get; private set; }
        internal long DamageDealt { get; private set; }

        internal static RaidBattlefield Create(MonsterSO definition, int level, Sprite floor,
            IReadOnlyList<OwnedMonsterRecord> members, MonsterCatalogSO catalog, EquipmentManager equipment, bool training = false)
        {
            var scene = SceneManager.CreateScene("FarmRaidArena_" + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            var root = new GameObject("RaidBattlefield");
            SceneManager.MoveGameObjectToScene(root, scene);
            var field = root.AddComponent<RaidBattlefield>();
            field.arenaScene = scene;
            field.IsTraining = training;
            field.duration = training ? 60f : TimeLimit;
            field.Remaining = field.duration;
            try
            {
                field.Build(definition, level, floor, members, catalog, equipment);
                return field;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                field.Close();
                return null;
            }
        }

        private void Build(MonsterSO definition, int level, Sprite floor,
            IReadOnlyList<OwnedMonsterRecord> members, MonsterCatalogSO catalog, EquipmentManager equipment)
        {
            view = new RenderTexture(768, 768, 16) { name = "FarmRaidView" };
            view.Create();
            var cameraObject = CreateChild("ArenaCamera", transform, Center + new Vector3(0, 0, -10));
            arenaCamera = cameraObject.AddComponent<Camera>();
            arenaCamera.clearFlags = CameraClearFlags.SolidColor;
            arenaCamera.backgroundColor = new Color(.035f, .055f, .085f);
            arenaCamera.orthographic = true;
            arenaCamera.orthographicSize = HalfSize + .5f;
            arenaCamera.nearClipPlane = .1f;
            arenaCamera.farClipPlane = 30f;
            arenaCamera.targetTexture = view;

            actors = CreateChild("Combatants", transform, Center);
            square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0,
                Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                new Vector2(.5f, .5f), Texture2D.whiteTexture.width);
            BuildTerrain(floor);

            var enemy = Instantiate(definition.enemyPrefab, Center, Quaternion.identity, actors.transform);
            enemy.name = "RaidBoss";
            var setup = enemy.GetComponent<MonsterRuntimeSetup>() ?? enemy.AddComponent<MonsterRuntimeSetup>();
            setup.RaidRewardsOnly = true;
            setup.Initialize(definition, level);
            DisableBehaviour<EnemyMaterialDropController>(enemy);
            DisableBehaviour<EnemyMonsterDropController>(enemy);
            DisableBehaviour<EnemyWanderAI>(enemy);
            boss = enemy.GetComponent<EnemyHealth>();
            enemy.GetComponent<EnemyCombatAI>().HuntRaidAllies();
            enemy.transform.localScale *= 5f;
            boss.RaidDamageFeedback = true;
            boss.IsTrainingTarget = IsTraining;
            boss.RefreshOriginalScale();
            BossMaxHp = boss.MaxHp;
            boss.Died += HandleBossDefeated;
            boss.Damaged += RecordDamage;

            foreach (var member in members)
            {
                if (allies.Count >= MonsterManager.MaxFarmSize) break;
                MonsterSO species = catalog.GetMonster(member.monsterId);
                if (species == null) continue;
                var stats = StatCalculator.CalculateOwnedMonsterStats(species, member,
                    catalog.GetTitle(member.titleId), equipment?.GetEquippedModifiers(member.equippedItemUniqueIds));
                int i = allies.Count;
                float angle = i * Mathf.PI * 2f / Mathf.Max(1, members.Count);
                float radius = i % 2 == 0 ? 4.1f : 5.6f;
                var position = Center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                var combat = PartyFieldSpawner.CreateMember(member, species, stats,
                    actors.transform, position, i, null);
                combat.SetCombatMode(PartyCombatMode.AutoHunt);
                combat.RaidDamageFeedback = true;
                combat.GetComponent<SkillActor>()?.RandomizeAutoSkills();
                foreach (var indicator in combat.GetComponentsInChildren<PlayerAttackRangeIndicator>(true))
                    indicator.HideForRaid();
                var bar = CreateChild("HP", combat.transform, position + Vector3.up * .55f);
                AddSprite(bar.transform, "Track", square, new Color(.10f, .12f, .16f),
                    Vector3.zero, new Vector2(3f, .22f), 12);
                var fill = AddSprite(bar.transform, "Fill", square, new Color(.3f, .95f, .44f),
                    Vector3.zero, new Vector2(3f, .16f), 13);
                var performance = new CombatPerformanceRecord
                {
                    ownerId = member.uniqueId,
                    name = member.DisplayName(species.displayName),
                    survived = true
                };
                performanceByOwner.Add(member.uniqueId, performance);
                var skillActor = combat.GetComponent<SkillActor>();
                Action<long> healingHandler = amount =>
                {
                    if (Result == Outcome.Fighting)
                        performance.healing = LongMath.SaturatingAdd(performance.healing, amount);
                };
                Action deathHandler = () =>
                {
                    performance.survivalSeconds = Elapsed;
                    performance.survived = false;
                };
                combat.Died += deathHandler;
                if (skillActor != null) skillActor.HealingPerformed += healingHandler;
                allies.Add(new Ally { combat = combat, hpFill = fill, skillActor = skillActor,
                    healingHandler = healingHandler, deathHandler = deathHandler, performance = performance });
            }
            LivingAllies = allies.Count;
        }

        private void BuildTerrain(Sprite floor)
        {
            if (floor != null)
                AddSprite(transform, "ArenaFloor", floor, Color.white, Center,
                    new Vector2(HalfSize * 2f, HalfSize * 2f), -20);
            else
            {
                for (int y = -3; y <= 3; y++)
                    for (int x = -3; x <= 3; x++)
                        AddSprite(transform, "ArenaTile", square,
                            (x + y) % 2 == 0 ? new Color(.15f, .20f, .26f) : new Color(.18f, .24f, .30f),
                            Center + new Vector3(x * 2f, y * 2f, 0f), Vector2.one * 2f, -20);
            }

            for (int side = 0; side < 4; side++)
            {
                bool vertical = side < 2;
                float offset = (side % 2 == 0 ? 1f : -1f) * (HalfSize + .18f);
                Vector2 size = vertical ? new Vector2(.36f, HalfSize * 2f + .72f) : new Vector2(HalfSize * 2f + .72f, .36f);
                Vector3 position = Center + (vertical ? Vector3.right : Vector3.up) * offset;
                AddSprite(transform, "Boundary", square, new Color(.36f, .32f, .30f), position, size, -19);
                CreateChild("ArenaWall", transform, position).AddComponent<BoxCollider2D>().size = size;
            }
        }

        private static void DisableBehaviour<T>(GameObject enemy) where T : Behaviour
        {
            if (enemy.TryGetComponent<T>(out var component)) component.enabled = false;
        }

        private static GameObject CreateChild(string name, Transform parent, Vector3 position)
        {
            var child = new GameObject(name);
            if (child.scene != parent.gameObject.scene)
                SceneManager.MoveGameObjectToScene(child, parent.gameObject.scene);
            child.transform.SetParent(parent, false);
            child.transform.position = position;
            return child;
        }

        private static Transform AddSprite(Transform parent, string name, Sprite sprite,
            Color color, Vector3 position, Vector2 size, int order)
        {
            var child = CreateChild(name, parent, position);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            child.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1f);
            return child.transform;
        }

        private void FixedUpdate()
        {
            if (Result == Outcome.Fighting)
                arenaScene.GetPhysicsScene2D().Simulate(Time.fixedDeltaTime);
        }

        private void Update()
        {
            if (Result != Outcome.Fighting) return;
            Remaining = Mathf.Max(0f, Remaining - Time.deltaTime);
            LivingAllies = 0;
            foreach (var ally in allies)
            {
                if (ally.combat == null || !ally.combat.IsAlive)
                {
                    ally.performance.survived = false;
                    if (ally.hpFill != null) ally.hpFill.parent.gameObject.SetActive(false);
                    continue;
                }
                LivingAllies++;
                ally.performance.survivalSeconds = Elapsed;
                float ratio = Mathf.Clamp01((float)((double)ally.combat.CurrentHp / Math.Max(1L, ally.combat.Stats.maxHp)));
                ally.hpFill.localScale = new Vector3(3f * ratio, .16f, 1f);
                ally.hpFill.localPosition = new Vector3(-1.5f * (1f - ratio), 0f, 0f);
            }
            if (LivingAllies == 0) Result = Outcome.Defeat;
            else if (Remaining <= 0f) Result = Outcome.Timeout;
        }

        private void RecordDamage(EnemyHealth target, long amount)
        {
            var source = target.LastHitSource;
            if (Result != Outcome.Fighting || source == null || string.IsNullOrEmpty(source.ownedMonsterUniqueId)) return;
            if (!performanceByOwner.TryGetValue(source.ownedMonsterUniqueId, out var performance)) return;
            performance.damage = LongMath.SaturatingAdd(performance.damage, amount);
            DamageDealt = LongMath.SaturatingAdd(DamageDealt, amount);
        }

        private void HandleBossDefeated(EnemyHealth _)
        {
            if (Result == Outcome.Fighting) Result = Outcome.Victory;
        }

        internal void Freeze()
        {
            foreach (var ally in allies)
            {
                ally.performance.survived = ally.combat != null && ally.combat.IsAlive;
                if (ally.performance.survived) ally.performance.survivalSeconds = Elapsed;
            }
            enabled = false;
            foreach (var root in arenaScene.GetRootGameObjects())
                if (root != gameObject) root.SetActive(false);
            // Retain the last rendered frame behind the result text.
            arenaCamera.enabled = false;
            actors.SetActive(false);
        }

        internal void Close()
        {
            if (!arenaScene.IsValid() || !arenaScene.isLoaded) return;
            foreach (var root in arenaScene.GetRootGameObjects()) root.SetActive(false);
            SceneManager.UnloadSceneAsync(arenaScene);
            arenaScene = default;
        }

        private void OnDestroy()
        {
            foreach (var ally in allies)
            {
                if (ally.skillActor != null) ally.skillActor.HealingPerformed -= ally.healingHandler;
                if (ally.combat != null) ally.combat.Died -= ally.deathHandler;
            }
            if (boss != null)
            {
                boss.Died -= HandleBossDefeated;
                boss.Damaged -= RecordDamage;
            }
            if (arenaCamera != null) arenaCamera.targetTexture = null;
            if (view != null)
            {
                view.Release();
                Destroy(view);
            }
            if (square != null) Destroy(square);
        }
    }
}
