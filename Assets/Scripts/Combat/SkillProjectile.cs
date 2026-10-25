using MixMaster.Core;
using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Combat
{
    [DisallowMultipleComponent]
    public sealed class SkillProjectile : MonoBehaviour
    {
        [Header("CSVの指定が空欄なら、この設定を使用")]
        [SerializeField] private ProjectileMotion defaults = new ProjectileMotion();
        [InspectorName("回転させる見た目（空欄ならSpriteRenderer）")]
        [SerializeField] private Transform rotatingVisual;
        public ProjectileMotion Defaults => defaults;
        private ProjectileMotion motion;
        private SkillActor owner, target;
        private SkillSO skill;
        private CharacterStats power;
        private MaterialDropSourceInfo source;
        private ProjectilePath path;
        private float end;
        private float powerScale=1,explosionStart;
        private Vector3 baseScale;
        private bool exploding,hasSplit,forcedHoming;
        private int generation,jumps;
        private SpriteRenderer visual;
        private Color baseColor;
        private sealed class SpawnBudget { public int remaining=64; }
        private SpawnBudget budget;
        private readonly Dictionary<SkillActor,float> lastHits=new Dictionary<SkillActor,float>();
        private readonly List<SkillActor> hits=new List<SkillActor>();
        private readonly List<SkillActor> expiredHits=new List<SkillActor>();
        private readonly HashSet<SkillActor> collided = new HashSet<SkillActor>();
        private readonly HashSet<SkillActor> affected = new HashSet<SkillActor>();
        private static Sprite fallback;
        private static Texture2D fallbackTexture;
        public static ProjectileMotion ResolveMotion(SkillSO skill,ProjectileMotion prefabDefaults)
        {
            var result=(prefabDefaults??new ProjectileMotion()).Copy();
            if(skill.useProjectileMotionOverrides){if(skill.projectileMotionOverrides!=null)foreach(var option in skill.projectileMotionOverrides)result.Apply(option);}
            else result.homing=skill.homing;
            return result;
        }
        public static void Launch(SkillActor owner, SkillActor target, SkillSO skill, CharacterStats power, MaterialDropSourceInfo source)
        {
            Spawn(owner,target,skill,power,source,owner.transform.position,(target.transform.position-owner.transform.position).normalized,0,1,new SpawnBudget(),null);
        }
        private static SkillProjectile Spawn(SkillActor owner,SkillActor target,SkillSO skill,CharacterStats power,MaterialDropSourceInfo source,Vector3 position,Vector3 direction,int generation,float scale,SpawnBudget budget,SkillActor exclude)
        {
            if(budget.remaining--<=0)return null;
            var prefab=skill.projectilePrefab!=null?skill.projectilePrefab:Resources.Load<GameObject>("Skills/CommonSkillProjectile");
            var go=prefab!=null?Instantiate(prefab,position,Quaternion.identity):new GameObject("SkillProjectile_"+skill.skillId);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,owner.gameObject.scene);
            go.transform.position=position;
            var renderer=go.GetComponentInChildren<SpriteRenderer>();
            if(renderer==null)renderer=go.AddComponent<SpriteRenderer>();
            if (fallback == null) fallback = SwordSpriteUtility.CreateDefault(out fallbackTexture, true);
            // An authored sprite/tint is preserved; the skill icon belongs to the UI.
            if(renderer.sprite==null)renderer.sprite=fallback;
            if(prefab==null){renderer.sortingOrder=60;go.transform.localScale=Vector3.one*0.2f;}
            var p=go.GetComponent<SkillProjectile>()??go.AddComponent<SkillProjectile>();
            p.motion=ResolveMotion(skill,p.defaults);
            p.owner=owner;p.target=target;p.skill=skill;p.power=ReincarnationRules.CopyStats(power);p.source=source;
            p.path=new ProjectilePath(position,direction,skill.projectileSpeed,p.motion);p.collided.Clear();p.affected.Clear();p.lastHits.Clear();
            if(exclude!=null){p.collided.Add(exclude);p.affected.Add(exclude);p.lastHits[exclude]=Time.time;}
            p.end=Time.time+skill.projectileLifetime;p.baseScale=go.transform.localScale;p.visual=renderer;p.baseColor=renderer.color;
            p.generation=generation;p.powerScale=scale;p.budget=budget;p.hasSplit=p.exploding=p.forcedHoming=false;p.jumps=0;
            return p;
        }
        private void Update()
        {
            if (Time.timeScale<=0) return;
            if(skill==null||motion==null)return; // A prefab can also be previewed without being launched.
            if (owner==null || !owner.isActiveAndEnabled) {Destroy(gameObject);return;}
            if(exploding)
            {
                float t=Mathf.Clamp01((Time.time-explosionStart)/Mathf.Max(.02f,motion.explosionDuration));
                transform.localScale=baseScale*Mathf.Lerp(path.Scale,Mathf.Max(path.Scale,motion.explosionRadius/Mathf.Max(.01f,motion.hitRadius)),t);
                if(visual!=null){var color=baseColor;color.a*=1-t;visual.color=color;}
                if(t>=1)Destroy(gameObject);return;
            }
            if(Time.time>=end){Destroy(gameObject);return;}
            bool targetValid=target!=null&&target.Alive&&target.gameObject.scene==gameObject.scene;
            if(!targetValid||collided.Contains(target)){target=Nearest(transform.position,float.MaxValue);targetValid=target!=null;}
            path.Step(motion,Time.deltaTime,owner.transform.position,targetValid?target.transform.position:transform.position,targetValid,forcedHoming);
            Vector3 next=path.Position;
            Vector3 segment=next-transform.position;
            hits.Clear();
            if(path.AllowHits)foreach(var candidate in SkillActor.Active)
            {
                if(!owner.ProjectileTargetAllowed(skill,candidate))continue;
                if(lastHits.TryGetValue(candidate,out float last)&&(!motion.repeatHits||Time.time-last<Mathf.Max(.02f,motion.repeatInterval)))continue;
                Vector3 delta=candidate.transform.position-transform.position;
                float t=segment.sqrMagnitude>0?Mathf.Clamp01(Vector3.Dot(delta,segment)/segment.sqrMagnitude):0;
                float radius=motion.hitRadius*path.Scale;
                if((transform.position+segment*t-candidate.transform.position).sqrMagnitude<=radius*radius)hits.Add(candidate);
            }
            if(hits.Count>1){Vector3 hitSegment=segment;hits.Sort((a,b)=>Vector3.Dot(a.transform.position-transform.position,hitSegment).CompareTo(Vector3.Dot(b.transform.position-transform.position,hitSegment)));}
            foreach(var hit in hits)
            {
                if(hit==null||!hit.Alive)continue;
                if(motion.repeatHits)
                {
                    expiredHits.Clear();
                    foreach(var old in affected)
                        if(old==null||!lastHits.TryGetValue(old,out float last)||Time.time-last>=Mathf.Max(.02f,motion.repeatInterval))expiredHits.Add(old);
                    foreach(var old in expiredHits)affected.Remove(old);
                }
                collided.Add(hit);lastHits[hit]=Time.time;
                owner.Execute(skill,hit,hit.transform.position,power,source,affected,powerScale*path.Power);
                foreach(var a in affected)if(!lastHits.TryGetValue(a,out float previous)||Time.time-previous>=Mathf.Max(.02f,motion.repeatInterval))lastHits[a]=Time.time;
                Split(hit);
                if(motion.explodeOnHit||path.CurrentMode==SkillProjectileMoveType.ProximityMine){transform.position=hit.transform.position;Explode();return;}
                if(motion.chain&&jumps<Mathf.Max(0,motion.chainCount))
                {
                    var following=Nearest(hit.transform.position,motion.chainRadius);
                    if(following!=null){jumps++;powerScale*=Mathf.Max(0,motion.chainPower);target=following;forcedHoming=true;Vector3 dir=(following.transform.position-hit.transform.position).normalized;path.Redirect(hit.transform.position+dir*.25f,dir);transform.position=path.Position;return;}
                }
                if(!motion.piercing&&!motion.repeatHits){Destroy(gameObject);return;}
            }
            transform.position=next;
            transform.localScale=baseScale*path.Scale;
            if(path.StoppedNow&&motion.explodeOnStop){Explode();return;}
            if(path.Finished){Destroy(gameObject);return;}
            Transform rotating=rotatingVisual!=null?rotatingVisual:visual!=null?visual.transform:transform;
            if(!motion.spin||rotating!=transform)
            {var facing=segment.sqrMagnitude>.000001f?segment.normalized:path.Direction;transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(facing.y,facing.x)*Mathf.Rad2Deg-90);}
            if(motion.spin)rotating.Rotate(0,0,motion.spinSpeed*Time.deltaTime);
        }
        private SkillActor Nearest(Vector3 origin,float radius)
        {
            SkillActor best=null;float distance=radius*radius;
            foreach(var a in SkillActor.Active)
            {
                if(collided.Contains(a)||!owner.ProjectileTargetAllowed(skill,a))continue;
                float d=(a.transform.position-origin).sqrMagnitude;
                if(d<distance){distance=d;best=a;}
            }
            return best;
        }
        private void Split(SkillActor hit)
        {
            if(!motion.split||hasSplit||generation>=Mathf.Clamp(motion.splitDepth,0,3))return;hasSplit=true;
            int count=Mathf.Clamp(motion.splitCount,1,16);
            for(int i=0;i<count;i++)
            {
                float angle=(count==1?0:-motion.splitAngle*.5f+motion.splitAngle*i/(count-1))*Mathf.Deg2Rad;
                Vector3 forward=path.Direction;var dir=new Vector3(forward.x*Mathf.Cos(angle)-forward.y*Mathf.Sin(angle),forward.x*Mathf.Sin(angle)+forward.y*Mathf.Cos(angle),0);
                Spawn(owner,target,skill,power,source,hit.transform.position+dir*.35f,dir,generation+1,powerScale*Mathf.Max(0,motion.splitPower),budget,hit);
            }
        }
        private void Explode()
        {
            if(exploding)return;exploding=true;explosionStart=Time.time;
            owner.Execute(skill,null,transform.position,power,source,affected,powerScale*path.Power,Mathf.Max(.01f,motion.explosionRadius));
        }
    }
}
