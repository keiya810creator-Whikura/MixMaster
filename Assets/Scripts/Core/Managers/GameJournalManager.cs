using System;
using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    public sealed class GameJournalManager : MonoBehaviour
    {
        public const int HistoryLimit = 200, GoalLimit = 50, TrackedGoalLimit = 7;
        public const long MaximumTarget = 999999;
        public static GameJournalManager Instance { get; private set; }
        private GameJournalData data = NewData();
        private bool paused, focused = true, pending;
        private float saveAt;
        private SaveManager save;
        private int revision, capturedRevision;
        public event Action Changed;
        public bool SavePending => pending;
        public double PlaySeconds => data.playSeconds;
        public string StartedUtc => data.startedUtc;
        public long DefeatedEnemies => data.defeatedEnemies;
        public long MaximumDamage => data.maximumDamage;
        public long BreedingAttempts => data.breedingAttempts;
        public long BreedingSuccesses => data.breedingSuccesses;
        public long BreedingFailures => data.breedingFailures;
        public IReadOnlyList<BreedingHistoryRecord> History => data.history;
        public IReadOnlyList<GoalRecord> Goals => data.goals;

        private static GameJournalData NewData() => new GameJournalData { startedUtc = DateTime.UtcNow.ToString("o") };
        private void Awake() { Instance = this; }
        private void Start() { BindSave(); }
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (save != null) save.Saved -= Saved;
        }
        private void BindSave()
        {
            if (save != null) return;
            save = GetComponent<SaveManager>();
            if (save != null) save.Saved += Saved;
        }
        private void Saved() { if (capturedRevision == revision) pending = false; }
        private void Update()
        {
            if (!paused && focused && Time.timeScale > 0) AddPlayTime(Time.unscaledDeltaTime);
            if (!pending || Time.unscaledTime < saveAt) return;
            BindSave(); saveAt = Time.unscaledTime + 1f;
            if (save != null) save.SaveGame();
        }
        private void OnApplicationPause(bool value) { paused = value; }
        private void OnApplicationFocus(bool value) { focused = value; }
        internal void AddPlayTime(double delta)
        {
            if (double.IsNaN(delta) || double.IsInfinity(delta) || delta <= 0) return;
            data.playSeconds = Math.Min(1e12, data.playSeconds + Math.Min(delta, 1));
        }
        // Combat accounting is allocation-free and uses the existing periodic save.
        public void RecordDefeat() { data.defeatedEnemies = LongMath.SaturatingAdd(data.defeatedEnemies, 1); }
        public void RecordDamage(long damage, MaterialDropSourceInfo source)
        {
            if (source == null || source.sourceType < MaterialDropSourceType.Player || source.sourceType > MaterialDropSourceType.Party3) return;
            if (damage > data.maximumDamage) data.maximumDamage = damage;
        }
        public static JournalMonster Snapshot(OwnedMonsterRecord record, MonsterCatalogSO catalog)
        {
            if (record == null) return null;
            MonsterSO species = catalog != null ? catalog.GetMonster(record.monsterId) : null;
            TitleSO title = catalog != null ? catalog.GetTitle(record.titleId) : null;
            return new JournalMonster { monsterId = record.monsterId, name = species != null ? species.displayName : record.monsterId,
                title = title != null ? title.displayName : record.titleId, level = record.level };
        }
        public void RecordBreeding(JournalMonster a, JournalMonster b, OwnedMonsterRecord child,
            BreedingMaterialOffer offer, bool succeeded, bool protectedParents, MonsterCatalogSO catalog, SkillCatalogSO skills)
        {
            if (a == null || b == null || offer == null) return;
            var entry = new BreedingHistoryRecord { utc = DateTime.UtcNow.ToString("o"), parentA = a, parentB = b,
                child = Snapshot(child, catalog), succeeded = succeeded, successPercent = offer.successPercent,
                specialRecipe = offer.isSpecial, parentsProtected = !succeeded && protectedParents };
            if (child?.inheritedSkillIds != null) foreach (string id in child.inheritedSkillIds)
            {
                if (entry.inheritedSkills.Count >= 3) break;
                SkillSO skill = skills != null ? skills.Get(id) : null;
                entry.inheritedSkills.Add(skill != null ? skill.displayName : id);
            }
            data.history.Insert(0, entry);
            if (data.history.Count > HistoryLimit) data.history.RemoveRange(HistoryLimit, data.history.Count - HistoryLimit);
            data.breedingAttempts = LongMath.SaturatingAdd(data.breedingAttempts, 1);
            if (succeeded) data.breedingSuccesses = LongMath.SaturatingAdd(data.breedingSuccesses, 1);
            else data.breedingFailures = LongMath.SaturatingAdd(data.breedingFailures, 1);
            QueueSave();
        }
        public bool SetGoal(GoalKind kind, string id, string name, long count)
        {
            if (!Enum.IsDefined(typeof(GoalKind), kind) || string.IsNullOrWhiteSpace(id) || count < 1 || count > MaximumTarget) return false;
            GoalRecord goal = data.goals.Find(x => x.kind == kind && string.Equals(x.definitionId, id, StringComparison.OrdinalIgnoreCase));
            if (goal == null)
            {
                if (data.goals.Count >= GoalLimit) return false;
                goal = new GoalRecord { goalId = Guid.NewGuid().ToString("N"), kind = kind, definitionId = id };
                data.goals.Add(goal);
            }
            goal.name = string.IsNullOrWhiteSpace(name) ? id : name;
            goal.targetCount = count; QueueSave(); return true;
        }
        public bool SetGoalTracked(string goalId, bool visible)
        {
            GoalRecord goal = data.goals.Find(x => x.goalId == goalId);
            if (goal == null) return false;
            if (goal.showOnHud == visible) return true;
            int tracked = 0;
            for (int i = 0; i < data.goals.Count; i++) if (data.goals[i].showOnHud) tracked++;
            if (visible && tracked >= TrackedGoalLimit) return false;
            goal.showOnHud = visible; QueueSave(); return true;
        }
        public bool DeleteGoal(string goalId)
        {
            int index = data.goals.FindIndex(x => x.goalId == goalId);
            if (index < 0) return false;
            data.goals.RemoveAt(index); QueueSave(); return true;
        }
        private void QueueSave() { revision++; pending = true; saveAt = Time.unscaledTime + 1f; Changed?.Invoke(); }
        public GameJournalData Capture() { capturedRevision = revision; return Copy(data); }
        public void Restore(GameJournalData source)
        {
            data = Copy(source); pending = false; revision++; Changed?.Invoke();
        }
        private static JournalMonster CopyMonster(JournalMonster x) => x == null ? null : new JournalMonster
            { monsterId = x.monsterId, name = x.name, title = x.title, level = Math.Max(1, x.level) };
        private static GameJournalData Copy(GameJournalData source)
        {
            if (source == null) return NewData();
            var result = NewData();
            if (DateTime.TryParse(source.startedUtc, out _)) result.startedUtc = source.startedUtc;
            result.playSeconds = double.IsNaN(source.playSeconds) || double.IsInfinity(source.playSeconds) ? 0 : Math.Max(0, Math.Min(1e12, source.playSeconds));
            result.defeatedEnemies = Math.Max(0, source.defeatedEnemies); result.maximumDamage = Math.Max(0, source.maximumDamage);
            result.breedingAttempts = Math.Max(0, source.breedingAttempts); result.breedingSuccesses = Math.Max(0, source.breedingSuccesses); result.breedingFailures = Math.Max(0, source.breedingFailures);
            if (source.history != null) foreach (var x in source.history)
            {
                if (result.history.Count >= HistoryLimit) break;
                if (x == null || x.parentA == null || x.parentB == null) continue;
                var entry = new BreedingHistoryRecord { utc = x.utc, parentA = CopyMonster(x.parentA), parentB = CopyMonster(x.parentB), child = CopyMonster(x.child),
                    succeeded = x.succeeded, specialRecipe = x.specialRecipe, parentsProtected = x.parentsProtected, successPercent = Mathf.Clamp(x.successPercent, 0, 100) };
                if (x.inheritedSkills != null) foreach (string skill in x.inheritedSkills)
                    if (!string.IsNullOrWhiteSpace(skill) && entry.inheritedSkills.Count < 3) entry.inheritedSkills.Add(skill);
                result.history.Add(entry);
            }
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ids = new HashSet<string>();
            int tracked = 0;
            if (source.goals != null) foreach (var x in source.goals)
            {
                if (result.goals.Count >= GoalLimit) break;
                if (x == null || !Enum.IsDefined(typeof(GoalKind), x.kind) || string.IsNullOrWhiteSpace(x.definitionId) || !targets.Add(x.kind + ":" + x.definitionId)) continue;
                string id = x.goalId;
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) { id = Guid.NewGuid().ToString("N"); ids.Add(id); }
                bool showOnHud = x.showOnHud && tracked < TrackedGoalLimit;
                if (showOnHud) tracked++;
                result.goals.Add(new GoalRecord { goalId = id, kind = x.kind, definitionId = x.definitionId, name = x.name, showOnHud = showOnHud, targetCount = Math.Max(1, Math.Min(MaximumTarget, x.targetCount)) });
            }
            return result;
        }
    }
}
