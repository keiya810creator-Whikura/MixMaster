namespace MixMaster.Core
{
    public sealed class CombatPerformanceRecord
    {
        public string ownerId;
        public string name;
        public long damage;
        public long healing;
        public float survivalSeconds;
        public bool survived;
        public double DamagePerSecond(float elapsed) => elapsed > 0 ? damage / (double)elapsed : 0;
    }
}
