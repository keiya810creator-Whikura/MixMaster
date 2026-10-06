using System;

namespace MixMaster.Core
{
    public enum ElementType
    {
        None,
        Fire,
        Water,
        Wind,
        Earth,
        Light,
        Dark
    }

    [Serializable]
    public class ElementResistanceSet
    {
        public double fire;
        public double water;
        public double wind;
        public double earth;
        public double light;
        public double dark;

        public double Get(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return fire;
                case ElementType.Water: return water;
                case ElementType.Wind: return wind;
                case ElementType.Earth: return earth;
                case ElementType.Light: return light;
                case ElementType.Dark: return dark;
                default: return 0d;
            }
        }
    }

    [Serializable]
    public class CharacterStats
    {
        public long maxHp = 100;
        public long attack = 10;
        public long magic = 10;
        public long defense = 5;
        public long magicDefense = 5;

        public double criticalRate = 0.05d;
        public double criticalMultiplier = 1.5d;

        public float moveSpeed = 5f;
        public float attackSpeed = 1f;
        public float attackRange = 1.5f;

        public ElementType element = ElementType.None;
        public ElementResistanceSet resistances = new ElementResistanceSet();
    }
}