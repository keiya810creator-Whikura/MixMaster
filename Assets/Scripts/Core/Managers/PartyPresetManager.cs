using System;
using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    public sealed class PartyPresetManager : MonoBehaviour
    {
        public static PartyPresetManager Instance { get; private set; }
        private readonly Dictionary<int, PartyPresetRecord> presets = new Dictionary<int, PartyPresetRecord>();
        private MonsterManager monsters;
        private EquipmentManager equipment;
        private SkillProgressionManager skills;
        private bool applying, pendingSave;
        private float retryAt;
        public int Count => presets.Count;
        public int Revision { get; private set; }
        public event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance() => Instance = null;
        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }
        private void Update() { if (pendingSave && Time.unscaledTime >= retryAt) Persist(); }

        private bool Bind(out string error)
        {
            monsters = GetComponent<MonsterManager>();
            equipment = GetComponent<EquipmentManager>();
            skills = GetComponent<SkillProgressionManager>();
            error = "パーティ設定を読み込めません。";
            return monsters != null && equipment != null && skills != null && GetComponent<PlayerManager>() != null;
        }

        public PartyPresetRecord GetPreset(int slot) => presets.TryGetValue(slot, out var preset) ? PartyPresetData.Copy(preset) : null;
        public bool IsSaved(int slot) => presets.ContainsKey(slot);

        public bool SavePreset(int slot, string name, bool overwrite, out string message)
        {
            message = "保存先を選択してください。";
            if (applying || slot < 0 || slot >= PartyPresetData.MaxPresets || !Bind(out message)) return false;
            if (presets.ContainsKey(slot) && !overwrite) { message = "保存済みです。上書き保存を選択してください。"; return false; }
            var preset = new PartyPresetRecord
            {
                slot = slot, id = presets.TryGetValue(slot, out var old) ? old.id : Guid.NewGuid().ToString("N"),
                name = PartyPresetData.CleanName(name), savedUtc = DateTime.UtcNow.ToString("o"),
                combatOrder = monsters.CurrentPartyCombatOrder, player = CaptureActor("player", null)
            };
            if (string.IsNullOrEmpty(preset.name)) preset.name = PartyPresetData.DefaultName(slot);
            foreach (string id in monsters.PartyMonsterUniqueIds)
            {
                OwnedMonsterRecord record = monsters.FindOwnedMonster(id);
                if (record == null) { message = "編成中のモンスターが見つかりません。"; return false; }
                preset.members.Add(CaptureActor(id, record));
            }
            if (!Validate(preset, out message)) return false;
            presets[slot] = preset;
            NotifyMutation();
            message = preset.name + "を保存しました。" + PersistenceNotice();
            return true;
        }

        private PartyPresetActor CaptureActor(string owner, OwnedMonsterRecord record)
        {
            MonsterSO definition = record != null ? MonsterCatalogSO.Load()?.GetMonster(record.monsterId) : null;
            var actor = new PartyPresetActor
            {
                ownerId = owner, monsterId = record?.monsterId,
                displayName = record == null ? "プレイヤー" : !string.IsNullOrWhiteSpace(definition?.displayName) ? definition.displayName : record.monsterId,
                combatBehavior = record?.combatBehavior ?? MonsterCombatBehavior.PlayerAttackRange
            };
            IReadOnlyList<string> items = record != null ? record.equippedItemUniqueIds : equipment.PlayerEquipmentUniqueIds;
            for (int i = 0; i < EquipmentManager.MaxEquipmentSlots; i++)
            {
                string id = items != null && i < items.Count ? items[i] : string.Empty;
                actor.equipmentIds.Add(id ?? string.Empty);
                EquipmentRecord item = equipment.FindEquipment(id);
                EquipmentSO data = item != null ? equipment.GetEquipmentDefinition(item.equipmentId) : null;
                actor.equipmentNames.Add(!string.IsNullOrWhiteSpace(data?.displayName) ? data.displayName : item?.equipmentId ?? string.Empty);
            }
            SkillLoadout loadout = skills.Loadout(owner);
            for (int i = 0; i < SkillProgressionManager.SlotCount; i++)
            {
                SkillSlot slot = i < loadout.slots.Count ? PartyPresetData.CopySkill(loadout.slots[i]) : new SkillSlot();
                slot ??= new SkillSlot();
                if (owner != "player" && slot.trigger == SkillTrigger.Manual) slot.trigger = SkillTrigger.Ready;
                actor.skills.Add(slot);
            }
            return actor;
        }

        public bool RenamePreset(int slot, string name, out string message)
        {
            message = "保存済みのプリセットを選択してください。";
            if (applying || !presets.TryGetValue(slot, out var preset)) return false;
            name = PartyPresetData.CleanName(name);
            if (string.IsNullOrEmpty(name)) { message = "名前を入力してください。"; return false; }
            preset.name = name;
            NotifyMutation();
            message = "名前を変更しました。" + PersistenceNotice();
            return true;
        }

        public bool DeletePreset(int slot, out string message)
        {
            message = "保存済みのプリセットを選択してください。";
            if (applying || !presets.Remove(slot)) return false;
            NotifyMutation();
            message = "プリセットを削除しました。" + PersistenceNotice();
            return true;
        }

        public bool CanApply(int slot, out string message)
        {
            message = "この枠にはまだ保存されていません。";
            return presets.TryGetValue(slot, out var preset) && Bind(out message) && Validate(preset, out message);
        }

        private bool Validate(PartyPresetRecord preset, out string message)
        {
            message = "プリセットの内容が不正です。上書き保存してください。";
            if (preset == null || preset.player == null || preset.player.ownerId != "player" || preset.members == null ||
                preset.members.Count > monsters.MaxPartySize || !Enum.IsDefined(typeof(PartyCombatOrder), preset.combatOrder)) return false;
            var owners = new HashSet<string>(StringComparer.Ordinal) { "player" };
            foreach (PartyPresetActor actor in preset.members)
            {
                if (actor == null || string.IsNullOrWhiteSpace(actor.ownerId) || !owners.Add(actor.ownerId) ||
                    !Enum.IsDefined(typeof(MonsterCombatBehavior), actor.combatBehavior)) return false;
                OwnedMonsterRecord current = monsters.FindOwnedMonster(actor.ownerId);
                if (current == null) { message = "所持していないモンスター：" + actor.displayName; return false; }
                if (current.monsterId != actor.monsterId || MonsterCatalogSO.Load()?.GetMonster(current.monsterId) == null)
                { message = "モンスターデータが見つかりません：" + actor.displayName; return false; }
            }
            var items = new HashSet<string>(StringComparer.Ordinal);
            foreach (PartyPresetActor actor in PartyPresetData.Actors(preset))
            {
                if (actor.equipmentIds == null || actor.equipmentIds.Count != EquipmentManager.MaxEquipmentSlots) return false;
                for (int i = 0; i < actor.equipmentIds.Count; i++)
                {
                    string id = actor.equipmentIds[i];
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    if (equipment.FindEquipment(id) == null)
                    {
                        string name = actor.equipmentNames != null && i < actor.equipmentNames.Count ? actor.equipmentNames[i] : id;
                        message = actor.displayName + "：所持していない装備 " + name;
                        return false;
                    }
                    if (!items.Add(id)) { message = "同じ装備が複数の枠に登録されています。"; return false; }
                }
                if (!skills.ValidatePresetSkills(actor.ownerId, actor.skills, out string error))
                { message = actor.displayName + "：" + error; return false; }
            }
            message = string.Empty;
            return true;
        }

        public bool ApplyPreset(int slot, out string message)
        {
            message = "切り替え中です。";
            if (applying || !CanApply(slot, out message)) return false;
            PartyPresetRecord preset = presets[slot];
            applying = true;
            try
            {
                List<PartyPresetActor> actors = PartyPresetData.Actors(preset);
                // Validate the complete set first, then commit without events.
                bool equipmentChanged = equipment.ApplyPresetEquipment(actors);
                bool skillsChanged = skills.ApplyPresetSkills(actors);
                monsters.ApplyPresetParty(preset);
                // Every observer sees the final party/equipment/skill state.
                if (skillsChanged) skills.NotifyChanged();
                if (equipmentChanged) equipment.NotifyPresetEquipmentChanged();
                monsters.NotifyPartyPresetChanged();
                NotifyMutation();
                message = preset.name + "を呼び出しました。" + PersistenceNotice();
                return true;
            }
            finally { applying = false; }
        }

        public List<PartyPresetRecord> Capture()
        {
            var result = new List<PartyPresetRecord>();
            for (int i = 0; i < PartyPresetData.MaxPresets; i++)
                if (presets.TryGetValue(i, out var preset)) result.Add(PartyPresetData.Copy(preset));
            return result;
        }

        public void Restore(IReadOnlyList<PartyPresetRecord> saved)
        {
            presets.Clear();
            if (saved != null) foreach (PartyPresetRecord source in saved)
            {
                if (source == null || source.slot < 0 || source.slot >= PartyPresetData.MaxPresets || presets.ContainsKey(source.slot)) continue;
                PartyPresetRecord record = PartyPresetData.Copy(source);
                if (string.IsNullOrWhiteSpace(record.id)) record.id = Guid.NewGuid().ToString("N");
                record.name = PartyPresetData.CleanName(record.name);
                if (string.IsNullOrEmpty(record.name)) record.name = PartyPresetData.DefaultName(record.slot);
                presets.Add(record.slot, record);
            }
            pendingSave = false;
            Revision++;
            Changed?.Invoke();
        }

        private void NotifyMutation()
        {
            Revision++;
            Changed?.Invoke();
            pendingSave = true;
            Persist();
        }

        private void Persist()
        {
            var save = GetComponent<SaveManager>();
            pendingSave = save != null && !save.SaveGame();
            retryAt = Time.unscaledTime + 1f;
        }
        private string PersistenceNotice() => pendingSave ? "\n端末保存に失敗しました。自動で再試行します。" : string.Empty;
    }
}
