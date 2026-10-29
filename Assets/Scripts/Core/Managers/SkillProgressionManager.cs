using System;
using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    public sealed class SkillProgressionManager : MonoBehaviour
    {
        public static SkillProgressionManager Instance { get; private set; }
        public const int SlotCount = 5;
        private SkillSaveData data = new SkillSaveData();
        private readonly Dictionary<string, Dictionary<string,float>> runtimeCooldowns = new Dictionary<string,Dictionary<string,float>>();
        public Dictionary<string,float> Cooldowns(string owner)
        {
            if (!runtimeCooldowns.TryGetValue(owner,out var times)) { times=new Dictionary<string,float>();runtimeCooldowns[owner]=times; }
            return times;
        }
        public SkillCatalogSO Catalog { get; private set; }
        public event Action Changed;
        private bool pending;
        private float saveAt;
        public int Revision { get; private set; }
        private sealed class PassiveSummary
        {
            public int frame = -1, revision = -1, attackStyle = -1;
            public bool affectsStats;
            public readonly double[] values = new double[(int)SkillPassiveType.RespawnSpeed + 1];
            public readonly HashSet<string> seen = new HashSet<string>();
        }
        private readonly Dictionary<string, PassiveSummary> passiveSummaries = new Dictionary<string, PassiveSummary>();
        private void Awake() { Instance = this; Catalog = SkillCatalogSO.Load(); }
        private void OnDestroy() { if (Instance == this) Instance = null; }
        private void Update()
        {
            if (!pending || Time.unscaledTime < saveAt) return;
            var save = GetComponent<SaveManager>();
            pending = save != null && !save.SaveGame(); saveAt = Time.unscaledTime + 1;
        }
        public void NotifyChanged() { Revision++; pending = true; saveAt = Time.unscaledTime + 0.35f; Changed?.Invoke(); }
        private OwnedMonsterRecord Record(string owner) => GetComponent<MonsterManager>()?.FindOwnedMonster(owner);
        public IReadOnlyList<SkillSO> Available(string owner)
        {
            if (Catalog == null) return Array.Empty<SkillSO>();
            if (owner == "player")
            {
                var p = GetComponent<PlayerManager>();
                return p != null ? ReincarnationSkills(p.ReincarnationSpecies, Catalog) : Array.Empty<SkillSO>();
            }
            var record = Record(owner);
            var monster = record != null ? MonsterCatalogSO.Load()?.GetMonster(record.monsterId) : null;
            var available=new List<SkillSO>();if(monster!=null)available.AddRange(monster.availableSkills);
            if(record?.inheritedSkillIds!=null)foreach(var id in record.inheritedSkillIds){var s=Catalog.Get(id);if(s!=null&&!available.Contains(s))available.Add(s);}
            if(record?.learnedSkillIds!=null)foreach(var id in record.learnedSkillIds){var s=Catalog.Get(id);if(s!=null&&!available.Contains(s))available.Add(s);}
            return available;
        }
        // Previewing a book and learning after reincarnation share the same pool.
        // Enemy-only skills are intentionally outside this list.
        public static IReadOnlyList<SkillSO> ReincarnationSkills(MonsterSO species, SkillCatalogSO catalog)
        {
            if (catalog == null) return Array.Empty<SkillSO>();
            return (IReadOnlyList<SkillSO>)(species != null ? species.availableSkills : catalog.humanSkills)
                ?? Array.Empty<SkillSO>();
        }
        public bool Learned(string owner, string id) => owner == "player" ? data.playerLearned.Contains(id) : Record(owner)?.learnedSkillIds?.Contains(id) == true;
        public int Points(string owner) => owner == "player" ? GetComponent<PlayerManager>().SkillPoints : Record(owner)?.skillPoints ?? 0;
        public bool Learn(string owner, string id, out string message)
        {
            message = "習得できません。";
            var skill = Catalog != null ? Catalog.Get(id) : null;
            bool available = false;
            foreach (var s in Available(owner)) if (s == skill) available = true;
            if (skill == null || !skill.executable || !available || Learned(owner, id)) return false;
            var record = Record(owner); var p = GetComponent<PlayerManager>();
            int level = owner == "player" ? p.Level : record?.level ?? 0;
            if (level < skill.requiredLevel) { message = "必要レベルに達していません。"; return false; }
            if (Points(owner) < skill.skillPointCost) { message = "スキルポイントが足りません。"; return false; }
            if (owner == "player") { if (!p.TrySpendSkillPoints(skill.skillPointCost)) return false; data.playerLearned.Add(id); }
            else { record.skillPoints -= skill.skillPointCost; record.spentSkillPoints = ReincarnationRules.TotalSkillPoints(record.spentSkillPoints, skill.skillPointCost); record.learnedSkillIds ??= new List<string>(); record.learnedSkillIds.Add(id); }
            NotifyChanged(); message = skill.displayName + "を習得しました。"; return true;
        }
        public SkillLoadout Loadout(string owner)
        {
            var result = data.loadouts.Find(l => l != null && l.ownerId == owner);
            if (result == null) { result = new SkillLoadout { ownerId = owner }; data.loadouts.Add(result); }
            result.slots ??= new List<SkillSlot>();
            while (result.slots.Count < SlotCount) result.slots.Add(new SkillSlot());
            return result;
        }
        public void SetSlot(string owner, int index, SkillSlot slot)
        {
            if (index < 0 || index >= SlotCount || slot == null || (!string.IsNullOrEmpty(slot.skillId) && !Learned(owner, slot.skillId))) return;
            if(owner!="player"&&slot.trigger==SkillTrigger.Manual)slot.trigger=SkillTrigger.Ready;
            var l = Loadout(owner);
            for (int i = 0; i < l.slots.Count; i++) if (i != index && l.slots[i].skillId == slot.skillId) l.slots[i] = new SkillSlot();
            l.slots[index] = slot; l.revision++; NotifyChanged();
        }

        internal bool ValidatePresetSkills(string owner, IReadOnlyList<SkillSlot> slots, out string error)
        {
            error = "スキル設定が不正です。";
            if (slots == null || slots.Count != SlotCount) return false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (SkillSlot slot in slots)
            {
                if (slot == null) return false;
                if (!Enum.IsDefined(typeof(SkillTrigger), slot.trigger) ||
                    !Enum.IsDefined(typeof(SkillTrigger), slot.andCondition) ||
                    float.IsNaN(slot.threshold) || float.IsInfinity(slot.threshold) || slot.threshold < 0f ||
                    float.IsNaN(slot.andThreshold) || float.IsInfinity(slot.andThreshold) || slot.andThreshold < 0f) return false;
                if (string.IsNullOrWhiteSpace(slot.skillId)) continue;
                SkillSO skill = Catalog != null ? Catalog.Get(slot.skillId) : null;
                if (skill == null || !skill.executable) { error = "スキルが見つかりません：" + slot.skillId; return false; }
                if (!Learned(owner, slot.skillId)) { error = "未習得のスキル：" + skill.displayName; return false; }
                if (!seen.Add(slot.skillId)) { error = "同じスキルが複数の枠に設定されています。"; return false; }
            }
            error = string.Empty;
            return true;
        }

        // Caller validates every owner before any of these silent mutations.
        // Keep learned skills, points and all runtime cooldown timestamps.
        internal bool ApplyPresetSkills(IReadOnlyList<PartyPresetActor> actors)
        {
            var changedOwners = new HashSet<string>(StringComparer.Ordinal);
            foreach (PartyPresetActor actor in actors)
            {
                SkillLoadout loadout = Loadout(actor.ownerId);
                var slots = new List<SkillSlot>(SlotCount);
                foreach (SkillSlot saved in actor.skills)
                {
                    SkillSlot slot = PartyPresetData.CopySkill(saved);
                    if (actor.ownerId != "player" && slot.trigger == SkillTrigger.Manual) slot.trigger = SkillTrigger.Ready;
                    slots.Add(slot);
                }
                bool changed = loadout.slots.Count != slots.Count;
                for (int i = 0; !changed && i < slots.Count; i++)
                {
                    SkillSlot a = loadout.slots[i], b = slots[i];
                    changed = a == null || a.skillId != b.skillId || a.trigger != b.trigger || a.threshold != b.threshold ||
                        a.andCondition != b.andCondition || a.andThreshold != b.andThreshold || a.priority != b.priority;
                }
                if (!changed) continue;
                loadout.slots = slots;
                loadout.revision++;
                changedOwners.Add(actor.ownerId);
            }
            foreach (var actor in MixMaster.Combat.SkillActor.Active)
                if (actor != null && changedOwners.Contains(actor.OwnerId)) actor.CancelPresetCast();
            return changedOwners.Count > 0;
        }
        public double PassiveValue(string owner, SkillPassiveType type)
        {
            if(string.IsNullOrEmpty(owner))return 0;
            int index = (int)type;
            var summary = GetPassiveSummary(owner);
            return index >= 0 && index < summary.values.Length ? summary.values[index] : 0;
        }
        public bool HasStatPassives(string owner) => !string.IsNullOrEmpty(owner) && GetPassiveSummary(owner).affectsStats;
        private PassiveSummary GetPassiveSummary(string owner)
        {
            if (!passiveSummaries.TryGetValue(owner, out var result))
                passiveSummaries[owner] = result = new PassiveSummary();
            if (result.frame == Time.frameCount && result.revision == Revision) return result;
            result.frame = Time.frameCount; result.revision = Revision;
            result.attackStyle = -1; result.affectsStats = false;
            Array.Clear(result.values, 0, result.values.Length); result.seen.Clear();
            foreach(var slot in Loadout(owner).slots)
            {
                if(slot==null||string.IsNullOrEmpty(slot.skillId)||!Learned(owner,slot.skillId))continue;
                var skill=Catalog?.Get(slot.skillId);
                if(skill==null||skill.category!=SkillCategory.Passive||skill.passives==null)continue;
                bool first = result.seen.Add(slot.skillId);
                foreach(var p in skill.passives)
                {
                    if (p == null) continue;
                    int index = (int)p.type;
                    if (first && index >= 0 && index < result.values.Length) result.values[index] += p.amount;
                    if (p.type == SkillPassiveType.Melee) result.attackStyle = 0;
                    if (p.type == SkillPassiveType.Ranged) result.attackStyle = 1;
                }
            }
            for (int i = (int)SkillPassiveType.MaxHp; i <= (int)SkillPassiveType.AllResistance; i++)
                if (i != (int)SkillPassiveType.EquipmentDrop && result.values[i] != 0) result.affectsStats = true;
            return result;
        }
        public static double PartyBonus(SkillPassiveType type)
        {
            var m=Instance;if(m==null)return 0;
            double total=m.PassiveValue("player",type);
            var monsters=m.GetComponent<MonsterManager>();
            if(monsters!=null)foreach(var id in monsters.PartyMonsterUniqueIds)total+=m.PassiveValue(id,type);
            return total;
        }
        // Last occupied slot wins if melee and ranged are both equipped.
        public bool Ranged(string owner,bool fallback)
        {
            if(string.IsNullOrEmpty(owner))return fallback;
            int style = GetPassiveSummary(owner).attackStyle;
            return style < 0 ? fallback : style == 1;
        }
        public List<string> InheritanceCandidates(string a,string b)
        {
            var result=new List<string>();
            foreach(var owner in new[]{a,b})foreach(var slot in Loadout(owner).slots)
                if(slot!=null&&!string.IsNullOrEmpty(slot.skillId)&&Learned(owner,slot.skillId)&&Catalog?.Get(slot.skillId)!=null&&!result.Contains(slot.skillId))result.Add(slot.skillId);
            return result;
        }
        public void Inherit(OwnedMonsterRecord child,List<string> candidates)
        {
            if (child == null || candidates == null || Catalog == null) return;
            child.inheritedSkillIds = CopyInheritedSkills(child.inheritedSkillIds);
            var pool = new List<string>();
            foreach (string id in candidates)
            {
                var skill = string.IsNullOrWhiteSpace(id) ? null : Catalog.Get(id);
                if (skill != null && skill.executable && !pool.Contains(id) && !child.inheritedSkillIds.Contains(id)) pool.Add(id);
            }
            while (child.inheritedSkillIds.Count < 3 && pool.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, pool.Count);
                child.inheritedSkillIds.Add(pool[index]); pool.RemoveAt(index);
            }
            NotifyChanged();
        }
        internal static List<string> CopyInheritedSkills(IReadOnlyList<string> source)
        {
            var result = new List<string>();
            if (source != null) foreach (string id in source)
            {
                if (!string.IsNullOrWhiteSpace(id) && !result.Contains(id)) result.Add(id);
                if (result.Count == 3) break;
            }
            return result;
        }
        public void ResetPlayer() { data.playerLearned.Clear(); data.loadouts.RemoveAll(l => l.ownerId == "player"); runtimeCooldowns.Remove("player"); MixMaster.Combat.SkillActor.PlayerActor?.ResetTransient(); NotifyChanged(); }
        public SkillSaveData Capture() => JsonUtility.FromJson<SkillSaveData>(JsonUtility.ToJson(data));
        public void Restore(SkillSaveData saved)
        {
            Revision++; passiveSummaries.Clear();
            foreach(var actor in MixMaster.Combat.SkillActor.Active)if(actor!=null)actor.ResetTransient();
            data = saved == null ? new SkillSaveData() : JsonUtility.FromJson<SkillSaveData>(JsonUtility.ToJson(saved));
            data.playerLearned ??= new List<string>(); data.loadouts ??= new List<SkillLoadout>();
            runtimeCooldowns.Clear();
            data.loadouts.RemoveAll(l => l == null || string.IsNullOrEmpty(l.ownerId));
            foreach (var l in data.loadouts)
            {
                l.slots ??= new List<SkillSlot>();
                if (l.slots.Count > SlotCount) l.slots.RemoveRange(SlotCount, l.slots.Count - SlotCount);
                for (int i = 0; i < l.slots.Count; i++)
                    if (l.slots[i] == null || !Learned(l.ownerId, l.slots[i].skillId)) l.slots[i] = new SkillSlot();
                foreach (var slot in l.slots)
                {
                    var skill=Catalog!=null?Catalog.Get(slot.skillId):null;
                    if(skill!=null)Cooldowns(l.ownerId)[slot.skillId]=Time.time+skill.cooldown;
                }
            }
            pending = false; Changed?.Invoke();
        }
    }
}
