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
        [SerializeField] private TMP_Text levelLabel;

        [Header("Follow")]
        [SerializeField] private Vector3 defaultWorldOffset = new Vector3(0f, 1.1f, 0f);

        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Camera worldCamera;
        private Transform targetTransform;
        private Vector3 worldOffset;

        private PartyMemberCombat partyTarget;
        private EnemyHealth enemyTarget;
        private MonsterRuntimeSetup enemySetup;
        private int displayedEnemyLevel = -1;
        private Action<CharacterWorldBar> releaseCallback;
        [Header("Skill name (edit the prefab child for layout)")]
        [SerializeField] private TMP_Text skillNameLabel;
        private Color skillNameColor = Color.white;
        private float skillNameRemaining;
        private const float SkillNameDuration = 1.4f;

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

            if (levelLabel == null)
            {
                Transform child = transform.Find("HPSlider/LevelLabel");
                if (child != null) levelLabel = child.GetComponent<TMP_Text>();
            }
            if (levelLabel != null) levelLabel.gameObject.SetActive(false);

            if (skillNameLabel == null)
            {
                Transform child = transform.Find("SkillName");
                if (child != null) skillNameLabel = child.GetComponent<TMP_Text>();
            }
            if (skillNameLabel != null) skillNameColor = skillNameLabel.color;
            ClearSkillName();

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
                (enemyTarget != null && enemyTarget.IsAlive);

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

            if (hpSlider != null)
                hpSlider.gameObject.SetActive(true);

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
            enemySetup = target != null ? target.GetComponent<MonsterRuntimeSetup>() : null;
            targetTransform = target != null ? target.transform : null;
            worldCamera = camera;
            worldOffset = offset == Vector3.zero ? defaultWorldOffset : offset;
            releaseCallback = onRelease;

            if (hpSlider != null)
                hpSlider.gameObject.SetActive(true);

            if (attackSlider != null)
                attackSlider.gameObject.SetActive(false);

            if (enemyTarget != null)
            {
                enemyTarget.HpChanged += HandleEnemyHpChanged;
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
                enemyTarget.HpChanged -= HandleEnemyHpChanged;
                enemyTarget.Died -= HandleEnemyDied;
            }

            partyTarget = null;
            enemyTarget = null;
            enemySetup = null;
            displayedEnemyLevel = -1;
            if (levelLabel != null) levelLabel.gameObject.SetActive(false);
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

        private void HandleEnemyHpChanged(long current, long max)
        {
            SetHp(current, max);
            RefreshEnemyLevel();
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
            RefreshEnemyLevel();
        }

        private void RefreshEnemyLevel()
        {
            if (enemyTarget == null || levelLabel == null) return;
            // EnemyHealth registers before a spawner may add/initialize the
            // runtime setup. SetStats raises HpChanged after that initialization.
            if (enemySetup == null)
                enemySetup = enemyTarget.GetComponent<MonsterRuntimeSetup>();
            int level = enemySetup != null ? Mathf.Max(1, enemySetup.Level) : 1;
            if (displayedEnemyLevel != level)
            {
                displayedEnemyLevel = level;
                levelLabel.text = "Lv." + level;
            }
            levelLabel.gameObject.SetActive(true);
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
            if (skillNameLabel == null) return;
            skillNameLabel.text = skillName;
            skillNameLabel.color = skillNameColor;
            skillNameLabel.gameObject.SetActive(true);
            skillNameRemaining = SkillNameDuration;
        }

        private void UpdateSkillName()
        {
            if (skillNameRemaining <= 0f) return;
            skillNameRemaining = Mathf.Max(0f, skillNameRemaining - Time.deltaTime);
            if (skillNameLabel == null) return;
            if (skillNameRemaining <= 0f) { ClearSkillName(); return; }
            var color = skillNameColor;
            color.a *= Mathf.Clamp01(skillNameRemaining / .3f);
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



