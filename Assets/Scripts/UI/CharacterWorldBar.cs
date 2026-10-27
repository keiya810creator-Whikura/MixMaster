using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MixMaster.Combat;
using MixMaster.Monsters;

namespace MixMaster.UI
{
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public sealed class CharacterWorldBar : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Slider hpSlider;
        [SerializeField] private Slider attackSlider;

        [Header("Follow")]
        [SerializeField] private Vector3 defaultWorldOffset = new Vector3(0f, 1.1f, 0f);

        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Camera worldCamera;
        private Transform targetTransform;
        private Vector3 worldOffset;

        private PartyMemberCombat partyTarget;
        private EnemyHealth enemyTarget;
        private Action<CharacterWorldBar> releaseCallback;
        private TMP_Text skillNameLabel;
        private float skillNameRemaining;
        private const float SkillNameDuration = 1.4f;
        private readonly Vector3[] barCorners = new Vector3[4];

        public Component Owner => partyTarget != null ? (Component)partyTarget : enemyTarget;
        public bool IsBound => targetTransform != null;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            ConfigureSlider(hpSlider);
            ConfigureSlider(attackSlider);
        }

        private void LateUpdate()
        {
            UpdateSkillName();
            if (targetTransform == null)
            {
                RequestRelease();
                return;
            }

            if (worldCamera == null)
                worldCamera = Camera.main;

            if (worldCamera == null)
                return;

            Vector3 worldPosition = targetTransform.position + worldOffset;
            Vector3 screenPosition = worldCamera.WorldToScreenPoint(worldPosition);

            bool targetWantsBar =
                partyTarget != null ||
                (enemyTarget != null &&
                 enemyTarget.IsAlive &&
                 (enemyTarget.CurrentHp < enemyTarget.MaxHp || skillNameRemaining > 0f));

            bool visible = screenPosition.z > 0f && targetWantsBar;

            if (canvasGroup != null)
                canvasGroup.alpha = visible ? 1f : 0f;

            if (!visible)
                return;

            rectTransform.position = screenPosition;

            if (partyTarget != null && attackSlider != null)
                attackSlider.SetValueWithoutNotify(partyTarget.AttackGauge);
        }

        public void BindParty(
            PartyMemberCombat target,
            Camera camera,
            Vector3 offset,
            Action<CharacterWorldBar> onRelease)
        {
            Unbind();

            partyTarget = target;
            targetTransform = target != null ? target.transform : null;
            worldCamera = camera;
            worldOffset = offset == Vector3.zero ? defaultWorldOffset : offset;
            releaseCallback = onRelease;

            if (attackSlider != null)
                attackSlider.gameObject.SetActive(true);

            if (partyTarget != null)
            {
                partyTarget.HpChanged += HandlePartyHpChanged;
                partyTarget.Died += HandlePartyDied;
                RefreshPartyHp();
            }

            gameObject.SetActive(true);
        }

        public void BindEnemy(
            EnemyHealth target,
            Camera camera,
            Vector3 offset,
            Action<CharacterWorldBar> onRelease)
        {
            Unbind();

            enemyTarget = target;
            targetTransform = target != null ? target.transform : null;
            worldCamera = camera;
            worldOffset = offset == Vector3.zero ? defaultWorldOffset : offset;
            releaseCallback = onRelease;

            if (attackSlider != null)
                attackSlider.gameObject.SetActive(false);

            if (enemyTarget != null)
            {
                enemyTarget.Damaged += HandleEnemyDamaged;
                enemyTarget.Died += HandleEnemyDied;
                RefreshEnemyHp();
            }

            gameObject.SetActive(true);
        }

        public void Unbind()
        {
            ClearSkillName();
            if (partyTarget != null)
            {
                partyTarget.HpChanged -= HandlePartyHpChanged;
                partyTarget.Died -= HandlePartyDied;
            }

            if (enemyTarget != null)
            {
                enemyTarget.Damaged -= HandleEnemyDamaged;
                enemyTarget.Died -= HandleEnemyDied;
            }

            partyTarget = null;
            enemyTarget = null;
            targetTransform = null;
            worldCamera = null;
            releaseCallback = null;

            if (canvasGroup != null)
                canvasGroup.alpha = 1f;
        }

        private void HandlePartyHpChanged(long current, long max)
        {
            SetHp(current, max);
        }

        private void HandlePartyDied()
        {
            ClearSkillName();
            SetHp(0L, 1L);
        }

        private void HandleEnemyDamaged(EnemyHealth enemy, long damage)
        {
            RefreshEnemyHp();
        }

        private void HandleEnemyDied(EnemyHealth enemy)
        {
            SetHp(0L, Math.Max(1L, enemy != null ? enemy.MaxHp : 1L));
            RequestRelease();
        }

        private void RefreshPartyHp()
        {
            if (partyTarget == null)
                return;

            SetHp(partyTarget.CurrentHp, Math.Max(1L, partyTarget.Stats.maxHp));

            if (attackSlider != null)
                attackSlider.SetValueWithoutNotify(partyTarget.AttackGauge);
        }

        private void RefreshEnemyHp()
        {
            if (enemyTarget == null)
                return;

            SetHp(enemyTarget.CurrentHp, Math.Max(1L, enemyTarget.MaxHp));
        }

        private void SetHp(long current, long max)
        {
            if (hpSlider == null)
                return;

            double normalized = max <= 0L ? 0d : (double)current / max;
            hpSlider.SetValueWithoutNotify(Mathf.Clamp01((float)normalized));
        }

        private void RequestRelease()
        {
            Action<CharacterWorldBar> callback = releaseCallback;

            if (callback != null)
                callback(this);
        }

        public void ShowSkillName(string skillName)
        {
            if (!IsBound || string.IsNullOrWhiteSpace(skillName)) return;
            if (skillNameLabel == null)
            {
                var go = new GameObject("SkillName", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                skillNameLabel = go.AddComponent<TextMeshProUGUI>();
                if (TMP_Settings.defaultFontAsset != null) skillNameLabel.font = TMP_Settings.defaultFontAsset;
                skillNameLabel.fontSize = 16f;
                skillNameLabel.enableAutoSizing = true;
                skillNameLabel.fontSizeMin = 12f;
                skillNameLabel.fontSizeMax = 16f;
                skillNameLabel.alignment = TextAlignmentOptions.Top;
                skillNameLabel.enableWordWrapping = false;
                skillNameLabel.richText = false;
                skillNameLabel.raycastTarget = false;
                skillNameLabel.outlineColor = Color.black;
                skillNameLabel.outlineWidth = .2f;
                var rect = (RectTransform)go.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.pivot = new Vector2(.5f, 1f);
                rect.sizeDelta = new Vector2(220f, 28f);
            }
            skillNameLabel.text = skillName;
            skillNameLabel.color = Color.white;
            skillNameLabel.gameObject.SetActive(true);
            skillNameRemaining = SkillNameDuration;
            PositionSkillName();
        }

        private void PositionSkillName()
        {
            float bottom = rectTransform.rect.yMin;
            IncludeSliderBottom(hpSlider, ref bottom);
            IncludeSliderBottom(attackSlider, ref bottom);
            skillNameLabel.rectTransform.anchoredPosition = new Vector2(0f, bottom - rectTransform.rect.center.y - 4f);
        }

        private void IncludeSliderBottom(Slider slider, ref float bottom)
        {
            if (slider == null || !slider.gameObject.activeInHierarchy) return;
            ((RectTransform)slider.transform).GetWorldCorners(barCorners);
            foreach (var corner in barCorners)
                bottom = Mathf.Min(bottom, rectTransform.InverseTransformPoint(corner).y);
        }

        private void UpdateSkillName()
        {
            if (skillNameRemaining <= 0f) return;
            skillNameRemaining = Mathf.Max(0f, skillNameRemaining - Time.deltaTime);
            if (skillNameLabel == null) return;
            if (skillNameRemaining <= 0f) { ClearSkillName(); return; }
            PositionSkillName();
            var color = Color.white;
            color.a = Mathf.Clamp01(skillNameRemaining / .3f);
            skillNameLabel.color = color;
        }

        private void ClearSkillName()
        {
            skillNameRemaining = 0f;
            if (skillNameLabel != null) skillNameLabel.gameObject.SetActive(false);
        }

        private static void ConfigureSlider(Slider slider)
        {
            if (slider == null)
                return;

            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.interactable = false;
        }
    }
}

