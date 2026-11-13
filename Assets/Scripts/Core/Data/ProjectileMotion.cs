using System;
using UnityEngine;

namespace MixMaster.Core
{
    public enum ProjectileMotionOption
    {
        Homing, Piercing, Spin, SpinSpeed, Wave, WaveWidth, WaveFrequency,
        Accelerate, Acceleration, MaximumSpeed, HitRadius,
        MoveType, TurnSpeed, GrowSpeed, MaximumScale, OutboundTime, ReturnSpeed,
        OrbitTime, OrbitRadius, OrbitAngularSpeed, DelayedHomingTime, SpiralAngularSpeed, SpiralGrowth,
        PingPongDistance, Reversals, DestroyAfterReversals, FollowOwner, RepeatHits, RepeatInterval,
        ChargeTime, ChargeScale, ChargePower, TimedPhase, PhaseTime, PhaseType, PhaseSpeed,
        Chain, ChainCount, ChainRadius, ChainPower, Split, SplitCount, SplitAngle, SplitPower, SplitDepth,
        ExplodeOnHit, ExplodeOnStop, ExplosionRadius, ExplosionDuration
    }
    public enum SkillProjectileMoveType
    {
        [InspectorName("直進")] Straight,
        [InspectorName("追尾")] Homing,
        [InspectorName("拡大直進")] GrowStraight,
        [InspectorName("その場")] Stay,
        [InspectorName("ブーメラン")] Boomerang,
        [InspectorName("周回後に射出")] OrbitLaunch,
        [InspectorName("蛇行")] SineWave,
        [InspectorName("加速")] Accelerate,
        [InspectorName("減速停止")] DecelerateStop,
        [InspectorName("遅延追尾")] DelayedHoming,
        [InspectorName("螺旋")] Spiral,
        [InspectorName("往復")] PingPong,
        [InspectorName("感応地雷")] ProximityMine,
        [InspectorName("収束成長")] ChargeGrow,
        [InspectorName("爆発")] Burst
    }
    [Serializable]
    public sealed class ProjectileMotionOverride
    {
        public ProjectileMotionOption option;
        public float value;
    }
    [Serializable]
    public sealed class ProjectileMotion
    {
        [InspectorName("移動タイプ")] public SkillProjectileMoveType moveType;
        [InspectorName("追尾")] public bool homing;
        [InspectorName("貫通")] public bool piercing;
        [InspectorName("スプライト回転")] public bool spin;
        [InspectorName("回転速度（度／秒）")] public float spinSpeed = 360;
        [InspectorName("蛇行")] public bool wave;
        [InspectorName("蛇行幅")] [Min(0)] public float waveWidth = 0.3f;
        [InspectorName("蛇行回数（回／秒）")] [Min(0)] public float waveFrequency = 2;
        [InspectorName("加速・減速")] public bool accelerate;
        [InspectorName("加速度")] public float acceleration = 3;
        [InspectorName("最高速度")] [Min(0.01f)] public float maximumSpeed = 50;
        [InspectorName("命中半径")] [Min(0.01f)] public float hitRadius = 0.3f;
        [Header("特殊移動")]
        [InspectorName("追尾旋回速度")] public float turnSpeed = 8;
        [InspectorName("拡大速度（倍率／秒）")] public float growSpeed = 0.8f;
        [InspectorName("最大拡大倍率")] public float maximumScale = 2;
        [InspectorName("ブーメラン直進秒")] public float outboundTime = 0.8f;
        [InspectorName("帰還速度")] public float returnSpeed = 12;
        [InspectorName("周回秒")] public float orbitTime = 0.8f;
        [InspectorName("周回半径")] public float orbitRadius = 1.5f;
        [InspectorName("周回角速度")] public float orbitAngularSpeed = 720;
        [InspectorName("追尾開始秒")] public float delayedHomingTime = 0.6f;
        [InspectorName("螺旋角速度")] public float spiralAngularSpeed = 540;
        [InspectorName("螺旋半径拡大速度")] public float spiralGrowth = 1.8f;
        [InspectorName("往復距離")] public float pingPongDistance = 4;
        [InspectorName("反転回数")] public int reversals = 4;
        [InspectorName("反転終了で消滅")] public bool destroyAfterReversals = true;
        [InspectorName("その場型が使用者に追従")] public bool followOwner;
        [InspectorName("同じ相手に繰り返し命中")] public bool repeatHits;
        [InspectorName("再命中間隔秒")] public float repeatInterval = 0.5f;
        [InspectorName("収束時間秒")] public float chargeTime = 1.5f;
        [InspectorName("収束時の最大拡大倍率")] public float chargeScale = 2.5f;
        [InspectorName("収束時の最大威力倍率")] public float chargePower = 2.5f;
        [Header("時間で移動タイプ切替")]
        [InspectorName("切替を使う")] public bool timedPhase;
        [InspectorName("切替秒")] public float phaseTime = 1;
        [InspectorName("切替後の移動タイプ")] public SkillProjectileMoveType phaseType = SkillProjectileMoveType.Homing;
        [InspectorName("切替後の速度")] public float phaseSpeed = 10;
        [Header("命中時のオプション")]
        [InspectorName("連鎖")] public bool chain;
        [InspectorName("最大連鎖回数")] public int chainCount = 4;
        [InspectorName("連鎖の検索半径")] public float chainRadius = 8;
        [InspectorName("連鎖ごとの威力倍率")] public float chainPower = 0.85f;
        [InspectorName("分裂")] public bool split;
        [InspectorName("子弾数")] public int splitCount = 2;
        [InspectorName("分裂角度")] public float splitAngle = 50;
        [InspectorName("子弾の威力倍率")] public float splitPower = 0.5f;
        [InspectorName("分裂世代数（1なら子弾は分裂なし）")] public int splitDepth = 1;
        [InspectorName("命中時に爆発")] public bool explodeOnHit;
        [InspectorName("減速停止時に爆発")] public bool explodeOnStop = true;
        [InspectorName("爆発半径")] public float explosionRadius = 2;
        [InspectorName("爆発表示秒")] public float explosionDuration = 0.4f;
        public ProjectileMotion Copy() => (ProjectileMotion)MemberwiseClone();
        public void Apply(ProjectileMotionOverride entry)
        {
            if(entry==null)return;
            float v=entry.value;
            switch(entry.option)
            {
                case ProjectileMotionOption.Homing:homing=v!=0;break;
                case ProjectileMotionOption.Piercing:piercing=v!=0;break;
                case ProjectileMotionOption.Spin:spin=v!=0;break;
                case ProjectileMotionOption.SpinSpeed:spinSpeed=v;break;
                case ProjectileMotionOption.Wave:wave=v!=0;break;
                case ProjectileMotionOption.WaveWidth:waveWidth=Mathf.Max(0,v);break;
                case ProjectileMotionOption.WaveFrequency:waveFrequency=Mathf.Max(0,v);break;
                case ProjectileMotionOption.Accelerate:accelerate=v!=0;break;
                case ProjectileMotionOption.Acceleration:acceleration=v;break;
                case ProjectileMotionOption.MaximumSpeed:maximumSpeed=Mathf.Max(.01f,v);break;
                case ProjectileMotionOption.HitRadius:hitRadius=Mathf.Max(.01f,v);break;
                case ProjectileMotionOption.MoveType:moveType=(SkillProjectileMoveType)(int)v;break;
                case ProjectileMotionOption.TurnSpeed:turnSpeed=v;break;
                case ProjectileMotionOption.GrowSpeed:growSpeed=v;break;
                case ProjectileMotionOption.MaximumScale:maximumScale=v;break;
                case ProjectileMotionOption.OutboundTime:outboundTime=v;break;
                case ProjectileMotionOption.ReturnSpeed:returnSpeed=v;break;
                case ProjectileMotionOption.OrbitTime:orbitTime=v;break;
                case ProjectileMotionOption.OrbitRadius:orbitRadius=v;break;
                case ProjectileMotionOption.OrbitAngularSpeed:orbitAngularSpeed=v;break;
                case ProjectileMotionOption.DelayedHomingTime:delayedHomingTime=v;break;
                case ProjectileMotionOption.SpiralAngularSpeed:spiralAngularSpeed=v;break;
                case ProjectileMotionOption.SpiralGrowth:spiralGrowth=v;break;
                case ProjectileMotionOption.PingPongDistance:pingPongDistance=v;break;
                case ProjectileMotionOption.Reversals:reversals=(int)v;break;
                case ProjectileMotionOption.DestroyAfterReversals:destroyAfterReversals=v!=0;break;
                case ProjectileMotionOption.FollowOwner:followOwner=v!=0;break;
                case ProjectileMotionOption.RepeatHits:repeatHits=v!=0;break;
                case ProjectileMotionOption.RepeatInterval:repeatInterval=v;break;
                case ProjectileMotionOption.ChargeTime:chargeTime=v;break;
                case ProjectileMotionOption.ChargeScale:chargeScale=v;break;
                case ProjectileMotionOption.ChargePower:chargePower=v;break;
                case ProjectileMotionOption.TimedPhase:timedPhase=v!=0;break;
                case ProjectileMotionOption.PhaseTime:phaseTime=v;break;
                case ProjectileMotionOption.PhaseType:phaseType=(SkillProjectileMoveType)(int)v;break;
                case ProjectileMotionOption.PhaseSpeed:phaseSpeed=v;break;
                case ProjectileMotionOption.Chain:chain=v!=0;break;
                case ProjectileMotionOption.ChainCount:chainCount=(int)v;break;
                case ProjectileMotionOption.ChainRadius:chainRadius=v;break;
                case ProjectileMotionOption.ChainPower:chainPower=v;break;
                case ProjectileMotionOption.Split:split=v!=0;break;
                case ProjectileMotionOption.SplitCount:splitCount=(int)v;break;
                case ProjectileMotionOption.SplitAngle:splitAngle=v;break;
                case ProjectileMotionOption.SplitPower:splitPower=v;break;
                case ProjectileMotionOption.SplitDepth:splitDepth=(int)v;break;
                case ProjectileMotionOption.ExplodeOnHit:explodeOnHit=v!=0;break;
                case ProjectileMotionOption.ExplodeOnStop:explodeOnStop=v!=0;break;
                case ProjectileMotionOption.ExplosionRadius:explosionRadius=v;break;
                case ProjectileMotionOption.ExplosionDuration:explosionDuration=v;break;
            }
        }
        // Integrate exactly up to the speed cap/stop time to avoid frame-rate-dependent travel.
        public float Travel(ref float speed,float seconds,float? forceAcceleration=null)
        {
            if(seconds<=0)return 0;
            float rate=forceAcceleration??acceleration;
            if((!accelerate&&!forceAcceleration.HasValue)||rate==0)return speed*seconds;
            double start=Math.Min(maximumSpeed,Math.Max(0,speed));
            double limit=rate>0?maximumSpeed:0;
            double changeTime=Math.Max(0,(limit-start)/rate);
            double changing=Math.Min(seconds,changeTime);
            double finish=start+rate*changing;
            double distance=(start+finish)*.5*changing+finish*(seconds-changing);
            speed=(float)finish;return(float)distance;
        }
        public Vector3 Offset(Vector3 direction,float age) => !wave ? Vector3.zero :
            new Vector3(-direction.y,direction.x,0)*waveWidth*Mathf.Sin(age*Mathf.PI*2*waveFrequency);
    }
}

