using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MixMaster.Core;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class OwnedMonsterEntryUI : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private Image monsterIcon;

        [Header("Text")]
        [SerializeField] private TMP_Text monsterNameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text ivText;
        [SerializeField] private Button selectionButton;

        private Action<OwnedMonsterRecord> onSelected;

        private MonsterCatalogSO catalog;
        private bool showIndividualValues;

        public OwnedMonsterRecord Record { get; private set; }

        private void Awake()
        {
            if (selectionButton == null)
                selectionButton = GetComponent<Button>();

            // Support already-generated prefab entries without a Button.
            if (selectionButton == null)
                selectionButton = gameObject.AddComponent<Button>();

            selectionButton.onClick.AddListener(HandleSelected);
        }

        private void OnDestroy()
        {
            if (selectionButton != null)
                selectionButton.onClick.RemoveListener(HandleSelected);
        }

        public void SetSelectionHandler(Action<OwnedMonsterRecord> handler)
        {
            onSelected = handler;
        }

        private void HandleSelected()
        {
            if (Record != null)
                onSelected?.Invoke(Record);
        }

        public void Bind(
            OwnedMonsterRecord record,
            MonsterCatalogSO newCatalog,
            bool showIvs = false)
        {
            Record = record;
            catalog = newCatalog;
            showIndividualValues = showIvs;

            if (record == null)
            {
                Clear();
                return;
            }

            MonsterSO monster =
                catalog != null
                    ? catalog.GetMonster(
                        record.monsterId)
                    : null;

            TitleSO title =
                catalog != null
                    ? catalog.GetTitle(
                        record.titleId)
                    : null;

            if (monsterIcon != null)
            {
                monsterIcon.sprite =
                    monster != null
                        ? (monster.icon != null
                            ? monster.icon
                            : monster.sprite)
                        : null;

                monsterIcon.enabled =
                    monsterIcon.sprite != null;
            }

            if (monsterNameText != null)
            {
                string monsterName =
                    monster != null &&
                    !string.IsNullOrWhiteSpace(
                        monster.displayName)
                        ? monster.displayName
                        : record.monsterId;

                string titlePrefix =
                    title != null
                        ? "《" +
                          (!string.IsNullOrWhiteSpace(
                              title.displayName)
                              ? title.displayName
                              : title.titleId) +
                          "》"
                        : string.Empty;

                monsterNameText.text =
                    titlePrefix + monsterName;
            }

            if (levelText != null)
            {
                levelText.text =
                    "Lv." +
                    Mathf.Max(
                        1,
                        record.level);
            }

            // Title is shown before the monster name.
            // Keep the old serialized reference compatible with
            // already-generated entry prefabs.
            if (titleText != null)
                titleText.text = string.Empty;

            RefreshValueText(
                record,
                monster,
                title);
        }

        public void SetDisplayMode(
            bool showIvs)
        {
            showIndividualValues = showIvs;

            if (Record == null)
                return;

            MonsterSO monster =
                catalog != null
                    ? catalog.GetMonster(
                        Record.monsterId)
                    : null;

            TitleSO title =
                catalog != null
                    ? catalog.GetTitle(
                        Record.titleId)
                    : null;

            RefreshValueText(
                Record,
                monster,
                title);
        }

        private void RefreshValueText(
            OwnedMonsterRecord record,
            MonsterSO monster,
            TitleSO title)
        {
            if (ivText == null)
                return;

            if (showIndividualValues)
            {
                ivText.text =
                    BuildIvText(
                        record.individualValues);
                return;
            }

            CharacterStats stats =
                StatCalculator.CalculateMonsterStats(
                    monster,
                    Mathf.Max(
                        1,
                        record.level),
                    record.individualValues,
                    title);

            ivText.text =
                BuildStatsText(
                    stats);
        }

        private static string BuildIvText(
            MonsterIndividualValues iv)
        {
            if (iv == null)
                return "IV: -";

            return
                "個体値  " +
                "HP " + FormatIv(iv.hp) +
                "  MP " + FormatIv(iv.mp) +
                "  攻 " + FormatIv(iv.attack) +
                "  魔 " + FormatIv(iv.magic) +
                "  防 " + FormatIv(iv.defense) +
                "  魔防 " + FormatIv(iv.magicDefense) +
                "\n" +
                "クリ " + FormatIv(iv.criticalRate) +
                "  クリ倍 " + FormatIv(iv.criticalMultiplier) +
                "  移動 " + FormatIv(iv.moveSpeed) +
                "  攻速 " + FormatIv(iv.attackSpeed) +
                "  射程 " + FormatIv(iv.attackRange);
        }

        private static string BuildStatsText(
            CharacterStats stats)
        {
            if (stats == null)
                return "ステータス: -";

            return
                "ステータス  " +
                "HP " + stats.maxHp.ToString("N0") +
                "  MP " + stats.maxMp.ToString("N0") +
                "  攻 " + stats.attack.ToString("N0") +
                "  魔 " + stats.magic.ToString("N0") +
                "  防 " + stats.defense.ToString("N0") +
                "  魔防 " + stats.magicDefense.ToString("N0") +
                "\n" +
                "クリ " + (stats.criticalRate * 100f).ToString("0.##") + "%" +
                "  クリ倍 x" + stats.criticalMultiplier.ToString("0.##") +
                "  移動 " + stats.moveSpeed.ToString("0.##") +
                "  攻速 " + stats.attackSpeed.ToString("0.##") +
                "  射程 " + stats.attackRange.ToString("0.##");
        }

        private static string FormatIv(
            int value)
        {
            int safeValue =
                MonsterIndividualValues.Clamp(
                    value);

            if (safeValue >= 100)
            {
                return
                    "<color=#FFD700><b>100</b></color>";
            }

            return safeValue.ToString();
        }

        private void Clear()
        {
            if (monsterIcon != null)
            {
                monsterIcon.sprite = null;
                monsterIcon.enabled = false;
            }

            if (monsterNameText != null)
                monsterNameText.text = string.Empty;

            if (levelText != null)
                levelText.text = string.Empty;

            if (titleText != null)
                titleText.text = string.Empty;

            if (ivText != null)
                ivText.text = string.Empty;
        }

        public void SetMonsterIcon(Image value) => monsterIcon = value;
        public void SetMonsterNameText(TMP_Text value) => monsterNameText = value;
        public void SetLevelText(TMP_Text value) => levelText = value;
        public void SetTitleText(TMP_Text value) => titleText = value;
        public void SetIvText(TMP_Text value) => ivText = value;
    }
}
