using System;
using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Core
{
    public enum ConsumableEffect { RestoreHp, RestoreMp, MonsterLure }

    [Serializable]
    public sealed class ConsumableDefinition
    {
        public string itemId;
        public string displayName;
        public Sprite icon;
        public ConsumableEffect effect;
        [Min(0)] public long recoveryAmount;
        [Min(0.1f)] public float durationSeconds = 60f;
        [Tooltip("-1: 価格未設定で販売停止。0以上で販売します。")]
        [Min(-1)] public long purchasePrice = -1;
        public string Description => effect == ConsumableEffect.MonsterLure
            ? "マップ上の全魔物を" + durationSeconds.ToString("0") + "秒間引き寄せる"
            : (effect == ConsumableEffect.RestoreHp ? "HP" : "MP") + "を" + recoveryAmount.ToString("N0") + "回復";
    }

    [CreateAssetMenu(fileName = "ConsumableCatalog", menuName = "MixMaster/Data/Consumable Catalog")]
    public sealed class ConsumableCatalogSO : ScriptableObject
    {
        public List<ConsumableDefinition> items = new List<ConsumableDefinition>();
        public ConsumableDefinition Get(string id) => string.IsNullOrEmpty(id) || items == null
            ? null : items.Find(x => x != null && x.itemId == id);
    }

    [Serializable]
    public sealed class ConsumableStack
    {
        public string itemId;
        public long quantity;
    }

    [Serializable]
    public sealed class ConsumableAutoUseRule
    {
        public bool enabled;
        public string itemId;
        public int thresholdPercent = 50;
        public ConsumableAutoUseRule Copy() => new ConsumableAutoUseRule
        { enabled = enabled, itemId = itemId, thresholdPercent = thresholdPercent };
    }

    [Serializable]
    public sealed class ConsumableSaveData
    {
        public List<ConsumableStack> inventory = new List<ConsumableStack>();
        public List<string> shortcuts = new List<string>();
        public ConsumableAutoUseRule hpAutoUse = new ConsumableAutoUseRule { itemId = "hp_low" };
        public ConsumableAutoUseRule mpAutoUse = new ConsumableAutoUseRule { itemId = "mp_low" };
    }
}
