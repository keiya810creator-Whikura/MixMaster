using UnityEngine;
namespace MixMaster.Core
{
    // Per-instance state; never changes a prefab or a shared SkillSO.
    public sealed class ProjectilePath
    {
        public Vector3 Position,Direction;
        public float Scale=1,Power=1;
        public bool AllowHits=true,Finished,StoppedNow;
        private Vector3 origin,center;
        private float speed,elapsed,total,travel;
        private bool changed,orbitReleased,stopped;
        private SkillProjectileMoveType mode;
        public SkillProjectileMoveType CurrentMode => mode;
        public ProjectilePath(Vector3 position,Vector3 direction,float speed,ProjectileMotion settings)
        {Position=origin=center=position;Direction=direction.sqrMagnitude>0?direction.normalized:Vector3.up;this.speed=speed;mode=settings.moveType;}
        public void Redirect(Vector3 position,Vector3 direction)
        {Position=center=origin=position;Direction=direction.normalized;travel=0;}
        public void Step(ProjectileMotion m,float dt,Vector3 owner,Vector3 target,bool hasTarget,bool forcedHoming=false)
        {
            if(dt<=0||Finished)return;
            total+=dt;elapsed+=dt;StoppedNow=false;
            if(m.timedPhase&&!changed&&total>=m.phaseTime)
            {changed=true;mode=m.phaseType;speed=m.phaseSpeed;origin=center=Position;elapsed=dt;travel=0;orbitReleased=false;stopped=false;Scale=Power=1;}
            var current=forcedHoming?SkillProjectileMoveType.Homing:mode;
            AllowHits=current!=SkillProjectileMoveType.OrbitLaunch||elapsed>=m.orbitTime;
            bool stationary=current==SkillProjectileMoveType.Stay||current==SkillProjectileMoveType.ProximityMine||current==SkillProjectileMoveType.ChargeGrow;
            if(stationary)
            {
                Position=m.followOwner?owner:origin;
                if(current==SkillProjectileMoveType.ChargeGrow){float t=Mathf.Clamp01(elapsed/Mathf.Max(.01f,m.chargeTime));Scale=Mathf.Lerp(1,Mathf.Max(1,m.chargeScale),t);Power=Mathf.Lerp(1,Mathf.Max(1,m.chargePower),t);}
                return;
            }
            if(current==SkillProjectileMoveType.OrbitLaunch&&!orbitReleased)
            {
                if(elapsed<m.orbitTime){float angle=elapsed*m.orbitAngularSpeed*Mathf.Deg2Rad;Position=owner+new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*m.orbitRadius;center=Position;return;}
                orbitReleased=true;if(hasTarget)Direction=(target-Position).normalized;
            }
            if(current==SkillProjectileMoveType.Boomerang&&elapsed>=m.outboundTime)
            {
                Vector3 delta=owner-Position;float distance=Mathf.Sqrt(delta.sqrMagnitude);
                if(distance<=.25f){Finished=true;return;}
                Direction=delta.normalized;Position+=Direction*Mathf.Min(distance,m.returnSpeed*dt);center=Position;return;
            }
            if(current==SkillProjectileMoveType.Spiral)
            {float angle=elapsed*m.spiralAngularSpeed*Mathf.Deg2Rad;Position=origin+new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*(elapsed*m.spiralGrowth);return;}
            if(current==SkillProjectileMoveType.PingPong)
            {
                float length=Mathf.Max(.01f,m.pingPongDistance);travel+=speed*dt;int reversals=Mathf.FloorToInt(travel/length);
                if(m.destroyAfterReversals&&reversals>=Mathf.Max(1,m.reversals)){Position=origin+Direction*(m.reversals%2==0?0:length);Finished=true;return;}
                float cycle=travel%(2*length);Position=origin+Direction*(cycle<=length?cycle:2*length-cycle);return;
            }
            bool homing=current==SkillProjectileMoveType.Homing||(current==SkillProjectileMoveType.DelayedHoming&&elapsed>=m.delayedHomingTime)||(current==SkillProjectileMoveType.OrbitLaunch&&m.homing)||(current==SkillProjectileMoveType.Straight&&m.homing);
            if(homing&&hasTarget)
            {Vector3 wanted=(target-center).normalized;Direction=(Direction*(1-Mathf.Clamp01(m.turnSpeed*dt))+wanted*Mathf.Clamp01(m.turnSpeed*dt)).normalized;}
            float distanceStep;
            if(current==SkillProjectileMoveType.Accelerate||current==SkillProjectileMoveType.DecelerateStop)
            {
                float rate=current==SkillProjectileMoveType.DecelerateStop?-Mathf.Abs(m.acceleration):Mathf.Abs(m.acceleration);
                distanceStep=m.Travel(ref speed,dt,rate);
                if(current==SkillProjectileMoveType.DecelerateStop&&speed<=.001f&&!stopped){stopped=true;StoppedNow=true;}
            }
            else distanceStep=m.Travel(ref speed,dt);
            center+=Direction*distanceStep;
            if(current==SkillProjectileMoveType.SineWave)Position=center+new Vector3(-Direction.y,Direction.x,0)*m.waveWidth*Mathf.Sin(elapsed*Mathf.PI*2*m.waveFrequency);
            else Position=center+m.Offset(Direction,elapsed);
            if(current==SkillProjectileMoveType.GrowStraight)Scale=Mathf.Min(Mathf.Max(1,m.maximumScale),1+Mathf.Max(0,m.growSpeed)*elapsed);
        }
    }
}
