using UnityEngine;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    public sealed class SpawnManager : MonoBehaviour
    {
        /// <summary>
        /// Returns respawn delay after altar efficiency is applied.
        /// At max level the delay becomes zero, allowing immediate respawn.
        /// </summary>
        public float GetRespawnDelay(float baseDelaySeconds, int efficiencyLevel, int maxEfficiencyLevel)
        {
            if (baseDelaySeconds <= 0f) return 0f;
            if (maxEfficiencyLevel <= 0) return baseDelaySeconds;

            efficiencyLevel = Mathf.Clamp(efficiencyLevel, 0, maxEfficiencyLevel);
            if (efficiencyLevel >= maxEfficiencyLevel) return 0f;

            float t = (float)efficiencyLevel / maxEfficiencyLevel;
            return Mathf.Max(0f, baseDelaySeconds * (1f - t));
        }
    }
}