using System;
using System.Collections.Generic;
using MixMaster.Core;
using MixMaster.Monsters;
using MixMaster.UI;
using UnityEngine;

namespace MixMaster.Combat
{
    [DisallowMultipleComponent]
    public sealed class SkillActor : MonoBehaviour
    {
        private sealed class Status
        {
            public string key;
            public SkillAffix effect;
            public float until, next;
            public int stacks = 1;
            public long tickDamage;
            public MaterialDropSourceInfo source;
        }
        public static readonly List<SkillActor> Active = new List<SkillActor>();
        public static SkillActor PlayerActor { get; private set; }
        public static bool ApplyingSkill { get; private set; }
        private readonly List<Status> statuses = new List<Status>();
        private readonly Dictionary<string, float> readyAt = new Dictionary<string, float>();
        private Dictionary<string,float> Cooldowns => !IsEnemy && !string.IsNullOrEmpty(ownerId) && SkillProgressionManager.Instance!=null ? SkillProgressionManager.Instance.Cooldowns(ownerId) : readyAt;
        private readonly Dictionary<int, float> intervals = new Dictionary<int, float>();
        private readonly Dictionary<int, int> hitsAt = new Dictionary<int, int>(), damageAt = new Dictionary<int, int>(), killsAt = new Dictionary<int, int>();
        private int normalHits, damageEvents, kills, lastAttackSequence;
        private int ruleRevision = -1;
        private int pendingSlot = -1;
        private PlayerManager player;
        private PlayerAutoAttack playerCombat;
        private PartyMemberCombat party;
        private EnemyHealth enemy;
        private CharacterStats enemyBase = new CharacterStats();
        private MonsterSO species;
        private string ownerId;
        private float nextCheck, globalUntil, finishAt, confusedAttackAt;
        private SkillSO casting;
        private SkillActor castTarget;
        private SpriteRenderer bodySprite;
        private int cachedFrame = -1;
        private CharacterStats cachedStats, cachedBase;
        public string OwnerId => ownerId;
        public bool IsEnemy => enemy != null;
        public bool IsPlayer => playerCombat != null;
        // Aim at the upper body for single-ally support. Area centers stay on the ground.
        public Vector3 SkillEffectPosition(SkillSO skill)
        {
            if (skill == null ||
                (skill.targetType != SkillTargetType.SingleAlly && skill.targetType != SkillTargetType.Self) ||
                (skill.application != SkillApplication.SelectedTarget && skill.application != SkillApplication.Self))
                return transform.position;
            if (bodySprite == null)
                bodySprite = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
            if (bodySprite == null || bodySprite.sprite == null || !bodySprite.enabled)
                return transform.position + Vector3.up * .5f;
            Bounds bounds = bodySprite.bounds;
            return new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * .8f, transform.position.z);
        }
        public bool Alive => isActiveAndEnabled && (enemy != null ? enemy.IsAlive : party != null ? party.IsAlive : player != null && player.CurrentHp > 0);
        public long Hp => enemy != null ? enemy.CurrentHp : party != null ? party.CurrentHp : player != null ? player.CurrentHp : 0;
        public long Mp => enemy != null ? enemy.CurrentMp : party != null ? party.CurrentMp : player != null ? player.CurrentMp : 0;
        public CharacterStats Stats
        {
            get
            {
                if (enemy != null) { enemyBase.maxHp=enemy.MaxHp;enemyBase.maxMp=enemy.MaxMp;enemyBase.defense=enemy.BaseDefense;enemyBase.magicDefense=enemy.BaseMagicDefense; }
                return Effective(enemy != null ? enemyBase : party != null ? party.BaseStats : player != null ? player.UnmodifiedStats : new CharacterStats());
            }
        }
        public bool MovementBlocked => Has(SkillAilment.Freeze) || Has(SkillAilment.Paralysis) || Has(SkillAilment.Sleep);
        public bool NormalAttackBlocked => isActiveAndEnabled && (MovementBlocked || Has(SkillAilment.Confusion) || (casting != null && Time.time < finishAt));
        public bool SkillBlocked => MovementBlocked || Has(SkillAilment.Silence);
        public float MovementRatio => Stats.moveSpeed / Mathf.Max(0.1f, enemy != null ? enemyBase.moveSpeed : party != null ? party.BaseStats.moveSpeed : player.UnmodifiedStats.moveSpeed);
        public static SkillActor Ensure(GameObject go)
        {
            var actor=go.GetComponent<SkillActor>()??go.AddComponent<SkillActor>();
            actor.enabled=true;actor.ResolveReferences();return actor;
        }
        private void ResolveReferences()
        {
            party=GetComponent<PartyMemberCombat>();enemy=party==null?GetComponent<EnemyHealth>():null;playerCombat=GetComponent<PlayerAutoAttack>();
            if(playerCombat!=null){player=FindFirstObjectByType<PlayerManager>();ownerId="player";PlayerActor=this;}
        }
        private void Awake()
        {
            ResolveReferences();
        }
        private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); if(IsPlayer)PlayerActor=this; }
        public void ResetTransient() { statuses.Clear(); casting=null; pendingSlot=-1; cachedFrame=-1; }
        private void OnDisable() { Active.Remove(this); ResetTransient(); if (PlayerActor == this) PlayerActor = null; }
        public void Configure(MonsterSO monster, string individualId, CharacterStats enemyStats = null)
        {
            ResolveReferences();ResetTransient();
            species = monster; ownerId = individualId;
            if (enemyStats != null) enemyBase = ReincarnationRules.CopyStats(enemyStats);
            statuses.Clear(); readyAt.Clear(); intervals.Clear(); hitsAt.Clear(); damageAt.Clear(); killsAt.Clear(); casting = null; cachedFrame = -1;
            if (IsEnemy && species != null)
                foreach (var s in species.enemySkills) if (s != null) readyAt[s.skillId] = Time.time + s.cooldown;
        }
        public bool Has(SkillAilment ailment) => statuses.Exists(s => s.effect.ailment == ailment && s.until > Time.time);
        public CharacterStats Effective(CharacterStats original)
        {
            if(!IsEnemy&&SkillProgressionManager.Instance!=null)
                original=SkillPassiveRules.Apply(original,t=>SkillProgressionManager.Instance.PassiveValue(ownerId,t));
            if (statuses.Count == 0) return original;
            if (cachedFrame == Time.frameCount && cachedBase == original) return cachedStats;
            cachedFrame = Time.frameCount; cachedBase = original; cachedStats = ReincarnationRules.CopyStats(original);
            var percent = new double[8]; var flat = new double[8];
            foreach (var s in statuses)
            {
                var e = s.effect;
                if (s.until <= Time.time || (e.type != SkillAffixType.Buff && e.type != SkillAffixType.Debuff)) continue;
                double change = e.amount * s.stacks * (e.type == SkillAffixType.Debuff ? -1 : 1);
                if (e.change == SkillChange.Percent) percent[(int)e.stat] += change; else flat[(int)e.stat] += change;
            }
            double Value(SkillStat stat, double value, double min) => SkillMath.Change(value, percent[(int)stat], flat[(int)stat], min);
            cachedStats.attack = SkillMath.Amount(Value(SkillStat.Attack, original.attack, 0));
            cachedStats.magic = SkillMath.Amount(Value(SkillStat.Magic, original.magic, 0));
            cachedStats.defense = SkillMath.Amount(Value(SkillStat.Defense, original.defense, 0));
            cachedStats.magicDefense = SkillMath.Amount(Value(SkillStat.MagicDefense, original.magicDefense, 0));
            cachedStats.moveSpeed = (float)Math.Min(float.MaxValue, Value(SkillStat.MoveSpeed, original.moveSpeed, 0.1));
            cachedStats.attackSpeed = (float)Math.Min(float.MaxValue, Value(SkillStat.AttackSpeed, original.attackSpeed, 0.1));
            cachedStats.attackRange = (float)Math.Min(float.MaxValue, Value(SkillStat.AttackRange, original.attackRange, 0.1));
            return cachedStats;
        }
        public void NotifyDamage(bool fromSkill)
        {
            statuses.RemoveAll(s => s.effect.ailment == SkillAilment.Sleep); cachedFrame = -1;
            if (!fromSkill) damageEvents++;
        }
        public static SkillActor FromSource(MaterialDropSourceInfo source)
        {
            if (source == null) return null;
            if(source.sourceType==MaterialDropSourceType.Unknown)return null;
            return source.sourceType == MaterialDropSourceType.Player ? PlayerActor : Active.Find(a => !a.IsEnemy && a.ownerId == source.ownedMonsterUniqueId);
        }
        public static bool AllowNormalHit(MaterialDropSourceInfo source)
        {
            if (source == null || source.isSkill) return true;
            var a = FromSource(source);
            return a == null || (!a.MovementBlocked && (!a.Has(SkillAilment.Blind) || UnityEngine.Random.value < 0.5f));
        }
        public void NotifyNormalHit(MaterialDropSourceInfo source, bool killed)
        {
            if (source == null || source.isSkill || ApplyingSkill) return;
            if (lastAttackSequence != source.attackSequence) { normalHits++; lastAttackSequence = source.attackSequence; }
            if (killed) kills++;
        }
        public MaterialDropSourceInfo Source()
        {
            var result = IsPlayer ? MaterialDropSourceInfo.Player(Stats.dropRateBonus) : party != null ? MaterialDropSourceInfo.Party(party.PartySlot, Stats.dropRateBonus, ownerId) : MaterialDropSourceInfo.Unknown();
            result.isSkill = true; return result;
        }
        private void Update()
        {
            player?.ClampSkillResources();party?.ClampSkillResources();
            if (Time.timeScale <= 0) return;
            if (!Alive) { statuses.Clear(); casting = null; return; }
            foreach (var s in statuses.ToArray())
            {
                if (!statuses.Contains(s)) continue;
                if (s.effect.interval > 0 && s.next <= Time.time && s.next <= s.until)
                {
                    // Missed frames are processed on the next frame, never in an unbounded loop.
                    s.next += s.effect.interval;
                    DealRaw(SkillMath.Amount((double)s.tickDamage * s.stacks), s.source);
                    if (!Alive) { statuses.Clear(); break; }
                }
                if (Time.time >= s.until && (s.effect.interval <= 0 || s.next > s.until)) { statuses.Remove(s); cachedFrame = -1; }
            }
            if (!Alive) return;
            if (casting != null)
            {
                if (SkillBlocked || !ValidTarget(casting, castTarget)) { casting = null; pendingSlot=-1; return; }
                if (Time.time >= finishAt) { var s = casting; var t = castTarget; casting = null; if(Commit(s, t)&&pendingSlot>=0)ConsumeCounter(pendingSlot);pendingSlot=-1; }
                return;
            }
            if (Has(SkillAilment.Confusion) && !MovementBlocked && Time.time >= confusedAttackAt)
            {
                confusedAttackAt = Time.time + 1f / Mathf.Max(0.1f, Stats.attackSpeed);
                var targets = Candidates(false, Stats.attackRange, true);
                if (targets.Count > 0) { var target=targets[UnityEngine.Random.Range(0, targets.Count)];target.DealRaw(DamageCalculator.Calculate(Stats.attack, target.Stats.defense), Source()); }
            }
            if (Time.time < nextCheck || SkillBlocked) return;
            nextCheck = Time.time + 0.2f;
            if (IsEnemy)
            {
                if (species == null) return;
                foreach (var s in species.enemySkills) if (s != null && TryCast(s, out _)) break;
                return;
            }
            var manager = SkillProgressionManager.Instance;
            if (manager == null || string.IsNullOrEmpty(ownerId) || (party != null && party.CombatMode == PartyCombatMode.NoAttack)) return;
            var loadout=manager.Loadout(ownerId);
            if(ruleRevision!=loadout.revision)
            {
                ruleRevision=loadout.revision;normalHits=damageEvents=kills=0;hitsAt.Clear();damageAt.Clear();killsAt.Clear();intervals.Clear();
            }
            var slots = loadout.slots;
            var order = new List<int>(); for (int i = 0; i < slots.Count; i++) order.Add(i);
            order.Sort((a,b) => slots[a].priority != slots[b].priority ? slots[b].priority.CompareTo(slots[a].priority) : a.CompareTo(b));
            foreach (int i in order)
            {
                var slot = slots[i];
                if (slot.trigger == SkillTrigger.Manual) continue;
                var s = manager.Catalog != null ? manager.Catalog.Get(slot.skillId) : null;
                var target = s != null ? SelectTarget(s) : null;
                if (!Condition(slot.trigger, slot.threshold, i, target) || !Condition(slot.andCondition, slot.andThreshold, i, target)) continue;
                if (s != null && TryCast(s, out _)) { if(casting!=null)pendingSlot=i;else ConsumeCounter(i);break; }
            }
        }
        private void ConsumeCounter(int i){hitsAt[i]=normalHits;damageAt[i]=damageEvents;killsAt[i]=kills;intervals[i]=Time.time;}
        private bool Condition(SkillTrigger trigger, float threshold, int index, SkillActor target)
        {
            int Previous(Dictionary<int,int> d) => d.TryGetValue(index, out int n) ? n : 0;
            switch (trigger)
            {
                case SkillTrigger.Manual: case SkillTrigger.Ready: return true;
                case SkillTrigger.SelfHpBelow: return (double)Hp / Math.Max(1, Stats.maxHp) * 100 <= threshold;
                case SkillTrigger.SelfMpAbove: return Stats.maxMp > 0 && (double)Mp / Stats.maxMp * 100 >= threshold;
                case SkillTrigger.TargetHpBelow: return target != null && (double)target.Hp / Math.Max(1, target.Stats.maxHp) * 100 <= threshold;
                case SkillTrigger.EnemyCount: return Candidates(false, Stats.attackRange, false).Count >= Math.Max(1, threshold);
                case SkillTrigger.NormalHits: return normalHits - Previous(hitsAt) >= Math.Max(1, threshold);
                case SkillTrigger.DamageTaken: return damageEvents - Previous(damageAt) >= Math.Max(1, threshold);
                case SkillTrigger.Kill: return kills - Previous(killsAt) >= Math.Max(1, threshold);
                case SkillTrigger.Interval:
                    if (!intervals.ContainsKey(index)) intervals[index] = Time.time;
                    return Time.time - intervals[index] >= Mathf.Max(0.2f, threshold);
                default: return false;
            }
        }
        public float Remaining(SkillSO s) => s != null && Cooldowns.TryGetValue(s.skillId, out float end) ? Mathf.Max(0, end - Time.time) : 0;
        public bool TryCast(SkillSO s, out string message)
        {
            message = "使用できません。";
            if (s == null || s.category == SkillCategory.Passive || !s.executable || !Alive || SkillBlocked || casting != null || Time.timeScale <= 0) return false;
            if (!IsEnemy)
            {
                var m = SkillProgressionManager.Instance;
                if (m == null || !m.Learned(ownerId, s.skillId)) { message = "未習得です。"; return false; }
                bool equipped = m.Loadout(ownerId).slots.Exists(x => x.skillId == s.skillId);
                if (!equipped) { message = "使用枠に登録してください。"; return false; }
            }
            else if (species == null || !species.enemySkills.Contains(s)) return false;
            if (Remaining(s) > 0 || Time.time < globalUntil) { message = "再使用待ちです。"; return false; }
            if (Mp < s.mpCost) { message = "MPが足りません。"; return false; }
            var target = SelectTarget(s);
            if (!ValidTarget(s, target)) { message = "射程内に対象がいません。"; return false; }
            if (s.castTime > 0) { casting = s; castTarget = target; finishAt = Time.time + s.castTime; }
            else if (!Commit(s, target)) return false;
            message = s.displayName + "を発動しました。"; return true;
        }
        private bool ValidTarget(SkillSO s, SkillActor target) => target != null && target.Alive && target.gameObject.scene == gameObject.scene &&
            (target == this || Vector2.Distance(transform.position, target.transform.position) <= s.range) &&
            (target == this || Has(SkillAilment.Confusion) || (target.IsEnemy == IsEnemy) == (s.targetType == SkillTargetType.SingleAlly));
        private bool Commit(SkillSO s, SkillActor target)
        {
            if (!Alive || SkillBlocked || !ValidTarget(s, target) || Remaining(s) > 0 || Mp < s.mpCost) return false;
            // Set the gate before resource events fire, preventing reentrant double casts.
            Cooldowns[s.skillId] = Time.time + s.cooldown; globalUntil = Time.time + Mathf.Max(0.02f, s.globalCooldown);
            bool paid = enemy != null ? enemy.TrySpendMp(s.mpCost) : party != null ? party.TrySpendMp(s.mpCost) : player.TrySpendMp(s.mpCost);
            if (!paid) { Cooldowns.Remove(s.skillId); globalUntil = Time.time; return false; }
            if (s.projectile) SkillProjectile.Launch(this, target, s, Stats, Source());
            else Execute(s, target, target.transform.position, ReincarnationRules.CopyStats(Stats), Source());
            WorldUIManager.ShowSkillName(party != null ? (Component)party : enemy, s.displayName);
            return true;
        }
        private List<SkillActor> Candidates(bool allies, float range, bool everyone)
        {
            var result = new List<SkillActor>();
            foreach (var a in Active)
                if (a != null && a.Alive && a.gameObject.scene == gameObject.scene && (everyone ? a != this : (a.IsEnemy == IsEnemy) == allies) && Vector2.Distance(transform.position, a.transform.position) <= range) result.Add(a);
            result.Sort((a,b) => { int d = ((a.transform.position-transform.position).sqrMagnitude).CompareTo((b.transform.position-transform.position).sqrMagnitude); return d != 0 ? d : a.GetInstanceID().CompareTo(b.GetInstanceID()); });
            return result;
        }
        private SkillActor SelectTarget(SkillSO s)
        {
            if (s.targetType == SkillTargetType.Self || s.targetType == SkillTargetType.AreaAroundSelf) return this;
            bool ally = s.targetType == SkillTargetType.SingleAlly;
            var options = Candidates(ally, s.range, Has(SkillAilment.Confusion));
            if (options.Count == 0) return null;
            if (Has(SkillAilment.Confusion)) return options[UnityEngine.Random.Range(0, options.Count)];
            if (ally) options.Sort((a,b) => ((double)a.Hp/Math.Max(1,a.Stats.maxHp)).CompareTo((double)b.Hp/Math.Max(1,b.Stats.maxHp)));
            return options[0];
        }
        public bool ProjectileTargetAllowed(SkillSO skill,SkillActor candidate)
        {
            if(candidate==null||!candidate.Alive||candidate.gameObject.scene!=gameObject.scene)return false;
            if(skill.targetType==SkillTargetType.Self||skill.targetType==SkillTargetType.AreaAroundSelf)return candidate==this;
            if(Has(SkillAilment.Confusion))return candidate!=this;
            return (candidate.IsEnemy==IsEnemy)==(skill.targetType==SkillTargetType.SingleAlly);
        }
        public void Execute(SkillSO s, SkillActor selected, Vector3 center, CharacterStats powerStats, MaterialDropSourceInfo source, HashSet<SkillActor> affected = null, float powerScale = 1, float areaRadiusOverride = -1)
        {
            var targets = new List<SkillActor>();
            if (s.application == SkillApplication.Self && areaRadiusOverride<0) targets.Add(this);
            else if (s.application == SkillApplication.SelectedTarget && areaRadiusOverride<0) { if (selected != null && selected.Alive) targets.Add(selected); }
            else
            {
                bool allies=s.application==SkillApplication.AlliesInArea||(areaRadiusOverride>=0&&(s.targetType==SkillTargetType.SingleAlly||s.application==SkillApplication.Self));
                foreach (var a in Active)
                    if (a != null && a.Alive && a.gameObject.scene == gameObject.scene && (Has(SkillAilment.Confusion) || ((a.IsEnemy == IsEnemy) == allies)) && Vector2.Distance(center,a.transform.position) <= (areaRadiusOverride>=0?areaRadiusOverride:s.radius)) targets.Add(a);
                targets.Sort((a,b) => { int d=(a.transform.position-center).sqrMagnitude.CompareTo((b.transform.position-center).sqrMagnitude); return d!=0?d:a.GetInstanceID().CompareTo(b.GetInstanceID()); });
                if (areaRadiusOverride<0&&s.hitLimit > 0 && targets.Count > s.hitLimit) targets.RemoveRange(s.hitLimit,targets.Count-s.hitLimit);
            }
            foreach (var target in targets)
            {
                if (target == null || !target.Alive) continue;
                if (affected!=null&&!affected.Add(target))continue;
                if (s.effect == SkillBaseEffect.Damage && Has(SkillAilment.Blind) && UnityEngine.Random.value >= 0.5f) continue;
                long stat = s.powerSource == SkillPowerSource.Attack ? powerStats.attack : s.powerSource == SkillPowerSource.Magic ? powerStats.magic : s.powerSource == SkillPowerSource.MaxHp ? powerStats.maxHp : 0;
                long amount = SkillMath.Amount((stat * (double)s.powerMultiplier + s.flatPower)*Math.Max(0,powerScale));
                if (s.effect == SkillBaseEffect.Damage)
                {
                    amount = DamageCalculator.Calculate(amount, s.powerSource == SkillPowerSource.Attack ? target.Stats.defense : target.Stats.magicDefense);
                    var element = s.useOwnerElement ? powerStats.element : s.overrideElement;
                    amount = SkillMath.Amount(amount * (1d - (target.Stats.resistances?.Get(element) ?? 0)));
                    if (s.canCritical && UnityEngine.Random.value < powerStats.criticalRate) amount = SkillMath.Amount(amount * (double)Mathf.Max(1,powerStats.criticalMultiplier));
                    target.DealRaw(amount, source);
                }
                else if (s.effect == SkillBaseEffect.Heal) target.Heal(amount);
                if (target.Alive)
                    for (int i=0;i<s.affixes.Length;i++) target.AddStatus(s.skillId+":"+i,s.affixes[i],source);
                if (s.effectPrefab != null) Destroy(Instantiate(s.effectPrefab,target.SkillEffectPosition(s),Quaternion.identity),5f);
            }
        }
        public void AddStatus(string key, SkillAffix e, MaterialDropSourceInfo source)
        {
            if (e == null || e.type == SkillAffixType.None || !Alive || UnityEngine.Random.value * 100 >= e.chance) return;
            var active = statuses.Find(x => x.key == key && x.until > Time.time);
            if (active != null)
            {
                if (e.stacking == SkillStacking.Ignore) return;
                if (e.stacking == SkillStacking.Stack) active.stacks = Math.Min(e.stackLimit,active.stacks+1);
                active.until = Time.time + e.duration; cachedFrame=-1;
                DamagePopupText.ShowStatus(transform.position,e,active.stacks); return;
            }
            statuses.RemoveAll(x=>x.key==key);
            statuses.Add(new Status { key=key,effect=e,until=Time.time+e.duration,next=Time.time+e.interval,
                tickDamage=SkillMath.Amount(e.change==SkillChange.MaxHpPercent?Stats.maxHp*e.amount/100:e.amount),source=source?.Clone() }); cachedFrame=-1;
            DamagePopupText.ShowStatus(transform.position,e);
        }
        private void Heal(long amount) { if (enemy != null) enemy.Heal(amount); else if (party != null) party.Heal(amount); else player?.Heal(amount); }
        public void DealRaw(long amount, MaterialDropSourceInfo source)
        {
            if (!Alive || amount <= 0) return;
            bool previous = ApplyingSkill; ApplyingSkill = true;
            try
            {
                if (enemy != null) enemy.TakeDamage(amount,source);
                else if (party != null) party.TakeDamage(amount);
                else { player.TakeDamage(amount); DamagePopupText.Show(transform.position,amount,true); }
            }
            finally { ApplyingSkill = previous; }
        }
    }
}




