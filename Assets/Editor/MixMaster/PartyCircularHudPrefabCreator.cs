#if UNITY_EDITOR
using System.IO;
using MixMaster.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace MixMaster.EditorTools
{
    /// <summary>
    /// Creates a ready-to-use circular party HUD and places it in the open
    /// scene. Existing authored prefabs and UI styling are not overwritten.
    /// </summary>
    public static class PartyCircularHudPrefabCreator
    {
        private const string HudPrefabPath =
            "Assets/Prefab/UI/PartyCircularStatusHUD.prefab";
        private const string SpriteFolder = "Assets/素材/UI";
        private const string RingPath = SpriteFolder + "/PartyHudRing.png";
        private const string CirclePath = SpriteFolder + "/PartyHudCircleMask.png";

        [MenuItem("MixMaster/UI/仲間の円形HP・MP HUDを作成・配置")]
        public static void CreateAndPlace()
        {
            EnsureFolder("Assets/Prefab/UI");
            EnsureFolder(SpriteFolder);

            Sprite ring = EnsureRingSprite();
            Sprite mask = EnsureCircleSprite();
            if (ring == null || mask == null)
            {
                Debug.LogError("[PartyCircularHud] ゲージ用Spriteを生成できませんでした。");
                return;
            }

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);

            if (prefab == null)
            {
                GameObject root = BuildPrefab(ring, mask);
                prefab = PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
                Object.DestroyImmediate(root);
            }

            if (prefab == null)
            {
                Debug.LogError("[PartyCircularHud] Prefab作成に失敗しました。");
                return;
            }

            Canvas canvas = FindScreenCanvas();
            if (canvas == null)
                canvas = CreateCanvas();

            PartyCircularStatusHUD old =
                canvas.GetComponentInChildren<PartyCircularStatusHUD>(true);
            if (old != null)
            {
                Selection.activeObject = old.gameObject;
                EditorUtility.DisplayDialog(
                    "仲間円形ゲージHUD",
                    "このCanvasには既に仲間ゲージがあります。" +
                    "既存配置は変更しませんでした。", "OK");
                return;
            }

            GameObject instance =
                PrefabUtility.InstantiatePrefab(prefab, canvas.transform)
                    as GameObject;

            if (instance == null)
            {
                Debug.LogError("[PartyCircularHud] シーンへの配置に失敗しました。");
                return;
            }

            Undo.RegisterCreatedObjectUndo(instance, "Place Circular Party HUD");
            RectTransform hudRect = instance.GetComponent<RectTransform>();
            if (hudRect != null)
            {
                hudRect.anchorMin = Vector2.zero;
                hudRect.anchorMax = Vector2.zero;
                hudRect.pivot = Vector2.zero;
                hudRect.anchoredPosition = new Vector2(24f, 24f);
                hudRect.sizeDelta = new Vector2(450f, 150f);
                hudRect.localScale = Vector3.one;
            }

            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                Debug.LogWarning(
                    "[PartyCircularHud] EventSystemがありません。" +
                    "アイコンのクリックにはシーン内のEventSystemが必要です。");

            Selection.activeGameObject = instance;
            EditorSceneManager.MarkSceneDirty(instance.scene);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "仲間円形ゲージHUD",
                "作成・配置しました。中央アイコンを押すと仲間詳細が開きます。" +
                "\nHP=外側の赤リング、MP=内側の青リング、EXP=上の金色バー。" +
                "\n\n" + HudPrefabPath +
                "\n\n※仲間詳細を開くため、シーン内に仲間一覧UIが必要です。",
                "OK");
        }

        private static GameObject BuildPrefab(Sprite ring, Sprite circle)
        {
            GameObject root = new GameObject(
                "PartyCircularStatusHUD",
                typeof(RectTransform),
                typeof(Image),
                typeof(PartyCircularStatusHUD));

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(450f, 150f);

            Image rootImage = root.GetComponent<Image>();
            rootImage.color = new Color(0.045f, 0.075f, 0.12f, 0.83f);
            rootImage.raycastTarget = false;

            var hpFills = new Image[3];
            var mpFills = new Image[3];
            var expFills = new Image[3];
            var portraits = new Image[3];
            var buttons = new Button[3];
            var levels = new TMP_Text[3];

            for (int i = 0; i < 3; i++)
            {
                GameObject cellObject = new GameObject(
                    "PartySlot_" + (i + 1),
                    typeof(RectTransform), typeof(Image));

                cellObject.transform.SetParent(root.transform, false);
                RectTransform cell = cellObject.GetComponent<RectTransform>();
                cell.anchorMin = Vector2.zero;
                cell.anchorMax = Vector2.zero;
                cell.pivot = Vector2.zero;
                cell.anchoredPosition = new Vector2(9f + 145f * i, 0f);
                cell.sizeDelta = new Vector2(135f, 150f);
                Image bg = cellObject.GetComponent<Image>();
                bg.color = new Color(0.09f, 0.12f, 0.18f, 0.75f);
                bg.raycastTarget = false;

                RectTransform gaugeParent = cell;

                CreateBox(gaugeParent, "ExperienceTrack",
                    new Vector2(67.5f, 134f), new Vector2(119f, 11f),
                    new Color(0.22f, 0.24f, 0.30f, 1f));

                expFills[i] = CreateFill(gaugeParent, "ExperienceFill",
                    null, new Vector2(67.5f, 134f), new Vector2(119f, 11f),
                    new Color(1f, 0.76f, 0.19f, 1f), false);

                Vector2 center = new Vector2(67.5f, 78f);
                Vector2 outer = new Vector2(112f, 112f);
                Vector2 inner = new Vector2(93f, 93f);

                CreateImage(gaugeParent, "HPTrack", ring, center, outer,
                    new Color(0.30f, 0.10f, 0.14f, 1f), false);
                hpFills[i] = CreateFill(
                    gaugeParent, "HPFill", ring, center, outer,
                    new Color(1f, 0.22f, 0.28f, 1f), true);

                CreateImage(gaugeParent, "MPTrack", ring, center, inner,
                    new Color(0.08f, 0.14f, 0.26f, 1f), false);
                mpFills[i] = CreateFill(
                    gaugeParent, "MPFill", ring, center, inner,
                    new Color(0.18f, 0.62f, 1f, 1f), true);

                // White circle texture is used as the alpha stencil. The
                // monster sprite is clipped to a circle rather than squared.
                Image maskImage = CreateImage(
                    gaugeParent, "PortraitButton", circle,
                    center, new Vector2(72f, 72f),
                    new Color(0.18f, 0.20f, 0.25f, 1f), true);
                maskImage.raycastTarget = true;
                Mask uiMask = maskImage.gameObject.AddComponent<Mask>();
                uiMask.showMaskGraphic = true;

                Button button = maskImage.gameObject.AddComponent<Button>();
                button.targetGraphic = maskImage;
                button.interactable = false;
                buttons[i] = button;

                Image avatar = CreateImage(
                    maskImage.rectTransform, "MonsterPortrait", null,
                    Vector2.zero, new Vector2(72f, 72f),
                    Color.white, false);
                avatar.preserveAspect = true;
                // A nested RectTransform must be centered in its circular
                // parent; anchoring it at (0,0) would show just a quadrant.
                RectTransform avatarRect = avatar.rectTransform;
                avatarRect.anchorMin = new Vector2(0.5f, 0.5f);
                avatarRect.anchorMax = new Vector2(0.5f, 0.5f);
                avatarRect.anchoredPosition = Vector2.zero;
                avatar.enabled = false;
                portraits[i] = avatar;

                levels[i] = CreateText(
                    gaugeParent, "LevelText", "空き",
                    new Vector2(67.5f, 16f), new Vector2(120f, 24f), 19f);
            }

            root.GetComponent<PartyCircularStatusHUD>().SetReferences(
                hpFills, mpFills, expFills, portraits, buttons, levels);
            return root;
        }

        private static Image CreateBox(
            Transform parent, string name,
            Vector2 center, Vector2 size, Color color)
        {
            return CreateImage(parent, name, null, center, size, color, false);
        }

        private static Image CreateImage(
            Transform parent, string name, Sprite sprite,
            Vector2 center, Vector2 size, Color tint, bool raycast)
        {
            GameObject go = new GameObject(
                name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = size;

            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = tint;
            img.raycastTarget = raycast;
            return img;
        }

        private static Image CreateFill(
            Transform parent, string name, Sprite sprite,
            Vector2 center, Vector2 size, Color color, bool radial)
        {
            Image img = CreateImage(
                parent, name, sprite, center, size, color, false);
            img.type = Image.Type.Filled;
            if (radial)
            {
                img.fillMethod = Image.FillMethod.Radial360;
                img.fillOrigin = (int)Image.Origin360.Top;
                img.fillClockwise = true;
            }
            else
            {
                img.fillMethod = Image.FillMethod.Horizontal;
                img.fillOrigin = (int)Image.OriginHorizontal.Left;
            }

            img.fillAmount = 0f;
            return img;
        }

        private static TMP_Text CreateText(
            Transform parent, string name, string value,
            Vector2 center, Vector2 size, float fontSize)
        {
            GameObject go = new GameObject(
                name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = size;

            TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
            label.text = value;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        private static Sprite EnsureRingSprite()
        {
            return EnsureSprite(RingPath, true);
        }

        private static Sprite EnsureCircleSprite()
        {
            return EnsureSprite(CirclePath, false);
        }

        private static Sprite EnsureSprite(string path, bool ring)
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(path) is Sprite old)
                return old;

            const int size = 256;
            Texture2D texture = new Texture2D(
                size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size * 0.5f;
                    float dy = y + 0.5f - size * 0.5f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);

                    float alpha = ring
                        ? Mathf.Clamp01((radius - 99f) * 1.5f) *
                          Mathf.Clamp01((120f - radius) * 1.5f)
                        : Mathf.Clamp01((121f - radius) * 1.5f);
                    pixels[y * size + x] = new Color32(
                        255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false);
            string absolutePath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, path);
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Canvas FindScreenCanvas()
        {
            if (Selection.activeGameObject != null)
            {
                Canvas selected = Selection.activeGameObject
                    .GetComponentInParent<Canvas>();
                if (selected != null &&
                    selected.renderMode != RenderMode.WorldSpace)
                    return selected.rootCanvas;
            }

            Canvas[] canvases =
                Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (Canvas canvas in canvases)
            {
                if (canvas != null &&
                    canvas.renderMode != RenderMode.WorldSpace &&
                    canvas.isRootCanvas)
                    return canvas;
            }

            return null;
        }

        private static Canvas CreateCanvas()
        {
            GameObject go = new GameObject(
                "PartyStatusHUDCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            Undo.RegisterCreatedObjectUndo(go, "Create Party HUD Canvas");
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 110;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void EnsureFolder(string folder)
        {
            string current = "Assets";
            string[] parts = folder.Split('/');
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
