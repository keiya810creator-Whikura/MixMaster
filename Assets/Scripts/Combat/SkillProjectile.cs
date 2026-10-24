using MixMaster.Core;
using UnityEngine;

namespace MixMaster.Combat
{
    public sealed class SkillProjectile : MonoBehaviour
    {
        private SkillActor owner, target;
        private SkillSO skill;
        private CharacterStats power;
        private MaterialDropSourceInfo source;
        private Vector3 direction;
        private float end;
        private static Sprite fallback;
        private static Texture2D fallbackTexture;
        public static void Launch(SkillActor owner, SkillActor target, SkillSO skill, CharacterStats power, MaterialDropSourceInfo source)
        {
            var go = new GameObject("SkillProjectile_"+skill.skillId);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,owner.gameObject.scene);
            go.transform.position=owner.transform.position;
            var renderer=go.AddComponent<SpriteRenderer>();
            if (fallback == null) fallback = SwordSpriteUtility.CreateDefault(out fallbackTexture, true);
            renderer.sprite=skill.icon!=null?skill.icon:fallback;
            renderer.color = skill.overrideElement == ElementType.Fire ? new Color(1f,0.35f,0.2f) : skill.overrideElement == ElementType.Water ? Color.cyan : Color.white;
            renderer.sortingOrder=60; go.transform.localScale=Vector3.one*0.2f;
            var p=go.AddComponent<SkillProjectile>();p.owner=owner;p.target=target;p.skill=skill;p.power=ReincarnationRules.CopyStats(power);p.source=source;
            p.end=Time.time+skill.projectileLifetime;p.direction=(target.transform.position-owner.transform.position).normalized;
        }
        private void Update()
        {
            if (Time.timeScale<=0) return;
            if (owner==null || !owner.isActiveAndEnabled || target==null || !target.Alive || target.gameObject.scene!=gameObject.scene || Time.time>=end) {Destroy(gameObject);return;}
            Vector3 delta=target.transform.position-transform.position;
            if(skill.homing)direction=delta.normalized;
            Vector3 next=transform.position+direction*skill.projectileSpeed*Time.deltaTime;
            Vector3 segment=next-transform.position;
            float t=segment.sqrMagnitude>0?Mathf.Clamp01(Vector3.Dot(delta,segment)/segment.sqrMagnitude):0;
            if((transform.position+segment*t-target.transform.position).sqrMagnitude<=0.09f)
            { owner.Execute(skill,target,target.transform.position,power,source);Destroy(gameObject);return; }
            transform.position=next;
        }
    }
}
