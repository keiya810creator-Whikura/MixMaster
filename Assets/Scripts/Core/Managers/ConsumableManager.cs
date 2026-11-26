using System;
using System.Collections.Generic;
using MixMaster.Combat;
using MixMaster.Player;
using MixMaster.World;
using UnityEngine;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    public sealed class ConsumableManager : MonoBehaviour
    {
        public const int ShortcutCount = 5;
        public const float RecoveryCooldownSeconds = 5f;
        private float recoveryReadyAt;
        public float RecoveryCooldownRemaining => Mathf.Max(0f, recoveryReadyAt - Time.time);
        public float CooldownRemaining(string id)
        {
            var item = Catalog != null ? Catalog.Get(id) : null;
            return item != null && (item.effect == ConsumableEffect.RestoreHp || item.effect == ConsumableEffect.RestoreMp)
                ? RecoveryCooldownRemaining : 0f;
        }
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
        private bool permanentLure;
        public bool PermanentLureActive => permanentLure && PurchaseManager.Instance?.UnlimitedTools == true;
        private int actorSearchFrame = -1;
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
                if (!PermanentLureActive && LureSecondsRemaining <= 0f) return null;
                return ResolveLureActor();
            }
        }
        private PlayerAutoAttack ResolveLureActor()
        {
            if (actor != null && actor.gameObject.activeInHierarchy && actor.IsAlive) return actor;
            // All active enemies query the bell. Search at most once per frame
            // while the player object is being replaced or temporarily absent.
            if (actorSearchFrame == Time.frameCount) return null;
            actorSearchFrame = Time.frameCount;
            PlayerController controller = FindFirstObjectByType<PlayerController>();
            actor = controller != null ? controller.GetComponent<PlayerAutoAttack>() : null;
            return actor != null && actor.IsAlive ? actor : null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            var purchases = GetComponent<PurchaseManager>();
            if (purchases != null) purchases.Changed += RefreshTools;
        }
        private void OnDestroy()
        {
            var purchases = GetComponent<PurchaseManager>();
            if (purchases != null) purchases.Changed -= RefreshTools;
            if (Instance == this) Instance = null;
        }
        private void RefreshTools()
        {
            if (!PermanentLureActive) permanentLure = false;
            Changed?.Invoke();
        }
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
        public long UsableQuantity(ConsumableDefinition item) => item.IsUnlimited
            ? (PurchaseManager.Instance?.UnlimitedTools == true ? 1L : 0L) : Quantity(item.itemId);
        public string Shortcut(int index) => index >= 0 && index < ShortcutCount ? shortcuts[index] : null;
        public ConsumableAutoUseRule AutoUseRule(ConsumableEffect effect) =>
            (effect == ConsumableEffect.RestoreMp ? mpAutoUse : hpAutoUse).Copy();

        public bool TryPurchase(string id, int quantity, out string message)
        {
            message = "購入できません。";
            Resolve();
            ConsumableDefinition item = Catalog != null ? Catalog.Get(id) : null;
            if (busy || item == null || item.IsUnlimited || quantity <= 0 || game == null) return false;
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
            if ((item.effect == ConsumableEffect.RestoreHp || item.effect == ConsumableEffect.RestoreMp) && RecoveryCooldownRemaining > 0f)
            { message = "回復アイテムはあと" + Mathf.CeilToInt(RecoveryCooldownRemaining) + "秒で使用できます。"; return false; }
            if (UsableQuantity(item) <= 0) { message = "アイテムを所持していません。"; return false; }
            if (item.effect == ConsumableEffect.RestoreHp && (item.recoveryAmount <= 0 || player.CurrentHp >= player.Stats.maxHp))
            { message = "HPは満タンです。"; return false; }
            if (item.effect == ConsumableEffect.RestoreMp && (item.recoveryAmount <= 0 || player.CurrentMp >= player.Stats.maxMp))
            { message = "MPは満タンです。"; return false; }
            if (item.effect == ConsumableEffect.MonsterLure)
            {
                if (ResolveLureActor() == null || item.durationSeconds <= 0 || float.IsNaN(item.durationSeconds) ||
                    float.IsInfinity(item.durationSeconds)) return false;
            }
            if (item.effect == ConsumableEffect.PermanentMonsterLure)
            {
                if (!permanentLure && ResolveLureActor() == null) return false;
                permanentLure = !permanentLure;
                if (!permanentLure) lureUntil = 0;
                message = permanentLure ? "敵の引き寄せをオンにしました。" : "敵の引き寄せをオフにしました。";
                Changed?.Invoke();
                return true;
            }
            VillageSpawnPoint village = null;
            if (item.effect == ConsumableEffect.ReturnToTown || item.effect == ConsumableEffect.PermanentReturnToTown)
            {
                village = FindFirstObjectByType<VillageSpawnPoint>(FindObjectsInactive.Include);
                if (village == null) { message = "街の帰還地点が見つかりません。"; return false; }
            }
            busy = true;
            try
            {
                if (!item.IsUnlimited) inventory[id] = Quantity(id) - 1;
                if (village != null && !village.TryRespawnPlayer(true))
                {
                    if (!item.IsUnlimited) inventory[id] = Quantity(id) + 1;
                    message = "街へ帰還できません。帰還の翼は消費していません。";
                    return false;
                }
                if (item.effect == ConsumableEffect.RestoreHp || item.effect == ConsumableEffect.RestoreMp)
                    recoveryReadyAt = Time.time + RecoveryCooldownSeconds;
                if (item.effect == ConsumableEffect.RestoreHp) player.Heal(item.recoveryAmount);
                else if (item.effect == ConsumableEffect.RestoreMp) player.RecoverMp(item.recoveryAmount);
                else if (item.effect == ConsumableEffect.MonsterLure) lureUntil = Time.realtimeSinceStartupAsDouble + item.durationSeconds;
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
            if ((effect != ConsumableEffect.RestoreHp && effect != ConsumableEffect.RestoreMp) || item == null || item.effect != effect) return false;
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
            permanentLure = false; lureUntil = 0; recoveryReadyAt = 0; savePending = false;
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
