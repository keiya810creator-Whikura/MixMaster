using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using MixMaster.Core;

namespace MixMaster.UI
{
    /// <summary>
    /// Lightweight world-space damage number with a small reusable pool.
    /// No scene prefab or Canvas wiring is required.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DamagePopupText : MonoBehaviour
    {
        private const int MaxActive = 64;
        private const int MaxPooled = 64;
        private const float Duration = 0.72f;
        private const float RiseDistance = 0.75f;
        private const float BaseFontSize = 4.2f;

        private static readonly Stack<DamagePopupText> pooled =
            new Stack<DamagePopupText>();
        private static int activeCount;

        // Damage dealt BY enemies to the player or party uses black.
        private static readonly Color EnemyDamageColor = Color.black;
        private static readonly Color HealingColor = new Color32(180, 255, 70, 255);
        private static readonly Color BuffColor = new Color32(255, 165, 55, 255);
        private static readonly Color DebuffColor = new Color32(100, 175, 255, 255);

        private TextMeshPro label;
        private Vector3 startPosition;
        private Color baseColor;
        private float horizontalDrift;
        private float elapsed;
        private bool showing;
        private DisplayOption displayOption;
        private DisplaySettingsManager displaySettings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            pooled.Clear();
            activeCount = 0;
        }

        /// <param name="takenByAlly">True for damage to the Player or party.</param>
        public static void Show(Vector3 worldPosition, long damage, bool takenByAlly = false)
        {
            Show(worldPosition, damage, MaterialDropSourceType.Unknown, takenByAlly);
        }

        public static void Show(Vector3 worldPosition, long damage, bool takenByAlly, DamageFeedback feedback)
        {
            Show(worldPosition, damage, MaterialDropSourceType.Unknown, takenByAlly, feedback);
        }

        // The attacker's material-log color is also the damage-number color.
        public static void Show(
            Vector3 worldPosition,
            long damage,
            MaterialDropSourceType attackSource,
            bool takenByAlly = false)
        {
            Show(worldPosition, damage, attackSource, takenByAlly, default);
        }

        public static void Show(Vector3 worldPosition, long damage, MaterialDropSourceType attackSource,
            bool takenByAlly, DamageFeedback feedback)
        {
            if (!Application.isPlaying || damage <= 0L)
                return;

            Color damageColor = takenByAlly
                ? EnemyDamageColor
                : MaterialDropLogUI.GetSourceColor(attackSource);
            DamageHitParticles.Show(worldPosition, damageColor);

            if (!DisplaySettingsManager.IsDisabled(DisplayOption.DamageText) && activeCount < MaxActive)
            {
                string prefix = feedback.IsWeakness ? "弱点\n" : string.Empty;
                string suffix = feedback.IsCritical ? "‼" : string.Empty;
                float size = feedback.IsCritical ? 0.65f : 0.5f;
                ShowText(worldPosition, prefix + damage.ToString("N0", CultureInfo.InvariantCulture) + suffix, damageColor,
                    DisplayOption.DamageText, takenByAlly, size);
            }
        }

        public static void ShowHealing(Vector3 worldPosition, long recovered)
        {
            if (recovered > 0 && !DisplaySettingsManager.IsDisabled(DisplayOption.HealingText))
                ShowText(worldPosition, "+" + recovered.ToString("N0", CultureInfo.InvariantCulture), HealingColor, DisplayOption.HealingText);
        }

        public static string StatusText(SkillAffix effect, int stacks = 1)
        {
            if (effect == null || (effect.type != SkillAffixType.Buff && effect.type != SkillAffixType.Debuff) ||
                effect.duration <= 0 || effect.amount <= 0 || double.IsNaN(effect.amount) || double.IsInfinity(effect.amount) ||
                (effect.change != SkillChange.Percent && effect.change != SkillChange.Flat)) return null;
            string stat;
            switch (effect.stat)
            {
                case SkillStat.Attack: stat = "攻撃力"; break;
                case SkillStat.Magic: stat = "魔法攻撃力"; break;
                case SkillStat.Defense: stat = "防御力"; break;
                case SkillStat.MagicDefense: stat = "魔法防御力"; break;
                case SkillStat.MoveSpeed: stat = "移動速度"; break;
                case SkillStat.AttackSpeed: stat = "攻撃速度"; break;
                case SkillStat.AttackRange: stat = "攻撃範囲"; break;
                default: return null;
            }
            double amount = effect.amount * System.Math.Max(1, stacks);
            return stat + amount.ToString("0.##", CultureInfo.InvariantCulture) +
                (effect.change == SkillChange.Percent ? "％" : "") +
                (effect.type == SkillAffixType.Buff ? "UP" : "Down");
        }

