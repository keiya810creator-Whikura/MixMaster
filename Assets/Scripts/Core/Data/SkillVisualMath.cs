using UnityEngine;
namespace MixMaster.Core
{
    public static class SkillVisualMath
    {
        public static Vector3 FitDiameter(Vector3 spriteSize,Vector3 parentScale,float radius,float z=1)
        {
            float diameter=2*Mathf.Max(0,radius);
            return new Vector3(diameter/Mathf.Max(.00001f,Mathf.Abs(spriteSize.x*parentScale.x)),
                diameter/Mathf.Max(.00001f,Mathf.Abs(spriteSize.y*parentScale.y)),z);
        }
        public static float BurstScale(float progress,float start) => Mathf.Lerp(Mathf.Clamp(start,.01f,1),1,1-Mathf.Pow(1-Mathf.Clamp01(progress),3));
        public static float BurstAlpha(float progress) => 1-Mathf.Clamp01(progress);
    }
}
