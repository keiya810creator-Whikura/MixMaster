using System;
using MixMaster.Core;

namespace MixMaster.UI
{
    /// <summary>
    /// Shared sort-value emphasis for the owned-monster and breeding lists.
    /// The selected stat value is cyan; an IV of 100 still uses gold when
    /// it is not the selected sort field.
    /// </summary>
    public static class InventorySortVisuals
    {
        private const string ColorOpen = "<color=#55E0FF><b>";
        private const string ColorClose = "</b></color>";

        public static InventoryStat SelectedStat(int sortIndex)
        {
            return sortIndex >= 2 && sortIndex <= InventoryStatFilter.Labels.Length
                ? (InventoryStat)(sortIndex - 1) : InventoryStat.All;
        }

        public static string Highlight(string value)
        {
            return ColorOpen + value + ColorClose;
        }

        // The existing inventory uses abbreviated labels, whereas the
        // breeding shop uses the detailed labels from OwnedMonsterDetailUI.
        public static string HighlightDisplayedStat(
            string text, InventoryStat stat, bool fullNames)
        {
            if (string.IsNullOrEmpty(text) || !InventoryStatFilter.SupportsIndividualValues(stat))
                return text;

            string label = GetLabel(stat, fullNames);
            if (string.IsNullOrEmpty(label))
                return text;

            string prefix = " " + label + " ";
            int start = text.IndexOf(prefix, StringComparison.Ordinal);
            if (start < 0)
                return text;

            int valueStart = start + prefix.Length;
            int valueEnd = valueStart;
            while (valueEnd < text.Length &&
                !char.IsWhiteSpace(text[valueEnd]))
                valueEnd++;

            if (valueEnd <= valueStart)
                return text;
            return text.Substring(0, valueStart) +
                Highlight(text.Substring(valueStart, valueEnd - valueStart)) +
                text.Substring(valueEnd);
        }

        public static string FormatSortMetric(
            InventoryStat stat, double value, bool individualValues)
        {
            if (individualValues && InventoryStatFilter.SupportsIndividualValues(stat))
                return value.ToString("0");
            switch (stat)
            {
                case InventoryStat.CriticalRate:
                case InventoryStat.FireResistance:
                case InventoryStat.WaterResistance:
                case InventoryStat.WindResistance:
                case InventoryStat.LightningResistance:
                case InventoryStat.LightResistance:
                case InventoryStat.DarkResistance:
                case InventoryStat.DropRate:
                    return (value * 100d).ToString("0.##") + "%";
                case InventoryStat.CriticalMultiplier:
                    return "x" + value.ToString("0.##");
                case InventoryStat.MaxHp:
                case InventoryStat.MaxMp:
                case InventoryStat.Attack:
                case InventoryStat.Magic:
                case InventoryStat.Defense:
                case InventoryStat.MagicDefense:
                    return value.ToString("N0");
                default:
                    return value.ToString("0.##");
            }
        }

        public static string SortMetricLine(
            InventoryStat stat, double value, bool individualValues)
        {
            if (stat == InventoryStat.All)
                return string.Empty;
            return Highlight(InventoryStatFilter.Labels[(int)stat] + " " +
                FormatSortMetric(stat, value, individualValues));
        }

        private static string GetLabel(InventoryStat stat, bool fullNames)
        {
            switch (stat)
            {
                case InventoryStat.MaxHp: return "HP";
                case InventoryStat.MaxMp: return "MP";
                case InventoryStat.Attack: return fullNames ? "攻撃" : "攻";
                case InventoryStat.Magic: return fullNames ? "魔力" : "魔";
                case InventoryStat.Defense: return fullNames ? "防御" : "防";
                case InventoryStat.MagicDefense: return "魔防";
                case InventoryStat.CriticalRate: return fullNames ? "クリ率" : "クリ";
                case InventoryStat.CriticalMultiplier: return "クリ倍" +
                    (fullNames ? "率" : "");
                case InventoryStat.MoveSpeed: return fullNames ? "移動速度" : "移動";
                case InventoryStat.AttackSpeed: return fullNames ? "攻撃速度" : "攻速";
                case InventoryStat.AttackRange: return fullNames ? "攻撃範囲" : "射程";
                default: return null;
            }
        }
    }
}