        public static void ShowStatus(Vector3 worldPosition, SkillAffix effect, int stacks = 1)
        {
            if (effect == null) return;
            DisplayOption option = effect.type == SkillAffixType.Buff ? DisplayOption.BuffText : DisplayOption.DebuffText;
            if (DisplaySettingsManager.IsDisabled(option)) return;
            string text = StatusText(effect, stacks);
            if (text != null) ShowText(worldPosition, text, effect.type == SkillAffixType.Buff ? BuffColor : DebuffColor, option);
        }

        private static void ShowText(Vector3 worldPosition, string text, Color color, DisplayOption option, bool takenByAlly = false, float fontScale = 1f)
        {
            if (!Application.isPlaying || DisplaySettingsManager.IsDisabled(option)) return;

            // Particle feedback remains available when the text pool is full.
            if (activeCount >= MaxActive)
                return;

            DamagePopupText popup = null;

            // Scene changes destroy pooled GameObjects, so ignore stale entries.
            while (pooled.Count > 0 && popup == null)
                popup = pooled.Pop();

            if (popup == null)
            {
                GameObject go = new GameObject("_DamagePopupText");
                popup = go.AddComponent<DamagePopupText>();
            }

            popup.gameObject.SetActive(true);
            popup.Begin(worldPosition, text, color, option, takenByAlly, fontScale);
        }

        private void Awake()
        {
            label = GetComponent<TextMeshPro>();
            if (label == null)
                label = gameObject.AddComponent<TextMeshPro>();

            if (TMP_Settings.defaultFontAsset != null)
                label.font = TMP_Settings.defaultFontAsset;

            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = BaseFontSize;
            label.fontStyle = FontStyles.Bold;
            label.enableWordWrapping = false;
            label.outlineColor = Color.white;
            label.outlineWidth = 0.18f;

            transform.localScale = Vector3.one;

            MeshRenderer renderer = GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sortingOrder = 3000;
        }

        private void Begin(Vector3 position, string text, Color damageColor, DisplayOption option, bool takenByAlly, float fontScale)
        {
            displayOption = option;
            startPosition = position +
                new Vector3(Random.Range(-0.15f, 0.15f), 0.72f, -0.2f);
            horizontalDrift = Random.Range(-0.18f, 0.18f);
            transform.position = startPosition;
            elapsed = 0f;

            baseColor = damageColor;
            // White outline improves contrast for black enemy-origin damage;
            // party-colored damage retains its original dark outline.
            label.outlineColor = takenByAlly
                ? Color.white : new Color32(23, 18, 27, 255);
            // Every pool reuse resets emphasis, including healing/status text.
            label.fontSize = BaseFontSize * fontScale;
            label.text = text;
            label.color = baseColor;

            if (!showing)
            {
                showing = true;
                activeCount++;
            }
        }

        private void OnEnable()
        {
            displaySettings = DisplaySettingsManager.Instance;
            if (displaySettings != null) displaySettings.Changed += HandleDisplaySettingsChanged;
        }

        private void HandleDisplaySettingsChanged()
        {
            if (showing && DisplaySettingsManager.IsDisabled(displayOption)) gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!showing)
                return;

            if (DisplaySettingsManager.IsDisabled(displayOption))
            { gameObject.SetActive(false); return; }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Duration);

            transform.position = startPosition +
                new Vector3(horizontalDrift * t, RiseDistance * t, 0f);

            Color color = baseColor;
            color.a = t < 0.55f
                ? 1f
                : Mathf.Clamp01((1f - t) / 0.45f);
            label.color = color;

            if (elapsed >= Duration)
                gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (displaySettings != null) displaySettings.Changed -= HandleDisplaySettingsChanged;
            displaySettings = null;
            if (!showing)
                return;

            showing = false;
            activeCount = Mathf.Max(0, activeCount - 1);

            if (pooled.Count < MaxPooled)
                pooled.Push(this);
            else if (Application.isPlaying)
                Destroy(gameObject);
        }
    }
}

