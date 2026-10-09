using System;
using System.Text;
using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class OwnedMonsterDetailUI : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private Image monsterIcon;
        [SerializeField] private TMP_Text monsterNameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text elementText;
        [SerializeField] private TMP_Text experienceText;
        [SerializeField] private TMP_Text intimacyText;
        [SerializeField] private TMP_Text titleDescriptionText;

        [Header("Statistics")]
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private Toggle individualValuesToggle;
        [SerializeField] private TMP_Text displayModeText;
        [SerializeField] private TMP_Text resistanceText;
        [SerializeField] private TMP_Text dropBonusText;

        [Header("Training / Equipment")]
        [SerializeField] private TMP_Text skillPointsText;
        [SerializeField] private TMP_Text learnedSkillsText;
        [SerializeField] private TMP_Text equipmentText;
        [SerializeField] private Button closeButton;

        private OwnedMonsterRecord selectedRecord;
        private MonsterCatalogSO catalog;
        private bool showIndividualValues;

        public OwnedMonsterRecord SelectedRecord => selectedRecord;

        private void Awake()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);

            if (individualValuesToggle != null)
            {
                individualValuesToggle.onValueChanged.AddListener(
                    HandleModeChanged);
            }
        }

        private void OnDestroy()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Hide);

            if (individualValuesToggle != null)
            {
                individualValuesToggle.onValueChanged.RemoveListener(
                    HandleModeChanged);
            }
        }

        public void Show(
            OwnedMonsterRecord record,
            MonsterCatalogSO monsterCatalog,
            bool showIvsInitially = false)
        {
            if (record == null)
                return;

            selectedRecord = record;
            catalog = monsterCatalog != null
                ? monsterCatalog
                : MonsterCatalogSO.Load();

            showIndividualValues = showIvsInitially;

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            if (individualValuesToggle != null)
            {
                individualValuesToggle.SetIsOnWithoutNotify(
                    showIndividualValues);
            }

            Refresh();
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        public void Refresh()
        {
            if (selectedRecord == null)
                return;

            MonsterSO monster = catalog != null
                ? catalog.GetMonster(selectedRecord.monsterId)
                : null;

            TitleSO title = catalog != null
                ? catalog.GetTitle(selectedRecord.titleId)
                : null;

            string monsterName = monster != null &&
                !string.IsNullOrWhiteSpace(monster.displayName)
                ? monster.displayName
                : selectedRecord.monsterId;

            string titleName = title != null
                ? (!string.IsNullOrWhiteSpace(title.displayName)
                    ? title.displayName
                    : title.titleId)
                : string.Empty;

            SetText(monsterNameText,
                (title != null ? "《" + titleName + "》" : "") +
                monsterName);

            if (monsterIcon != null)
            {
                monsterIcon.sprite = monster != null
                    ? (monster.icon != null ? monster.icon : monster.sprite)
                    : null;
                monsterIcon.enabled = monsterIcon.sprite != null;
                monsterIcon.raycastTarget = false;
            }

            SetText(levelText,
                "Lv." + Mathf.Max(1, selectedRecord.level));

            string elementName = monster != null
                ? GetElementName(monster.baseStats != null
                    ? monster.baseStats.element
                    : ElementType.None)
                : "不明";

            string attackTypeName = monster == null
                ? "不明"
                : (monster.attackType == MonsterAttackType.Ranged
                    ? "遠距離型"
                    : "近距離型");

            // Keep the existing top-row Element TMP reference.
            // This avoids breaking previously generated/customized prefabs.
            SetText(elementText,
                elementName + "属性｜" + attackTypeName);

            // Experience-to-next-level has not been defined yet.
            SetText(experienceText,
                "累計EXP: " + selectedRecord.experience.ToString("N0"));

            SetText(intimacyText,
                "親密度: " + selectedRecord.intimacy.ToString("0.##"));

            SetText(titleDescriptionText, title != null
                ? (!string.IsNullOrWhiteSpace(title.description)
                    ? "称号効果: " + title.description
                    : "称号: " + titleName)
                : "称号: なし");

            CharacterStats stats = monster != null
                ? StatCalculator.CalculateMonsterStats(
                    monster,
                    Mathf.Max(1, selectedRecord.level),
                    selectedRecord.individualValues,
                    title)
                : null;

            // Drop-rate bonus is a shared additive stat (not an
            // equipment-specific drop chance). Show it in both display modes.
            string statLines = showIndividualValues
                ? FormatIndividualValues(selectedRecord.individualValues)
                : FormatStats(stats);

            SetText(statsText,
                statLines + FormatDropRateBonus(stats));

            SetText(displayModeText, showIndividualValues
                ? "表示: 個体値"
                : "表示: ステータス");

            SetText(resistanceText, stats != null
                ? FormatResistances(stats.resistances)
                : "モンスターデータなし");

            // Legacy separately positioned field is kept for old
            // prefab references but no longer duplicates the bonus.
            SetText(dropBonusText, string.Empty);

            SetText(skillPointsText,
                "スキルポイント: " + selectedRecord.skillPoints +
                "  (使用済み: " + selectedRecord.spentSkillPoints + ")");

            SetText(learnedSkillsText,
                FormatLearnedSkills(monster, selectedRecord));

            SetText(equipmentText,
                FormatEquipment(selectedRecord));
        }

        private void HandleModeChanged(bool showIvs)
        {
            showIndividualValues = showIvs;
            Refresh();
        }

        private static string FormatIndividualValues(
            MonsterIndividualValues iv)
        {
            if (iv == null)
                return "個体値データなし";

            return "HP " + FormatIv(iv.hp) + "    MP " + FormatIv(iv.mp) +
                   "\n攻撃 " + FormatIv(iv.attack) + "    魔力 " + FormatIv(iv.magic) +
                   "\n防御 " + FormatIv(iv.defense) + "    魔防 " + FormatIv(iv.magicDefense) +
                   "\nクリ率 " + FormatIv(iv.criticalRate) +
                   "    クリ倍率 " + FormatIv(iv.criticalMultiplier) +
                   "\n移動速度 " + FormatIv(iv.moveSpeed) +
                   "    攻撃速度 " + FormatIv(iv.attackSpeed) +
                   "\n攻撃範囲 " + FormatIv(iv.attackRange);
        }

        private static string FormatStats(CharacterStats stats)
        {
            if (stats == null)
                return "モンスターデータなし";

            return "HP " + stats.maxHp.ToString("N0") +
                   "    MP " + stats.maxMp.ToString("N0") +
                   "\n攻撃 " + stats.attack.ToString("N0") +
                   "    魔力 " + stats.magic.ToString("N0") +
                   "\n防御 " + stats.defense.ToString("N0") +
                   "    魔防 " + stats.magicDefense.ToString("N0") +
                   "\nクリ率 " + (stats.criticalRate * 100f).ToString("0.##") + "%" +
                   "    クリ倍率 x" + stats.criticalMultiplier.ToString("0.##") +
                   "\n移動速度 " + stats.moveSpeed.ToString("0.##") +
                   "    攻撃速度 " + stats.attackSpeed.ToString("0.##") +
                   "\n攻撃範囲 " + stats.attackRange.ToString("0.##");
        }

        private static string FormatDropRateBonus(CharacterStats stats)
        {
            if (stats == null)
                return "    ドロップ率補正 -";

            // Shared additive percentage; equipment-specific drop rate
            // itself is not implemented in the current data model.
            return "    ドロップ率補正 +" +
                   (stats.dropRateBonus * 100f).ToString("0.##") + "%";
        }

        private static string FormatIv(int value)
        {
            int iv = MonsterIndividualValues.Clamp(value);

            return iv == 100
                ? "<color=#FFD700><b>100</b></color>"
                : iv.ToString();
        }

        private static string FormatResistances(ElementResistanceSet set)
        {
            if (set == null)
                return "耐性データなし";

            return "火 " + FormatResistance(set.fire) +
                   "    水 " + FormatResistance(set.water) +
                   "\n風 " + FormatResistance(set.wind) +
                   "    雷 " + FormatResistance(set.lightning) +
                   "\n光 " + FormatResistance(set.light) +
                   "    闇 " + FormatResistance(set.dark);
        }

        private static string FormatResistance(float value)
        {
            float percent = ElementResistanceSet.Clamp(value) * 100f;
            return (percent >= 0f ? "+" : "") +
                   percent.ToString("0.##") + "%";
        }

        private static string FormatLearnedSkills(
            MonsterSO monster,
            OwnedMonsterRecord record)
        {
            if (record.learnedSkillIds == null ||
                record.learnedSkillIds.Count == 0)
            {
                return "習得スキル: なし";
            }

            StringBuilder builder = new StringBuilder("習得スキル:");

            for (int i = 0; i < record.learnedSkillIds.Count; i++)
            {
                string id = record.learnedSkillIds[i];
                string name = id;

                if (monster != null && monster.availableSkills != null)
                {
                    for (int j = 0; j < monster.availableSkills.Count; j++)
                    {
                        SkillSO skill = monster.availableSkills[j];

                        if (skill != null &&
                            string.Equals(skill.skillId, id,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            name = string.IsNullOrWhiteSpace(skill.displayName)
                                ? skill.skillId
                                : skill.displayName;
                            break;
                        }
                    }
                }

                builder.Append("\n・").Append(name);
            }

            return builder.ToString();
        }

        private static string FormatEquipment(OwnedMonsterRecord record)
        {
            StringBuilder result = new StringBuilder();
            var equipmentManager =
                UnityEngine.Object.FindFirstObjectByType<EquipmentManager>();

            for (int i = 0; i < EquipmentManager.MaxEquipmentSlots; i++)
            {
                if (i > 0)
                    result.Append("\n");

                result.Append("装備").Append(i + 1).Append(": ");

                if (record.equippedItemUniqueIds == null ||
                    i >= record.equippedItemUniqueIds.Count)
                {
                    result.Append("未装備");
                    continue;
                }

                string uniqueId = record.equippedItemUniqueIds[i];
                EquipmentRecord item = equipmentManager != null
                    ? equipmentManager.FindEquipment(uniqueId)
                    : null;

                result.Append(item != null
                    ? item.equipmentId + " Lv." + item.level
                    : "装備ID: " + uniqueId);
            }

            return result.ToString();
        }

        private static string GetElementName(ElementType type)
        {
            switch (type)
            {
                case ElementType.Fire: return "火";
                case ElementType.Water: return "水";
                case ElementType.Wind: return "風";
                case ElementType.Lightning: return "雷";
                case ElementType.Light: return "光";
                case ElementType.Dark: return "闇";
                default: return "無";
            }
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
                target.text = value;
        }

        public void SetMonsterIcon(Image value) => monsterIcon = value;
        public void SetMonsterNameText(TMP_Text value) => monsterNameText = value;
        public void SetLevelText(TMP_Text value) => levelText = value;
        public void SetElementText(TMP_Text value) => elementText = value;
        public void SetExperienceText(TMP_Text value) => experienceText = value;
        public void SetIntimacyText(TMP_Text value) => intimacyText = value;
        public void SetTitleDescriptionText(TMP_Text value) => titleDescriptionText = value;
        public void SetStatsText(TMP_Text value) => statsText = value;
        public void SetResistanceText(TMP_Text value) => resistanceText = value;
        public void SetDropBonusText(TMP_Text value) => dropBonusText = value;
        public void SetSkillPointsText(TMP_Text value) => skillPointsText = value;
        public void SetLearnedSkillsText(TMP_Text value) => learnedSkillsText = value;
        public void SetEquipmentText(TMP_Text value) => equipmentText = value;
        public void SetDisplayModeText(TMP_Text value) => displayModeText = value;
        public void SetIndividualValuesToggle(Toggle value) => individualValuesToggle = value;
        public void SetCloseButton(Button value) => closeButton = value;
    }
}
