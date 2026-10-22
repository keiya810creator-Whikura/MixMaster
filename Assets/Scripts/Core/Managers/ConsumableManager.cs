using System;
using System.Collections.Generic;
using MixMaster.Combat;
using UnityEngine;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    public sealed class ConsumableManager : MonoBehaviour
    {
        public const int ShortcutCount = 5;
        public static ConsumableManager Instance { get; private set; }
        [SerializeField] private ConsumableCatalogSO catalog;
        [SerializeField, Min(0.1f)] private float autoUseInterval = 1f;
        private readonly Dictionary<string, long> inventory = new Dictionary<string, long>();
        private readonly string[] shortcuts = new string[ShortcutCount];
        private ConsumableAutoUseRule hpAutoUse = new ConsumableAutoUseRule { itemId = "hp_low" };
        private ConsumableAutoUseRule mpAutoUse = new ConsumableAutoUseRule { itemId = "mp_low" };
        private PlayerManager player;
        private GameManager game;
        private PlayerAutoAttack actor;
        private bool busy, savePending;
        private float saveAt, nextAutoUse;
        private double lureUntil;
        public event Action Changed;

        public ConsumableCatalogSO Catalog
        {
            get
            {
                if (catalog == null) catalog = Resources.Load<ConsumableCatalogSO>("Consumables/ConsumableCatalog");
                return catalog;
            }
        }
        public float LureSecondsRemaining => (float)Math.Max(0d, lureUntil - Time.realtimeSinceStartupAsDouble);
        public PlayerAutoAttack LureTarget
        {
            get
            {
                if (LureSecondsRemaining <= 0f) return null;
                if (actor == null) actor = FindFirstObjectByType<PlayerAutoAttack>();
                return actor != null && actor.isActiveAndEnabled && actor.IsAlive ? actor : null;
            }
        }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }
        private void Resolve()
        {
            if (player == null) player = GetComponent<PlayerManager>() ?? FindFirstObjectByType<PlayerManager>();
            if (game == null) game = GetComponent<GameManager>() ?? GameManager.Instance;
        }
        private void Update()
        {
            if (savePending && Time.unscaledTime >= saveAt) FlushSave();
            if (Time.unscaledTime < nextAutoUse || Time.timeScale <= 0f) return;
            nextAutoUse = Time.unscaledTime + Mathf.Max(0.1f, autoUseInterval);
            Resolve();
            if (player == null || player.CurrentHp <= 0) return;
            AutoUse(hpAutoUse, ConsumableEffect.RestoreHp, player.CurrentHp, player.Stats.maxHp);
            AutoUse(mpAutoUse, ConsumableEffect.RestoreMp, player.CurrentMp, player.Stats.maxMp);
        }
        private void AutoUse(ConsumableAutoUseRule rule, ConsumableEffect effect, long current, long maximum)
        {
            ConsumableDefinition item = Catalog != null ? Catalog.Get(rule.itemId) : null;
            if (!rule.enabled || item == null || item.effect != effect || maximum <= 0 || current >= maximum) return;
            if ((double)current / maximum * 100d <= rule.thresholdPercent) TryUse(item.itemId, out _);
        }
        public long Quantity(string id) => id != null && inventory.TryGetValue(id, out long n) ? n : 0;
        public string Shortcut(int index) => index >= 0 && index < ShortcutCount ? shortcuts[index] : null;
        public ConsumableAutoUseRule AutoUseRule(ConsumableEffect effect) =>
            (effect == ConsumableEffect.RestoreMp ? mpAutoUse : hpAutoUse).Copy();

        public bool TryPurchase(string id, int quantity, out string message)
        {
            message = "購入できません。";
            Resolve();
            ConsumableDefinition item = Catalog != null ? Catalog.Get(id) : null;
            if (busy || item == null || quantity <= 0 || game == null) return false;
            if (item.purchasePrice < 0) { message = "価格未設定です。"; return false; }
            if (item.purchasePrice > long.MaxValue / quantity || Quantity(id) > long.MaxValue - quantity)
            { message = "購入可能な上限を超えています。"; return false; }
            long cost = item.purchasePrice * quantity;
            if (game.Gold < cost) { message = "ゴールドが足りません。"; return false; }
            busy = true;
            try
            {
                if (!game.TrySpendGold(cost, false)) return false;
                inventory[id] = Quantity(id) + quantity;
                game.NotifyGoldChanged();
                Changed?.Invoke();
                RequestSave();
                message = item.displayName + "を" + quantity + "個購入しました。";
                return true;
            }
            finally { busy = false; }
        }
        public bool TryUse(string id, out string message)
        {
            message = "使用できません。";
            Resolve();
            ConsumableDefinition item = Catalog != null ? Catalog.Get(id) : null;
            if (busy || item == null || player == null || player.CurrentHp <= 0) return false;
            if (Quantity(id) <= 0) { message = "アイテムを所持していません。"; return false; }
            if (item.effect == ConsumableEffect.RestoreHp && (item.recoveryAmount <= 0 || player.CurrentHp >= player.Stats.maxHp))
            { message = "HPは満タンです。"; return false; }
            if (item.effect == ConsumableEffect.RestoreMp && (item.recoveryAmount <= 0 || player.CurrentMp >= player.Stats.maxMp))
            { message = "MPは満タンです。"; return false; }
            if (item.effect == ConsumableEffect.MonsterLure)
            {
                if (actor == null) actor = FindFirstObjectByType<PlayerAutoAttack>();
                if (actor == null || !actor.isActiveAndEnabled || !actor.IsAlive ||
                    item.durationSeconds <= 0 || float.IsNaN(item.durationSeconds) || float.IsInfinity(item.durationSeconds)) return false;
            }
            busy = true;
            try
            {
                inventory[id] = Quantity(id) - 1;
                if (item.effect == ConsumableEffect.RestoreHp) player.Heal(item.recoveryAmount);
                else if (item.effect == ConsumableEffect.RestoreMp) player.RecoverMp(item.recoveryAmount);
                else lureUntil = Time.realtimeSinceStartupAsDouble + item.durationSeconds;
                Changed?.Invoke();
                RequestSave();
                message = item.displayName + "を使用しました。";
                return true;
            }
            finally { busy = false; }
        }
        public bool SetShortcut(int slot, string id)
        {
            if (slot < 0 || slot >= ShortcutCount || (id != null && (Catalog == null || Catalog.Get(id) == null))) return false;
            if (id != null)
                for (int i = 0; i < shortcuts.Length; i++) if (shortcuts[i] == id) shortcuts[i] = null;
            shortcuts[slot] = id;
            Changed?.Invoke(); RequestSave(); return true;
        }
        public bool SetAutoUse(ConsumableEffect effect, bool enabled, string id, int percent)
        {
            ConsumableDefinition item = Catalog != null ? Catalog.Get(id) : null;
            if (effect == ConsumableEffect.MonsterLure || item == null || item.effect != effect) return false;
            ConsumableAutoUseRule rule = effect == ConsumableEffect.RestoreMp ? mpAutoUse : hpAutoUse;
            rule.enabled = enabled; rule.itemId = id; rule.thresholdPercent = Mathf.Clamp(percent, 0, 100);
            Changed?.Invoke(); RequestSave(); return true;
        }
        private void RequestSave() { savePending = true; saveAt = Time.unscaledTime + 0.35f; }
        private void FlushSave()
        {
            SaveManager save = GetComponent<SaveManager>() ?? FindFirstObjectByType<SaveManager>();
            if (save != null) savePending = !save.SaveGame();
            else savePending = false;
            saveAt = Time.unscaledTime + 1f;
        }
        public ConsumableSaveData Capture()
        {
            var data = new ConsumableSaveData { hpAutoUse = hpAutoUse.Copy(), mpAutoUse = mpAutoUse.Copy() };
            foreach (var pair in inventory) if (pair.Value > 0) data.inventory.Add(new ConsumableStack { itemId = pair.Key, quantity = pair.Value });
            data.shortcuts.AddRange(shortcuts);
            return data;
        }
        public void Restore(ConsumableSaveData data)
        {
            inventory.Clear(); Array.Clear(shortcuts, 0, shortcuts.Length);
            if (data != null && data.inventory != null)
                foreach (var stack in data.inventory)
                    if (stack != null && stack.quantity > 0 && Catalog != null && Catalog.Get(stack.itemId) != null)
                        inventory[stack.itemId] = LongMath.SaturatingAdd(Quantity(stack.itemId), stack.quantity);
            if (data != null && data.shortcuts != null)
                for (int i = 0; i < Math.Min(ShortcutCount, data.shortcuts.Count); i++)
                {
                    string id = data.shortcuts[i];
                    if (Catalog != null && Catalog.Get(id) != null && Array.IndexOf(shortcuts, id) < 0) shortcuts[i] = id;
                }
            hpAutoUse = NormalizeRule(data?.hpAutoUse, ConsumableEffect.RestoreHp, "hp_low");
            mpAutoUse = NormalizeRule(data?.mpAutoUse, ConsumableEffect.RestoreMp, "mp_low");
            lureUntil = 0; savePending = false;
            Changed?.Invoke();
        }
        private ConsumableAutoUseRule NormalizeRule(ConsumableAutoUseRule saved, ConsumableEffect effect, string fallback)
        {
            var rule = saved?.Copy() ?? new ConsumableAutoUseRule { itemId = fallback };
            ConsumableDefinition item = Catalog != null ? Catalog.Get(rule.itemId) : null;
            if (item == null || item.effect != effect) { rule.itemId = fallback; rule.enabled = false; }
            rule.thresholdPercent = Mathf.Clamp(rule.thresholdPercent, 0, 100);
            return rule;
        }
    }
}
