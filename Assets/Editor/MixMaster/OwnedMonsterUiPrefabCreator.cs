#if UNITY_EDITOR
using MixMaster.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.EditorTools
{
    public static class OwnedMonsterUiPrefabCreator
    {
        private const string UiFolder =
            "Assets/Prefab/UI";

        public const string EntryPrefabPath =
            "Assets/Prefab/UI/OwnedMonsterEntry.prefab";

        public const string PanelPrefabPath =
            "Assets/Prefab/UI/OwnedMonsterListPanel.prefab";

        [MenuItem("MixMaster/UI/仲間一覧UI Prefabを作成・更新")]
        public static void CreateOrUpdate()
        {
            EnsureFolder(UiFolder);

            GameObject entry =
                CreateEntryPrefab();

            if (entry == null)
                return;

            CreatePanelPrefab(entry);

            // Keep the detail screen connected even when the list prefab
            // is regenerated from this menu.
            OwnedMonsterDetailPrefabCreator.AttachFromListCreation();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[OwnedMonsterUiPrefabCreator] 作成・更新完了\n" +
                EntryPrefabPath + "\n" +
                PanelPrefabPath);
        }

        private static GameObject CreateEntryPrefab()
        {
            GameObject root =
                new GameObject(
                    "OwnedMonsterEntry",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(LayoutElement),
                    typeof(OwnedMonsterEntryUI));

            RectTransform rect =
                root.GetComponent<RectTransform>();

            rect.sizeDelta =
                new Vector2(900f, 190f);

            Image background =
                root.GetComponent<Image>();

            background.color =
                new Color(
                    0.08f,
                    0.10f,
                    0.14f,
                    0.88f);

            LayoutElement layout =
                root.GetComponent<LayoutElement>();

            layout.minHeight = 190f;
            layout.preferredHeight = 190f;
            layout.flexibleHeight = 0f;

            Image icon =
                CreateImage(
                    root.transform,
                    "MonsterIcon",
                    new Vector2(16f, -20f),
                    new Vector2(150f, 150f));

            TMP_Text name =
                CreateText(
                    root.transform,
                    "MonsterName",
                    new Vector2(180f, -15f),
                    new Vector2(520f, 42f),
                    30f,
                    TextAlignmentOptions.Left);

            // Long names with a title should shrink rather than
            // overlap the level indicator.
            name.enableAutoSizing = true;
            name.fontSizeMin = 17f;
            name.fontSizeMax = 30f;
            name.enableWordWrapping = false;

            TMP_Text level =
                CreateText(
                    root.transform,
                    "Level",
                    new Vector2(720f, -15f),
                    new Vector2(165f, 42f),
                    26f,
                    TextAlignmentOptions.Left);

            TMP_Text iv =
                CreateText(
                    root.transform,
                    "StatsOrIV",
                    new Vector2(180f, -65f),
                    new Vector2(690f, 115f),
                    21f,
                    TextAlignmentOptions.TopLeft);

            iv.richText = true;

            OwnedMonsterEntryUI ui =
                root.GetComponent<OwnedMonsterEntryUI>();

            ui.SetMonsterIcon(icon);
            ui.SetMonsterNameText(name);
            ui.SetLevelText(level);
            ui.SetIvText(iv);

            GameObject saved =
                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    EntryPrefabPath);

            Object.DestroyImmediate(root);

            return saved;
        }

        private static void CreatePanelPrefab(
            GameObject entryPrefabObject)
        {
            GameObject root =
                new GameObject(
                    "OwnedMonsterListPanel",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(OwnedMonsterListUI));

            RectTransform rootRect =
                root.GetComponent<RectTransform>();

            rootRect.anchorMin =
                new Vector2(0f, 0f);

            rootRect.anchorMax =
                new Vector2(1f, 1f);

            rootRect.offsetMin =
                Vector2.zero;

            rootRect.offsetMax =
                Vector2.zero;

            Image rootImage =
                root.GetComponent<Image>();

            rootImage.color =
                new Color(
                    0.03f,
                    0.04f,
                    0.06f,
                    0.94f);

            TMP_Text count =
                CreateText(
                    root.transform,
                    "CountText",
                    new Vector2(40f, -30f),
                    new Vector2(400f, 55f),
                    34f,
                    TextAlignmentOptions.Left);

            count.text = "仲間 0体";

            TMP_Text modeText =
                CreateText(
                    root.transform,
                    "DisplayModeText",
                    new Vector2(430f, -30f),
                    new Vector2(250f, 55f),
                    26f,
                    TextAlignmentOptions.Left);

            modeText.text =
                "表示: ステータス";

            Toggle modeToggle =
                CreateToggle(
                    root.transform,
                    "DisplayModeToggle",
                    new Vector2(700f, -32f),
                    new Vector2(70f, 44f));

            modeToggle.isOn = false;

            Button close =
                CreateButton(
                    root.transform,
                    "CloseButton",
                    new Vector2(-35f, -28f),
                    new Vector2(120f, 56f),
                    "閉じる");

            RectTransform scrollRoot =
                CreateRect(
                    root.transform,
                    "ScrollView");

            scrollRoot.anchorMin =
                new Vector2(0f, 0f);

            scrollRoot.anchorMax =
                new Vector2(1f, 1f);

            scrollRoot.offsetMin =
                new Vector2(30f, 30f);

            scrollRoot.offsetMax =
                new Vector2(-30f, -105f);

            ScrollRect scroll =
                scrollRoot.gameObject
                    .AddComponent<ScrollRect>();

            RectTransform viewport =
                CreateRect(
                    scrollRoot,
                    "Viewport");

            viewport.anchorMin =
                Vector2.zero;

            viewport.anchorMax =
                Vector2.one;

            viewport.offsetMin =
                Vector2.zero;

            viewport.offsetMax =
                Vector2.zero;

            Image viewportImage =
                viewport.gameObject
                    .AddComponent<Image>();

            viewportImage.color =
                new Color(
                    1f,
                    1f,
                    1f,
                    0.001f);

            viewport.gameObject
                .AddComponent<RectMask2D>();

            RectTransform content =
                CreateRect(
                    viewport,
                    "Content");

            content.anchorMin =
                new Vector2(0f, 1f);

            content.anchorMax =
                new Vector2(1f, 1f);

            content.pivot =
                new Vector2(0.5f, 1f);

            content.anchoredPosition =
                Vector2.zero;

            content.sizeDelta =
                new Vector2(0f, 0f);

            VerticalLayoutGroup vertical =
                content.gameObject
                    .AddComponent<VerticalLayoutGroup>();

            vertical.padding =
                new RectOffset(
                    8,
                    8,
                    8,
                    8);

            vertical.spacing = 10f;
            vertical.childAlignment =
                TextAnchor.UpperCenter;

            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;

            ContentSizeFitter fitter =
                content.gameObject
                    .AddComponent<ContentSizeFitter>();

            fitter.horizontalFit =
                ContentSizeFitter.FitMode.Unconstrained;

            fitter.verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType =
                ScrollRect.MovementType.Clamped;

            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 30f;

            TMP_Text emptyText =
                CreateText(
                    root.transform,
                    "EmptyState",
                    new Vector2(0f, -300f),
                    new Vector2(600f, 80f),
                    30f,
                    TextAlignmentOptions.Center);

            RectTransform emptyRect =
                emptyText.rectTransform;

            emptyRect.anchorMin =
                new Vector2(0.5f, 1f);

            emptyRect.anchorMax =
                new Vector2(0.5f, 1f);

            emptyRect.pivot =
                new Vector2(0.5f, 1f);

            emptyRect.anchoredPosition =
                new Vector2(0f, -280f);

            emptyText.text =
                "まだ仲間がいません";

            OwnedMonsterListUI listUi =
                root.GetComponent<OwnedMonsterListUI>();

            listUi.SetPanelRoot(root);
            listUi.SetContentRoot(content);

            OwnedMonsterEntryUI entryPrefab =
                entryPrefabObject != null
                    ? entryPrefabObject
                        .GetComponent<OwnedMonsterEntryUI>()
                    : null;

            listUi.SetEntryPrefab(
                entryPrefab);

            listUi.SetCountText(count);
            listUi.SetEmptyState(
                emptyText.gameObject);

            listUi.SetDisplayModeText(
                modeText);

            listUi.SetDisplayModeToggle(
                modeToggle);

            listUi.SetCloseButton(close);

            PrefabUtility.SaveAsPrefabAsset(
                root,
                PanelPrefabPath);

            Object.DestroyImmediate(root);
        }

        private static Image CreateImage(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            GameObject go =
                new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(Image));

            go.transform.SetParent(
                parent,
                false);

            RectTransform rect =
                go.GetComponent<RectTransform>();

            rect.anchorMin =
                new Vector2(0f, 1f);

            rect.anchorMax =
                new Vector2(0f, 1f);

            rect.pivot =
                new Vector2(0f, 1f);

            rect.anchoredPosition =
                anchoredPosition;

            rect.sizeDelta = size;

            Image image =
                go.GetComponent<Image>();

            image.preserveAspect = true;

            return image;
        }

        private static TMP_Text CreateText(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            Vector2 size,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            GameObject go =
                new GameObject(
                    name,
                    typeof(RectTransform));

            go.transform.SetParent(
                parent,
                false);

            RectTransform rect =
                go.GetComponent<RectTransform>();

            rect.anchorMin =
                new Vector2(0f, 1f);

            rect.anchorMax =
                new Vector2(0f, 1f);

            rect.pivot =
                new Vector2(0f, 1f);

            rect.anchoredPosition =
                anchoredPosition;

            rect.sizeDelta = size;

            TextMeshProUGUI text =
                go.AddComponent<TextMeshProUGUI>();

            text.fontSize = fontSize;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.enableWordWrapping = true;

            return text;
        }

        private static Toggle CreateToggle(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            GameObject root =
                new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(Toggle));

            root.transform.SetParent(
                parent,
                false);

            RectTransform rootRect =
                root.GetComponent<RectTransform>();

            rootRect.anchorMin =
                new Vector2(0f, 1f);

            rootRect.anchorMax =
                new Vector2(0f, 1f);

            rootRect.pivot =
                new Vector2(0f, 1f);

            rootRect.anchoredPosition =
                anchoredPosition;

            rootRect.sizeDelta = size;

            GameObject backgroundObject =
                new GameObject(
                    "Background",
                    typeof(RectTransform),
                    typeof(Image));

            backgroundObject.transform.SetParent(
                root.transform,
                false);

            RectTransform backgroundRect =
                backgroundObject.GetComponent<RectTransform>();

            backgroundRect.anchorMin =
                new Vector2(0f, 0.5f);

            backgroundRect.anchorMax =
                new Vector2(0f, 0.5f);

            backgroundRect.pivot =
                new Vector2(0f, 0.5f);

            backgroundRect.anchoredPosition =
                Vector2.zero;

            backgroundRect.sizeDelta =
                new Vector2(44f, 44f);

            Image background =
                backgroundObject.GetComponent<Image>();

            background.color =
                new Color(
                    0.18f,
                    0.21f,
                    0.28f,
                    1f);

            GameObject checkObject =
                new GameObject(
                    "Checkmark",
                    typeof(RectTransform),
                    typeof(Image));

            checkObject.transform.SetParent(
                backgroundObject.transform,
                false);

            RectTransform checkRect =
                checkObject.GetComponent<RectTransform>();

            checkRect.anchorMin =
                new Vector2(0.18f, 0.18f);

            checkRect.anchorMax =
                new Vector2(0.82f, 0.82f);

            checkRect.offsetMin =
                Vector2.zero;

            checkRect.offsetMax =
                Vector2.zero;

            Image check =
                checkObject.GetComponent<Image>();

            check.color =
                new Color(
                    1f,
                    0.82f,
                    0.2f,
                    1f);

            Toggle toggle =
                root.GetComponent<Toggle>();

            toggle.targetGraphic =
                background;

            toggle.graphic =
                check;

            return toggle;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            Vector2 offsetFromTopRight,
            Vector2 size,
            string label)
        {
            GameObject go =
                new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Button));

            go.transform.SetParent(
                parent,
                false);

            RectTransform rect =
                go.GetComponent<RectTransform>();

            rect.anchorMin =
                new Vector2(1f, 1f);

            rect.anchorMax =
                new Vector2(1f, 1f);

            rect.pivot =
                new Vector2(1f, 1f);

            rect.anchoredPosition =
                offsetFromTopRight;

            rect.sizeDelta = size;

            Image image =
                go.GetComponent<Image>();

            image.color =
                new Color(
                    0.22f,
                    0.25f,
                    0.32f,
                    1f);

            Button button =
                go.GetComponent<Button>();

            TMP_Text text =
                CreateText(
                    go.transform,
                    "Label",
                    Vector2.zero,
                    size,
                    24f,
                    TextAlignmentOptions.Center);

            RectTransform textRect =
                text.rectTransform;

            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot =
                new Vector2(0.5f, 0.5f);

            textRect.anchoredPosition =
                Vector2.zero;

            textRect.sizeDelta =
                Vector2.zero;

            text.text = label;

            return button;
        }

        private static RectTransform CreateRect(
            Transform parent,
            string name)
        {
            GameObject go =
                new GameObject(
                    name,
                    typeof(RectTransform));

            go.transform.SetParent(
                parent,
                false);

            return go.GetComponent<RectTransform>();
        }

        private static void EnsureFolder(
            string folderPath)
        {
            string[] parts =
                folderPath
                    .Replace('\\', '/')
                    .Split('/');

            string current = "Assets";

            for (int i = 1; i < parts.Length; i++)
            {
                string next =
                    current + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(
                        current,
                        parts[i]);
                }

                current = next;
            }
        }
    }
}
#endif
