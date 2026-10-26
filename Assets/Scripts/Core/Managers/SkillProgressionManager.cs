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
        private void Awake() { Instance = this; Catalog = SkillCatalogSO.Load(); }
        private void OnDestroy() { if (Instance == this) Instance = null; }
        private void Update()
        {
            if (!pending || Time.unscaledTime < saveAt) return;
            var save = GetComponent<SaveManager>();
            pending = save != null && !save.SaveGame(); saveAt = Time.unscaledTime + 1;
        }
        public void NotifyChanged() { pending = true; saveAt = Time.unscaledTime + 0.35f; Changed?.Invoke(); }
        private OwnedMonsterRecord Record(string owner) => GetComponent<MonsterManager>()?.FindOwnedMonster(owner);
        public IReadOnlyList<SkillSO> Available(string owner)
        {
            if (Catalog == null) return Array.Empty<SkillSO>();
            if (owner == "player")
            {
                var p = GetComponent<PlayerManager>();
                return p.ReincarnationSpecies != null ? p.AvailableReincarnationSkills : Catalog.humanSkills;
            }
            var record = Record(owner);
            var monster = record != null ? MonsterCatalogSO.Load()?.GetMonster(record.monsterId) : null;
            var available=new List<SkillSO>();if(monster!=null)available.AddRange(monster.availableSkills);
            if(record?.learnedSkillIds!=null)foreach(var id in record.learnedSkillIds){var s=Catalog.Get(id);if(s!=null&&!available.Contains(s))available.Add(s);}
            return available;
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
        public double PassiveValue(string owner, SkillPassiveType type)
        {
            if(string.IsNullOrEmpty(owner))return 0;
            double total=0;var seen=new HashSet<string>();
            foreach(var slot in Loadout(owner).slots)
            {
                if(slot==null||string.IsNullOrEmpty(slot.skillId)||!seen.Add(slot.skillId)||!Learned(owner,slot.skillId))continue;
                var skill=Catalog?.Get(slot.skillId);
                if(skill==null||skill.category!=SkillCategory.Passive||skill.passives==null)continue;
                foreach(var p in skill.passives)if(p!=null&&p.type==type)total+=p.amount;
            }
            return total;
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
            foreach(var slot in Loadout(owner).slots)
            {
                if(slot==null||!Learned(owner,slot.skillId))continue;
                var s=Catalog?.Get(slot.skillId);if(s==null||s.category!=SkillCategory.Passive||s.passives==null)continue;
                foreach(var p in s.passives)if(p!=null){if(p.type==SkillPassiveType.Melee)fallback=false;if(p.type==SkillPassiveType.Ranged)fallback=true;}
            }
            return fallback;
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
            if(child==null)return;child.learnedSkillIds ??= new List<string>();
            for(int i=0;i<3&&candidates.Count>0;i++)
            {
                int n=UnityEngine.Random.Range(0,candidates.Count);string id=candidates[n];candidates.RemoveAt(n);
                if(!child.learnedSkillIds.Contains(id))child.learnedSkillIds.Add(id);
            }
            NotifyChanged();
        }
        public void ResetPlayer() { data.playerLearned.Clear(); data.loadouts.RemoveAll(l => l.ownerId == "player"); runtimeCooldowns.Remove("player"); MixMaster.Combat.SkillActor.PlayerActor?.ResetTransient(); NotifyChanged(); }
        public SkillSaveData Capture() => JsonUtility.FromJson<SkillSaveData>(JsonUtility.ToJson(data));
        public void Restore(SkillSaveData saved)
        {
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
