using System.Collections.Generic;
using MixMaster.Core;
using MixMaster.Monsters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    /// <summary>
    /// Three independent monster portraits with radial HP/MP rings and
    /// horizontal EXP bars. A portrait opens that monster's detail screen.
    /// Create the HUD prefab through MixMaster/UI/仲間の円形HP・MP HUDを作成・配置.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PartyCircularStatusHUD : MonoBehaviour
    {
        [SerializeField] private Image[] hpFills = new Image[3];
        [SerializeField] private Image[] mpFills = new Image[3];
        [SerializeField] private Image[] expFills = new Image[3];
        [SerializeField] private Image[] portraits = new Image[3];
        [SerializeField] private Button[] portraitButtons = new Button[3];
        [SerializeField] private TMP_Text[] levelTexts = new TMP_Text[3];
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.15f;

        private readonly Image[] slotBackgrounds = new Image[3];

        private MonsterManager monsterManager;
        private PartyFieldSpawner partySpawner;
        private MonsterCatalogSO catalog;
        private float refreshTimer;
        private bool subscribed;

        public void SetReferences(
            Image[] health,
            Image[] mana,
            Image[] experience,
            Image[] avatars,
            Button[] buttons,
            TMP_Text[] levels)
        {
            hpFills = health;
            mpFills = mana;
            expFills = experience;
            portraits = avatars;
            portraitButtons = buttons;
            levelTexts = levels;
        }

        private void Awake()
        {
            for (int i = 0; i < 3; i++)
            {
                Transform slotTransform = transform.Find("PartySlot_" + (i + 1));
                if (slotTransform != null)
                    slotBackgrounds[i] = slotTransform.GetComponent<Image>();

                if (portraitButtons == null || i >= portraitButtons.Length ||
                    portraitButtons[i] == null)
                    continue;

                int slot = i;
                portraitButtons[i].onClick.AddListener(() => OpenDetail(slot));
            }

            catalog = MonsterCatalogSO.Load();
        }

        private void OnEnable()
        {
            EnsureManagers();
            RefreshAll();
        }

        private void OnDisable()
        {
            if (subscribed && monsterManager != null)
            {
                monsterManager.PartyChanged -= RefreshAll;
                monsterManager.MonsterExperienceChanged -= HandleExperienceChanged;
            }

            subscribed = false;
        }

        private void Update()
        {
            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer > 0f)
                return;

            refreshTimer = Mathf.Max(0.05f, refreshInterval);
            EnsureManagers();
            RefreshAll();
        }

        private void EnsureManagers()
        {
            if (monsterManager == null)
            {
                subscribed = false;
                monsterManager = FindFirstObjectByType<MonsterManager>();
            }

            if (partySpawner == null)
                partySpawner = FindFirstObjectByType<PartyFieldSpawner>();

            if (catalog == null)
                catalog = MonsterCatalogSO.Load();

            if (monsterManager != null && !subscribed)
            {
                monsterManager.PartyChanged += RefreshAll;
                monsterManager.MonsterExperienceChanged += HandleExperienceChanged;
                subscribed = true;
            }
        }

        private void HandleExperienceChanged(OwnedMonsterRecord monster)
        {
            RefreshAll();
        }

        private void RefreshAll()
        {
            IReadOnlyList<string> ids =
                monsterManager != null ? monsterManager.PartyMonsterUniqueIds : null;

            for (int i = 0; i < 3; i++)
            {
                OwnedMonsterRecord record = null;
                if (ids != null && i < ids.Count)
                    record = monsterManager.FindOwnedMonster(ids[i]);

                Image portrait = GetAt(portraits, i);
                Button button = GetAt(portraitButtons, i);
                TMP_Text level = GetAt(levelTexts, i);
                Image health = GetAt(hpFills, i);
                Image mana = GetAt(mpFills, i);
                Image experience = GetAt(expFills, i);

                Image slotBackground = slotBackgrounds[i];
                if (slotBackground != null)
                {
                    Color color = GetSlotBackgroundColor(i, record != null);
                    if (slotBackground.color != color)
                        slotBackground.color = color;
                }

                if (record == null)
                {
                    if (portrait != null)
                    {
                        portrait.sprite = null;
                        portrait.enabled = false;
                    }

                    if (button != null) button.interactable = false;
                    if (level != null) level.text = "空き";
                    SetFill(health, 0f);
                    SetFill(mana, 0f);
                    SetFill(experience, 0f);
                    continue;
                }

                MonsterSO definition =
                    catalog != null ? catalog.GetMonster(record.monsterId) : null;
                Sprite avatar = definition != null
                    ? (definition.icon != null ? definition.icon : definition.sprite)
                    : null;

                if (portrait != null)
                {
                    if (portrait.sprite != avatar)
                        portrait.sprite = avatar;
                    portrait.enabled = avatar != null;
                }

                if (button != null) button.interactable = true;

                if (level != null)
                {
                    string text = "Lv." + Mathf.Max(1, record.level);
                    if (level.text != text)
                        level.text = text;
                }

                long requiredExp = ExperienceProgression.RequiredExperience(
                    record.level, true);
                float expAmount = requiredExp > 0L
                    ? Mathf.Clamp01((float)((double)record.experience / requiredExp))
                    : 1f;
                SetFill(experience, expAmount);

                PartyMemberCombat actor =
                    partySpawner != null
                        ? partySpawner.GetActiveMember(record.uniqueId)
                        : null;

                if (actor == null)
                {
                    // Do not fabricate combat HP/MP while the party is spawning.
                    SetFill(health, 0f);
                    SetFill(mana, 0f);
                    continue;
                }

                SetFill(health, Ratio(actor.CurrentHp, actor.Stats.maxHp));
                SetFill(mana, Ratio(actor.CurrentMp, actor.Stats.maxMp));
            }
        }

        private void OpenDetail(int slot)
        {
            if (monsterManager == null ||
                slot < 0 || slot >= monsterManager.PartyMonsterUniqueIds.Count)
                return;

            string id = monsterManager.PartyMonsterUniqueIds[slot];
            OwnedMonsterRecord record = monsterManager.FindOwnedMonster(id);
            if (record == null)
                return;

            // The party HUD can be outside the otherwise closed monster panel.
            // Include inactive panels, open them, then select the tapped member.
            OwnedMonsterListUI list =
                FindFirstObjectByType<OwnedMonsterListUI>(
                    FindObjectsInactive.Include);

            if (list == null)
            {
                Debug.LogWarning(
                    "[PartyCircularStatusHUD] 仲間一覧UIがシーンに見つかりません。" +
                    "仲間一覧パネルをシーンへ配置してください。", this);
                return;
            }

            list.Open();
            list.OpenDetail(record);
        }

        public static Color GetSlotBackgroundColor(int slot, bool occupied)
        {
            if (!occupied)
                return new Color(0.09f, 0.12f, 0.18f, 0.75f);

            MaterialDropSourceType source = slot == 0
                ? MaterialDropSourceType.Party1
                : slot == 1 ? MaterialDropSourceType.Party2
                : MaterialDropSourceType.Party3;
            Color color = MaterialDropLogUI.GetSourceColor(source);

            // Slightly darken the source hue so HP/MP rings and portrait
            // remain legible; the slot order stays red / blue / green.
            return new Color(color.r * 0.55f,
                color.g * 0.55f, color.b * 0.55f, 0.93f);
        }

        private static T GetAt<T>(T[] array, int i) where T : Object
        {
            return array != null && i >= 0 && i < array.Length
                ? array[i] : null;
        }

        private static float Ratio(long current, long maximum)
        {
            return maximum > 0L
                ? Mathf.Clamp01((float)((double)current / maximum))
                : 0f;
        }

        private static void SetFill(Image image, float fill)
        {
            if (image == null)
                return;

            image.fillAmount = fill;
        }
    }
}
