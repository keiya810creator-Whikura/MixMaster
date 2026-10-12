#if UNITY_EDITOR
using MixMaster.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.EditorTools
{
    /// <summary>
    /// Creates a player status menu and connects it to the existing circular
    /// player HUD without recreating either prefab or changing scene layout.
    /// </summary>
    public static class PlayerStatusMenuPrefabCreator
    {
        private const string PrefabPath =
            "Assets/Prefab/UI/PlayerStatusMenuPanel.prefab";

        [MenuItem("MixMaster/UI/プレイヤーステータスメニューを作成・接続")]
        public static void CreateAndAttach()
        {
            // Prefer the HUD's own Canvas; a scene may contain several
            // screen canvases with different sorting and scale settings.
            Canvas canvas = null;
            PlayerCircularStatusHUD[] huds =
                Object.FindObjectsByType<PlayerCircularStatusHUD>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (PlayerCircularStatusHUD hud in huds)
            {
                if (hud == null || !hud.gameObject.scene.IsValid())
                    continue;

                Canvas hudCanvas = hud.GetComponentInParent<Canvas>();
                if (hudCanvas != null &&
                    hudCanvas.renderMode != RenderMode.WorldSpace)
                {
                    canvas = hudCanvas.rootCanvas;
                    break;
                }
            }

            if (canvas == null)
                canvas = PartyCircularHudPrefabCreator.FindScreenCanvas();
            if (canvas == null)
                canvas = PartyCircularHudPrefabCreator.CreateCanvas();

            PlayerStatusMenuUI panel = EnsureAndConnect(canvas);
            if (panel == null)
            {
                EditorUtility.DisplayDialog(
                    "プレイヤーステータスメニュー",
                    "メニューの作成に失敗しました。Consoleを確認してください。", "OK");
                return;
            }

            Selection.activeGameObject = panel.gameObject;
            EditorUtility.DisplayDialog(
                "プレイヤーステータスメニュー",
                "ステータスメニューを生成・配置しました。" +
                "\nプレイヤーの円形HUD中央を押すと開きます。" +
                "\n「装備」ボタンで現在の装備3枠を確認できます。" +
                "\n\n後から装備UIを実装する際には、" +
                "PlayerStatusMenuUI の On Equipment Menu Requested に" +
                "装備画面を開く関数を設定してください。" +
                "\n\n転生機能はここには実装していません。",
                "OK");
        }

        /// <summary>
        /// Idempotent integration point for new and existing player HUDs.
        /// Does not overwrite an existing authored prefab or scene instance.
        /// </summary>
        internal static PlayerStatusMenuUI EnsureAndConnect(Canvas canvas)
        {
            if (canvas == null)
                return null;

            PartyCircularHudPrefabCreator.EnsureFolder("Assets/Prefab/UI");

            GameObject asset =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (asset == null)
            {
                GameObject root = CreatePrefab();
                try
                {
                    asset = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }

            if (asset == null)
            {
                Debug.LogError(
                    "[PlayerStatusMenu] Prefabを保存できませんでした: " + PrefabPath);
                return null;
            }

            PlayerStatusMenuUI current =
                canvas.GetComponentInChildren<PlayerStatusMenuUI>(true);

            if (current == null)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(
                    asset, canvas.transform) as GameObject;
                if (instance == null)
                    return null;

                Undo.RegisterCreatedObjectUndo(
                    instance, "Place Player Status Menu");

                RectTransform rect = instance.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                instance.transform.SetAsLastSibling();
                current = instance.GetComponent<PlayerStatusMenuUI>();
                instance.SetActive(false);
            }

            PlayerCircularStatusHUD[] huds =
                canvas.GetComponentsInChildren<PlayerCircularStatusHUD>(true);
            for (int i = 0; i < huds.Length; i++)
            {
                if (huds[i] == null)
                    continue;

                Undo.RecordObject(huds[i], "Link Player Status Menu to HUD");
                huds[i].SetStatusMenu(current);
                PrefabUtility.RecordPrefabInstancePropertyModifications(huds[i]);
                EditorUtility.SetDirty(huds[i]);
            }

            // Keep the equipment inventory available from the status screen
            // even in scenes generated before this feature was added.
            EquipmentInventoryPrefabCreator.EnsureAndConnect(canvas, current);

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            AssetDatabase.SaveAssets();
            return current;
        }

        private static GameObject CreatePrefab()
        {
            GameObject root = new GameObject(
                "PlayerStatusMenuPanel",
                typeof(RectTransform),
                typeof(Image),
                typeof(PlayerStatusMenuUI));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.sizeDelta = new Vector2(1080f, 1920f);

            Image background = root.GetComponent<Image>();
            background.color = new Color(0.027f, 0.042f, 0.072f, 0.965f);
            background.raycastTarget = true;

            // Full-screen dark modal with fixed header and a scrollable body.
            TMP_Text title = MakeText(
                root.transform, "MenuTitle", "ステータス",
                new Vector2(36f, -38f), new Vector2(545f, 74f), 49f);
            title.color = new Color(1f, 0.83f, 0.45f);

            Button equipButton = MakeTopRightButton(
                root.transform, "EquipmentButton", "装備",
                new Vector2(-197f, -40f), new Vector2(175f, 70f),
                new Color(0.20f, 0.35f, 0.39f, 1f));
            Button closeButton = MakeTopRightButton(
                root.transform, "CloseButton", "閉じる",
                new Vector2(-26f, -40f), new Vector2(155f, 70f),
                new Color(0.37f, 0.23f, 0.26f, 1f));

            GameObject statusPage = MakeStretchPage(
                root.transform, "StatusPage",
                new Vector2(28f, 24f), new Vector2(-28f, -140f));
            RectTransform pageRect = statusPage.GetComponent<RectTransform>();

            RectTransform scrollRect = new GameObject(
                "StatsScrollView", typeof(RectTransform), typeof(ScrollRect))
                    .GetComponent<RectTransform>();
            scrollRect.SetParent(pageRect, false);
            Stretch(scrollRect);

            GameObject viewportObject = new GameObject(
                "Viewport", typeof(RectTransform), typeof(Image),
                typeof(RectMask2D));
            viewportObject.transform.SetParent(scrollRect, false);
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            Stretch(viewport);
            Image viewportImage = viewportObject.GetComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.001f);
            viewportImage.raycastTarget = true;

            GameObject contentObject = new GameObject(
                "Content", typeof(RectTransform),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewport, false);
            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            VerticalLayoutGroup layout =
                contentObject.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 30);
            layout.spacing = 18f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter =
                contentObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scrolling = scrollRect.GetComponent<ScrollRect>();
            scrolling.content = content;
            scrolling.viewport = viewport;
            scrolling.vertical = true;
            scrolling.horizontal = false;
            scrolling.movementType = ScrollRect.MovementType.Clamped;
            scrolling.scrollSensitivity = 48f;

            RectTransform identity = MakeSection(
                content, "CharacterIdentity", "プレイヤー", 215f);
            Image portrait = MakeImage(
                identity, "PlayerPortrait", new Vector2(30f, -66f),
                new Vector2(122f, 122f));
            portrait.preserveAspect = true;
            TMP_Text level = MakeText(
                identity, "PlayerLevel", "プレイヤー Lv.1",
                new Vector2(178f, -70f), new Vector2(645f, 55f), 30f);
            TMP_Text resources = MakeText(
                identity, "Resources", "HP\nMP\nEXP",
                new Vector2(178f, -116f), new Vector2(650f, 94f), 23f);

            RectTransform core = MakeSection(
                content, "CoreStatsSection", "基礎ステータス", 312f);
            TMP_Text coreText = MakeText(
                core, "CoreStats", string.Empty,
                new Vector2(25f, -64f), new Vector2(830f, 245f), 29f);

            RectTransform combat = MakeSection(
                content, "CombatStatsSection", "戦闘パラメータ", 317f);
            TMP_Text combatText = MakeText(
                combat, "CombatStats", string.Empty,
                new Vector2(25f, -64f), new Vector2(830f, 245f), 28f);

            RectTransform resistance = MakeSection(
                content, "ResistanceSection", "属性耐性", 219f);
            TMP_Text resistanceText = MakeText(
                resistance, "ResistanceValues", string.Empty,
                new Vector2(25f, -75f), new Vector2(830f, 150f), 28f);

            RectTransform equipment = MakeSection(
                content, "EquipmentSection", "装備中（3枠）", 240f);
            TMP_Text equipmentText = MakeText(
                equipment, "EquippedItems", string.Empty,
                new Vector2(25f, -68f), new Vector2(830f, 162f), 27f);

            RectTransform note = MakeSection(
                content, "GrowthNote", "育成", 135f);
            TMP_Text noteText = MakeText(
                note, "GrowthInfo",
                "レベルアップ：HP +5、MP・攻撃・魔力・防御・魔防 +1" +
                "\n転生は今後、祭壇や町のオブジェクトから行います。",
                new Vector2(20f, -60f), new Vector2(850f, 75f), 22f);
            noteText.color = new Color(0.84f, 0.84f, 0.89f);

            // Equipment entry is functional now: it opens a concise
            // equipped-items/inventory summary and provides an event hook.
            GameObject gearPage = MakeStretchPage(
                root.transform, "EquipmentPage",
                new Vector2(36f, 45f), new Vector2(-36f, -157f));
            RectTransform gear = gearPage.GetComponent<RectTransform>();
            Image gearBg = gearPage.AddComponent<Image>();
            gearBg.color = new Color(0.09f, 0.135f, 0.20f, 1f);
            gearBg.raycastTarget = true;

            TMP_Text gearTitle = MakeText(
                gear, "GearTitle", "プレイヤー装備",
                new Vector2(27f, -32f), new Vector2(610f, 75f), 43f);
            gearTitle.color = new Color(1f, 0.83f, 0.45f);

            Button back = MakeTopRightButton(
                gear, "BackButton", "戻る",
                new Vector2(-30f, -35f), new Vector2(163f, 64f),
                new Color(0.23f, 0.35f, 0.44f, 1f));

            TMP_Text gearText = MakeText(
                gear, "EquipmentOverview", string.Empty,
                new Vector2(34f, -153f), new Vector2(815f, 520f), 30f);

            PlayerStatusMenuUI menu = root.GetComponent<PlayerStatusMenuUI>();
            menu.SetReferences(level, resources, coreText, combatText,
                resistanceText, equipmentText, portrait,
                statusPage, gearPage, gearText,
                closeButton, equipButton, back, null);

            // Keep equipment content and whole modal hidden until requested.
            gearPage.SetActive(false);
            root.SetActive(false);
            return root;
        }

        private static GameObject MakeStretchPage(
            Transform parent, string name, Vector2 min, Vector2 max)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = min;
            rect.offsetMax = max;
            return go;
        }

        private static RectTransform MakeSection(
            Transform parent, string name, string caption, float height)
        {
            GameObject go = new GameObject(
                name, typeof(RectTransform), typeof(Image),
                typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(905f, height);
            LayoutElement element = go.GetComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleHeight = 0f;

            Image bg = go.GetComponent<Image>();
            bg.color = new Color(0.10f, 0.15f, 0.22f, 0.96f);
            bg.raycastTarget = false;

            TMP_Text heading = MakeText(
                rect, "Heading", caption,
                new Vector2(22f, -13f), new Vector2(760f, 48f), 31f);
            heading.color = new Color(1f, 0.83f, 0.46f);
            return rect;
        }

        private static Image MakeImage(
            Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(
                name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            TopLeft(go.GetComponent<RectTransform>(), position, size);
            Image img = go.GetComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = false;
            return img;
        }

        private static TMP_Text MakeText(
            Transform parent, string name, string value,
            Vector2 position, Vector2 size, float fontSize)
        {
            GameObject go = new GameObject(
                name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TopLeft(go.GetComponent<RectTransform>(), position, size);

            TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
            label.text = value;
            label.fontSize = fontSize;
            label.enableAutoSizing = true;
            label.fontSizeMin = Mathf.Max(16f, fontSize - 8f);
            label.fontSizeMax = fontSize;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.raycastTarget = false;
            return label;
        }

        private static Button MakeTopRightButton(
            Transform parent, string name, string caption,
            Vector2 position, Vector2 size, Color background)
        {
            GameObject go = new GameObject(
                name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = go.GetComponent<Image>();
            image.color = background;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;

            TMP_Text captionText = MakeText(
                rect, "Label", caption, Vector2.zero, size, 26f);
            Stretch(captionText.rectTransform);
            captionText.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static void TopLeft(
            RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
#endif
