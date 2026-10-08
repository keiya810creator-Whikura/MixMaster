using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace MixMaster.Core
{
    public enum ElementType
    {
        None,
        Fire,
        Water,
        Wind,
        Lightning,
        Light,
        Dark
    }

    [Serializable]
    public class ElementResistanceSet
    {
        // -10.0 = -1000%, 0.8 = 80% damage reduction.
        public float fire;
        public float water;
        public float wind;
        [FormerlySerializedAs("earth")]
        public float lightning;
        public float light;
        public float dark;

        public float Get(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return fire;
                case ElementType.Water: return water;
                case ElementType.Wind: return wind;
                case ElementType.Lightning: return lightning;
                case ElementType.Light: return light;
                case ElementType.Dark: return dark;
                default: return 0f;
            }
        }

        public void ClampAll()
        {
            fire = Clamp(fire);
            water = Clamp(water);
            wind = Clamp(wind);
            lightning = Clamp(lightning);
            light = Clamp(light);
            dark = Clamp(dark);
        }

        public static float Clamp(float value)
        {
            return Mathf.Clamp(value, -10f, 0.8f);
        }
    }

    [Serializable]
    public class CharacterStats
    {
        public long maxHp = 100;
        public long maxMp = 100;

        public long attack = 10;
        public long magic = 10;
        public long defense = 5;
        public long magicDefense = 5;

        public float criticalRate = 0.05f;
        public float criticalMultiplier = 1.5f;

        public float moveSpeed = 5f;
        public float attackSpeed = 1f;
        public float attackRange = 1.5f;

        public ElementType element = ElementType.None;
        public ElementResistanceSet resistances = new ElementResistanceSet();

        // Only used by Player / allied monsters.
        // Additive decimal rate: 1.0 = +100%, 2.0 = +200%.
        public float dropRateBonus = 0f;
    }
}
