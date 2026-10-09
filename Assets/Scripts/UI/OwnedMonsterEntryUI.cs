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

        public OwnedMonsterRecord Record { get; private set; }

        public void Bind(
            OwnedMonsterRecord record,
            MonsterCatalogSO catalog)
        {
            Record = record;

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
                monsterNameText.text =
                    monster != null &&
                    !string.IsNullOrWhiteSpace(
                        monster.displayName)
                        ? monster.displayName
                        : record.monsterId;
            }

            if (levelText != null)
            {
                levelText.text =
                    "Lv." +
                    Mathf.Max(
                        1,
                        record.level);
            }

            if (titleText != null)
            {
                titleText.text =
                    title != null
                        ? "称号: " +
                          (!string.IsNullOrWhiteSpace(
                              title.displayName)
                              ? title.displayName
                              : title.titleId)
                        : "称号: なし";
            }

            if (ivText != null)
            {
                ivText.text =
                    BuildIvText(
                        record.individualValues);
            }
        }

        private static string BuildIvText(
            MonsterIndividualValues iv)
        {
            if (iv == null)
                return "IV: -";

            return
                "IV  " +
                "HP " + iv.hp +
                "  MP " + iv.mp +
                "  攻 " + iv.attack +
                "  魔 " + iv.magic +
                "  防 " + iv.defense +
                "  魔防 " + iv.magicDefense +
                "\n" +
                "クリ " + iv.criticalRate +
                "  クリ倍 " + iv.criticalMultiplier +
                "  移動 " + iv.moveSpeed +
                "  攻速 " + iv.attackSpeed +
                "  射程 " + iv.attackRange;
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
