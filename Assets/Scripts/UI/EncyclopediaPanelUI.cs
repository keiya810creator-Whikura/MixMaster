using System;
using System.Collections.Generic;
using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class EncyclopediaPanelUI : MonoBehaviour
    {
        [SerializeField] private EncyclopediaKind kind;
        [SerializeField] private EncyclopediaGridUI grid;
        [SerializeField] private GameObject listRoot, detailRoot, popupRoot;
        [SerializeField] private Button backButton, popupCloseButton;
        [SerializeField] private TMP_Text backLabel, heading, progressText, emptyText;
        [Header("Detail")]
        [SerializeField] private ScrollRect detailScroll;
        [SerializeField] private Image detailIcon, materialIcon;
        [SerializeField] private TMP_Text detailName, description, statsHeading, statsText, materialText, habitatsText;
        [SerializeField] private GameObject materialSection, titlesSection, skillsSection;
        [SerializeField] private TMP_Text titlesHeading, skillsHeading;
        [SerializeField] private RectTransform titlesRoot, skillsRoot;
        [SerializeField] private EncyclopediaLinkUI linkPrefab;
        [Header("Title / skill popup")]
        [SerializeField] private ScrollRect popupScroll;
        [SerializeField] private Image popupIcon;
        [SerializeField] private TMP_Text popupName, popupText;

        private List<MonsterSO> monsters = new List<MonsterSO>();
        private List<EquipmentSO> equipment = new List<EquipmentSO>();
        private readonly List<EncyclopediaLinkUI> titleLinks = new List<EncyclopediaLinkUI>();
        private readonly List<EncyclopediaLinkUI> skillLinks = new List<EncyclopediaLinkUI>();
        private EncyclopediaManager progress;
        private MapCatalogSO maps;
        private MonsterSO selectedMonster;
        private Action returnToMenu;

        private void Awake()
        {
            if (backButton != null) backButton.onClick.AddListener(Back);
            if (popupCloseButton != null) popupCloseButton.onClick.AddListener(ClosePopup);
        }

        private void OnDisable()
        {
            if (progress != null) progress.Changed -= ProgressChanged;
            progress = null;
        }

        private void OnDestroy()
        {
            if (backButton != null) backButton.onClick.RemoveListener(Back);
            if (popupCloseButton != null) popupCloseButton.onClick.RemoveListener(ClosePopup);
        }

        public void Open(Action onBack)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            returnToMenu = onBack;
            if (progress != null) progress.Changed -= ProgressChanged;
            progress = EncyclopediaManager.Instance;
            if (progress != null) progress.Changed += ProgressChanged;
            maps = MapCatalogSO.Load();
            if (kind == EncyclopediaKind.Monster)
                monsters = EncyclopediaContent.Monsters(MonsterCatalogSO.Load());
            else equipment = EncyclopediaContent.Equipment(EquipmentCatalogSO.Load());
            if (heading != null) heading.text = kind == EncyclopediaKind.Monster ? "モンスター図鑑" : "装備図鑑";
            ShowList();
            if (grid != null) grid.SetData(Count, BindCell);
            if (emptyText != null)
            {
                emptyText.text = kind == EncyclopediaKind.Monster ? "モンスターデータがありません" : "装備データがありません";
                emptyText.gameObject.SetActive(Count == 0);
            }
        }

        private int Count => kind == EncyclopediaKind.Monster ? monsters.Count : equipment.Count;
        private bool Unlocked(MonsterSO monster) => monster != null && progress != null && progress.IsUnlocked(monster.monsterId);

        private void BindCell(EncyclopediaCellUI cell, int index)
        {
            if (kind == EncyclopediaKind.Monster)
            {
                MonsterSO monster = monsters[index];
                cell.Bind(index, monster.icon != null ? monster.icon : monster.sprite,
                    EncyclopediaContent.Name(monster.displayName, monster.monsterId), !Unlocked(monster), SelectEntry);
            }
            else
            {
                EquipmentSO item = equipment[index];
                cell.Bind(index, item.icon, EncyclopediaContent.Name(item.displayName, item.equipmentId), false, SelectEntry);
            }
        }

        private void ProgressChanged()
        {
            if (kind != EncyclopediaKind.Monster) return;
            if (selectedMonster != null && !Unlocked(selectedMonster)) ShowList();
            RefreshProgress();
            if (grid != null) grid.RefreshVisible();
        }

        private void RefreshProgress()
        {
            if (progressText == null) return;
            if (kind == EncyclopediaKind.Equipment) { progressText.text = "登録数 " + equipment.Count; return; }
            int known = 0;
            foreach (MonsterSO monster in monsters) if (Unlocked(monster)) known++;
            progressText.text = "討伐済み " + known + " / " + monsters.Count;
        }

        private void SelectEntry(int index)
        {
            if (index < 0 || index >= Count) return;
            if (kind == EncyclopediaKind.Monster)
            {
                MonsterSO monster = monsters[index];
                // Guard again even if a stale/external button invokes this.
                if (!Unlocked(monster)) return;
                selectedMonster = monster;
                SetIdentity(monster.icon != null ? monster.icon : monster.sprite,
                    EncyclopediaContent.Name(monster.displayName, monster.monsterId), monster.description);
                SetText(statsHeading, "基礎ステータス（Lv.1・補正前）");
                SetText(statsText, EncyclopediaContent.BaseStats(monster.baseStats));
                if (materialSection != null) materialSection.SetActive(true);
                MaterialSO material = monster.uniqueMaterial;
                SetIcon(materialIcon, material != null ? material.icon : null);
                SetText(materialText, "ドロップ素材\n" + (material != null
                    ? EncyclopediaContent.Name(material.displayName, material.materialId) : "なし"));
                SetText(habitatsText, "生息地\n" + EncyclopediaContent.Habitats(monster, maps));
                ShowLinks(monster);
            }
            else
            {
                selectedMonster = null;
                EquipmentSO item = equipment[index];
                SetIdentity(item.icon, EncyclopediaContent.Name(item.displayName, item.equipmentId), item.description);
                SetText(statsHeading, "ステータス補正（装備Lv.1）");
                SetText(statsText, EncyclopediaContent.Modifiers(item.modifiers));
                if (materialSection != null) materialSection.SetActive(false);
                if (titlesSection != null) titlesSection.SetActive(false);
                if (skillsSection != null) skillsSection.SetActive(false);
                SetText(habitatsText, "入手できるダンジョン\n" + EncyclopediaContent.EquipmentDungeons(item, maps));
            }
            if (listRoot != null) listRoot.SetActive(false);
            if (detailRoot != null) detailRoot.SetActive(true);
            ClosePopup();
            SetText(backLabel, "一覧へ");
            ResetScroll(detailScroll);
        }

        private void SetIdentity(Sprite icon, string name, string text)
        {
            SetIcon(detailIcon, icon);
            SetText(detailName, name);
            SetText(description, text ?? string.Empty);
        }

        private void ShowLinks(MonsterSO monster)
        {
            var titles = EncyclopediaContent.Titles(monster);
            var skills = EncyclopediaContent.Skills(monster);
            if (titlesSection != null) titlesSection.SetActive(true);
            if (skillsSection != null) skillsSection.SetActive(true);
            SetText(titlesHeading, titles.Count == 0 ? "称号：なし" : "称号（タップで詳細）");
            SetText(skillsHeading, skills.Count == 0 ? "スキル：なし" : "スキル（タップで詳細）");
            foreach (EncyclopediaLinkUI link in titleLinks) link.Clear();
            foreach (EncyclopediaLinkUI link in skillLinks) link.Clear();
            for (int i = 0; i < titles.Count; i++)
            {
                TitleSO title = titles[i];
                var link = Link(titleLinks, i, titlesRoot);
                if (link != null) link.Bind(EncyclopediaContent.Name(title.displayName, title.titleId), title.icon,
                    () => ShowPopup(title.icon, EncyclopediaContent.Name(title.displayName, title.titleId), EncyclopediaContent.TitleDetails(title)));
            }
            for (int i = 0; i < skills.Count; i++)
            {
                SkillSO skill = skills[i];
                var link = Link(skillLinks, i, skillsRoot);
                if (link != null) link.Bind(EncyclopediaContent.Name(skill.displayName, skill.skillId), skill.icon,
                    () => ShowPopup(skill.icon, EncyclopediaContent.Name(skill.displayName, skill.skillId), EncyclopediaContent.SkillDetails(skill)));
            }
        }

        private EncyclopediaLinkUI Link(List<EncyclopediaLinkUI> pool, int index, RectTransform root)
        {
            if (linkPrefab == null || root == null) return null;
            while (pool.Count <= index) pool.Add(Instantiate(linkPrefab, root, false));
            return pool[index];
        }

        private void ShowPopup(Sprite icon, string name, string text)
        {
            SetIcon(popupIcon, icon);
            SetText(popupName, name);
            SetText(popupText, text);
            if (popupRoot != null) { popupRoot.SetActive(true); popupRoot.transform.SetAsLastSibling(); }
            ResetScroll(popupScroll);
        }

        public void ClosePopup() { if (popupRoot != null) popupRoot.SetActive(false); }

        public void ShowList()
        {
            selectedMonster = null;
            ClosePopup();
            if (detailRoot != null) detailRoot.SetActive(false);
            if (listRoot != null) listRoot.SetActive(true);
            SetText(backLabel, "図鑑選択へ");
            RefreshProgress();
        }

        public void Back()
        {
            if (popupRoot != null && popupRoot.activeSelf) { ClosePopup(); return; }
            if (detailRoot != null && detailRoot.activeSelf) { ShowList(); return; }
            Action callback = returnToMenu;
            Hide();
            callback?.Invoke();
        }

        public void Hide()
        {
            ClosePopup();
            selectedMonster = null;
            returnToMenu = null;
            gameObject.SetActive(false);
        }

        private static void ResetScroll(ScrollRect scroll)
        {
            if (scroll == null) return;
            Canvas.ForceUpdateCanvases();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
        }
        private static void SetText(TMP_Text text, string value) { if (text != null) text.text = value; }
        private static void SetIcon(Image image, Sprite sprite)
        { if (image != null) { image.sprite = sprite; image.enabled = sprite != null; } }
    }
}
