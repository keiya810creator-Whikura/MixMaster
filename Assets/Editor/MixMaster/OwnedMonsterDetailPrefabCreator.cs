#if UNITY_EDITOR
using MixMaster.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.EditorTools
{
    public static class OwnedMonsterDetailPrefabCreator
    {
        private const string Folder = "Assets/Prefab/UI";
        private const string DetailPrefabPath =
            "Assets/Prefab/UI/OwnedMonsterDetailPanel.prefab";

        [MenuItem("MixMaster/UI/仲間詳細Prefabを作成・一覧に接続")]
        public static void CreateAndAttach()
        {
            EnsureFolder(Folder);
            GameObject detailPrefab = EnsureDetailPrefab();

            if (detailPrefab == null)
                return;

            GameObject listPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    OwnedMonsterUiPrefabCreator.PanelPrefabPath);

            if (listPrefab == null)
            {
                EditorUtility.DisplayDialog(
                    "仲間詳細UI",
                    "詳細Prefabを作成しました。先に仲間一覧UI Prefabを作成してから、もう一度実行してください。",
                    "OK");
                return;
            }

            GameObject root =
                PrefabUtility.LoadPrefabContents(
                    OwnedMonsterUiPrefabCreator.PanelPrefabPath);

            try
            {
                OwnedMonsterListUI list =
                    root.GetComponent<OwnedMonsterListUI>();

                if (list == null)
                {
                    Debug.LogError(
                        "[OwnedMonsterDetailPrefabCreator] 一覧PrefabにOwnedMonsterListUIがありません。");
                    return;
                }

                OwnedMonsterDetailUI detail =
                    root.GetComponentInChildren<OwnedMonsterDetailUI>(true);

                if (detail == null)
                {
                    GameObject instance =
                        PrefabUtility.InstantiatePrefab(
                            detailPrefab,
                            root.transform) as GameObject;

                    if (instance == null)
                    {
                        Debug.LogError(
                            "[OwnedMonsterDetailPrefabCreator] 詳細Prefabの追加に失敗しました。");
                        return;
                    }

                    instance.name = "OwnedMonsterDetailPanel";
                    instance.transform.SetAsLastSibling();

                    detail = instance.GetComponent<OwnedMonsterDetailUI>();
                    instance.SetActive(false);
                }

                if (detail == null)
                    return;

                list.SetDetailPanel(detail);

                EditorUtility.SetDirty(list);

                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    OwnedMonsterUiPrefabCreator.PanelPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                "仲間詳細UI",
                "詳細Prefabを作成し、仲間一覧Prefabへ接続しました。\n\n既存の一覧デザインは再生成していません。",
                "OK");
        }

        private static GameObject EnsureDetailPrefab()
        {
            GameObject existing =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    DetailPrefabPath);

            // Preserve user styling when this menu is run again.
            if (existing != null)
                return existing;

            GameObject root = new GameObject(
                "OwnedMonsterDetailPanel",
                typeof(RectTransform),
                typeof(Image),
                typeof(OwnedMonsterDetailUI));

            RectTransform rect = root.GetComponent<RectTransform>();
            Stretch(rect, Vector2.zero, Vector2.zero);

            Image background = root.GetComponent<Image>();
            background.color = new Color(0.035f, 0.05f, 0.09f, 0.99f);
            background.raycastTarget = true;

            OwnedMonsterDetailUI detail =
                root.GetComponent<OwnedMonsterDetailUI>();

            TMP_Text heading = Text(
                root.transform,
                "Heading",
                "モンスター詳細",
                new Vector2(34f, -22f),
                new Vector2(600f, 62f),
                38f);

            Button closeButton = Button(
                root.transform,
                "CloseButton",
                "閉じる",
                new Vector2(-30f, -20f),
                new Vector2(140f, 64f));

            GameObject scrollGo = new GameObject(
                "ScrollView",
                typeof(RectTransform),
                typeof(ScrollRect));

            scrollGo.transform.SetParent(root.transform, false);
            RectTransform scrollRect =
                scrollGo.GetComponent<RectTransform>();

            Stretch(scrollRect,
                new Vector2(30f, 28f),
                new Vector2(-30f, -105f));

            GameObject viewportGo = new GameObject(
                "Viewport",
                typeof(RectTransform),
                typeof(Image),
                typeof(RectMask2D));

            viewportGo.transform.SetParent(scrollGo.transform, false);

            RectTransform viewport = viewportGo.GetComponent<RectTransform>();
            Stretch(viewport, Vector2.zero, Vector2.zero);

            Image viewportGraphic = viewportGo.GetComponent<Image>();
            viewportGraphic.color = new Color(1f, 1f, 1f, 0.001f);
            viewportGraphic.raycastTarget = true;

            GameObject contentGo = new GameObject(
                "Content",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));

            contentGo.transform.SetParent(viewportGo.transform, false);
            RectTransform contentRect =
                contentGo.GetComponent<RectTransform>();

            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            VerticalLayoutGroup vertical =
                contentGo.GetComponent<VerticalLayoutGroup>();

            vertical.padding = new RectOffset(5, 5, 8, 8);
            vertical.spacing = 14f;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;

            ContentSizeFitter fitter =
                contentGo.GetComponent<ContentSizeFitter>();

            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35f;

            // Identity: species / title / level / affinity / EXP.
            RectTransform identity = Section(contentRect, "Identity", 258f);

            Image icon = Image(
                identity,
                "MonsterIcon",
                new Vector2(18f, -20f),
                new Vector2(172f, 172f));

            TMP_Text name = Text(
                identity,
                "MonsterName",
                "",
                new Vector2(210f, -16f),
                new Vector2(660f, 60f),
                30f);

            name.enableAutoSizing = true;
            name.fontSizeMin = 18f;
            name.fontSizeMax = 30f;
            name.enableWordWrapping = false;

            TMP_Text level = Text(
                identity, "Level", "",
                new Vector2(212f, -82f),
                new Vector2(175f, 44f), 27f);

            TMP_Text element = Text(
                identity, "Element", "",
                new Vector2(407f, -82f),
                new Vector2(230f, 44f), 27f);

            TMP_Text exp = Text(
                identity, "Experience", "",
                new Vector2(212f, -133f),
                new Vector2(540f, 39f), 24f);

            TMP_Text intimacy = Text(
                identity, "Intimacy", "",
                new Vector2(212f, -174f),
                new Vector2(540f, 39f), 24f);

            TMP_Text titleDesc = Text(
                identity, "TitleDescription", "",
                new Vector2(22f, -216f),
                new Vector2(850f, 34f), 22f);

            // Stats / IV toggle.
            RectTransform statsArea = Section(contentRect, "Stats", 320f);
            Text(statsArea, "Header", "能力値",
                new Vector2(20f, -14f),
                new Vector2(250f, 48f), 29f);

            TMP_Text mode = Text(
                statsArea, "DisplayModeText", "表示: ステータス",
                new Vector2(405f, -17f),
                new Vector2(310f, 45f), 23f);

            Toggle ivToggle = Toggle(
                statsArea,
                new Vector2(740f, -16f));

            TMP_Text values = Text(
                statsArea, "StatsOrIV", "",
                new Vector2(24f, -73f),
                new Vector2(840f, 236f), 28f);

            values.richText = true;
            values.enableWordWrapping = true;

            // Resistances + additive drop-rate bonus.
            RectTransform resistanceArea =
                Section(contentRect, "Resistances", 206f);

            Text(resistanceArea, "Header", "属性耐性",
                new Vector2(20f, -14f),
                new Vector2(400f, 47f), 29f);

            TMP_Text resistances = Text(
                resistanceArea, "ResistanceText", "",
                new Vector2(24f, -70f),
                new Vector2(850f, 104f), 27f);

            TMP_Text dropBonus = Text(
                resistanceArea, "DropBonusText", "",
                new Vector2(24f, -172f),
                new Vector2(850f, 32f), 23f);

            // Skills: read only, training is a future feature.
            RectTransform skillArea =
                Section(contentRect, "Skills", 370f);

            Text(skillArea, "Header", "スキル",
                new Vector2(20f, -14f),
                new Vector2(430f, 48f), 29f);

            TMP_Text skillPoints = Text(
                skillArea, "SkillPointsText", "",
                new Vector2(24f, -65f),
                new Vector2(850f, 42f), 24f);

            TMP_Text learned = Text(
                skillArea, "LearnedSkillsText", "",
                new Vector2(24f, -112f),
                new Vector2(850f, 250f), 24f);

            // Equipment: 3 slots, display only.
            RectTransform equipmentArea =
                Section(contentRect, "Equipment", 191f);

            Text(equipmentArea, "Header", "装備（最大3枠）",
                new Vector2(20f, -12f),
                new Vector2(600f, 46f), 29f);

            TMP_Text equipment = Text(
                equipmentArea, "EquipmentText", "",
                new Vector2(24f, -61f),
                new Vector2(850f, 118f), 25f);

            detail.SetMonsterIcon(icon);
            detail.SetMonsterNameText(name);
            detail.SetLevelText(level);
            detail.SetElementText(element);
            detail.SetExperienceText(exp);
            detail.SetIntimacyText(intimacy);
            detail.SetTitleDescriptionText(titleDesc);
            detail.SetStatsText(values);
            detail.SetIndividualValuesToggle(ivToggle);
            detail.SetDisplayModeText(mode);
            detail.SetResistanceText(resistances);
            detail.SetDropBonusText(dropBonus);
            detail.SetSkillPointsText(skillPoints);
            detail.SetLearnedSkillsText(learned);
            detail.SetEquipmentText(equipment);
            detail.SetCloseButton(closeButton);

            // Detail is shown explicitly on an entry tap.
            root.SetActive(false);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(
                root, DetailPrefabPath);

            Object.DestroyImmediate(root);
            return saved;
        }

        private static RectTransform Section(
            Transform parent, string name, float height)
        {
            GameObject go = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(LayoutElement));

            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(910f, height);

            LayoutElement layout = go.GetComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.minHeight = height;
            layout.flexibleHeight = 0f;

            Image bg = go.GetComponent<Image>();
            bg.color = new Color(0.095f, 0.13f, 0.19f, 0.97f);
            bg.raycastTarget = false;

            return rect;
        }

        private static TMP_Text Text(
            Transform parent,
            string name,
            string value,
            Vector2 position,
            Vector2 size,
            float fontSize)
        {
            GameObject go = new GameObject(
                name, typeof(RectTransform), typeof(TextMeshProUGUI));

            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            TopLeft(rect, position, size);

            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = true;
            tmp.text = value;
            return tmp;
        }

        private static Image Image(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size)
        {
            GameObject go = new GameObject(
                name, typeof(RectTransform), typeof(Image));

            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            TopLeft(rect, position, size);

            Image result = go.GetComponent<Image>();
            result.preserveAspect = true;
            result.raycastTarget = false;

            return result;
        }

        private static Toggle Toggle(Transform parent, Vector2 position)
        {
            GameObject root = new GameObject(
                "IndividualValuesToggle",
                typeof(RectTransform),
                typeof(Toggle));

            root.transform.SetParent(parent, false);
            TopLeft(root.GetComponent<RectTransform>(),
                position, new Vector2(80f, 52f));

            Image bg = Image(root.transform, "Background",
                Vector2.zero, new Vector2(48f, 48f));

            bg.color = new Color(0.2f, 0.24f, 0.3f, 1f);
            bg.raycastTarget = true;

            Image check = Image(bg.transform, "Checkmark",
                new Vector2(10f, -10f),
                new Vector2(28f, 28f));

            check.color = new Color(1f, 0.83f, 0.2f, 1f);
            check.raycastTarget = false;

            Toggle toggle = root.GetComponent<Toggle>();
            toggle.targetGraphic = bg;
            toggle.graphic = check;
            toggle.isOn = false;
            return toggle;
        }

        private static Button Button(
            Transform parent,
            string name,
            string caption,
            Vector2 position,
            Vector2 size)
        {
            GameObject go = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));

            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image bg = go.GetComponent<Image>();
            bg.color = new Color(0.25f, 0.3f, 0.38f, 1f);

            TextMeshProUGUI label = Text(
                go.transform, "Label", caption,
                Vector2.zero, size, 26f) as TextMeshProUGUI;

            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            label.alignment = TextAlignmentOptions.Center;

            return go.GetComponent<Button>();
        }

        private static void TopLeft(
            RectTransform rect,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(
            RectTransform rect,
            Vector2 min,
            Vector2 max)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = min;
            rect.offsetMax = max;
        }

        private static void EnsureFolder(string folder)
        {
            string[] parts = folder.Replace('\\', '/').Split('/');
            string current = "Assets";

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }
    }
}
#endif
