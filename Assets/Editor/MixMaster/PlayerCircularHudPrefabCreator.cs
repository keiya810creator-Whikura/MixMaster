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
    /// Creates a player HUD that matches PartyCircularStatusHUD and shares
    /// its generated ring/circle/solid Sprite assets.
    /// </summary>
    public static class PlayerCircularHudPrefabCreator
    {
        private const string PrefabPath =
            "Assets/Prefab/UI/PlayerCircularStatusHUD.prefab";

        [MenuItem("MixMaster/UI/プレイヤー円形HP・MP HUDを作成・配置")]
        public static void CreateAndPlace()
        {
            PartyCircularHudPrefabCreator.EnsureFolder("Assets/Prefab/UI");
            PartyCircularHudPrefabCreator.EnsureFolder("Assets/素材/UI");

            Sprite ring = PartyCircularHudPrefabCreator.EnsureRingSprite();
            Sprite circle = PartyCircularHudPrefabCreator.EnsureCircleSprite();
            Sprite solid = PartyCircularHudPrefabCreator.EnsureSolidSprite();

            if (ring == null || circle == null || solid == null)
            {
                Debug.LogError("[PlayerCircularHud] 共通ゲージSpriteを準備できませんでした。" +
                    "先に仲間円形HUDの作成メニューを実行してください。");
                return;
            }

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Prefab/UI"))
                {
                    Debug.LogError("[PlayerCircularHud] Assets/Prefab/UI がありません。");
                    return;
                }

                GameObject generated = BuildPrefab(ring, circle, solid);
                try
                {
                    prefab = PrefabUtility.SaveAsPrefabAsset(
                        generated, PrefabPath);
                }
                finally
                {
                    Object.DestroyImmediate(generated);
                }
            }

            if (prefab == null)
                return;

            Canvas canvas = PartyCircularHudPrefabCreator.FindScreenCanvas();
            if (canvas == null)
                canvas = PartyCircularHudPrefabCreator.CreateCanvas();

            PlayerCircularStatusHUD existing =
                canvas.GetComponentInChildren<PlayerCircularStatusHUD>(true);
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorUtility.DisplayDialog(
                    "プレイヤー円形ゲージ",
                    "既にこのCanvasに配置されています。" +
                    "再配置・上書きは行いませんでした。", "OK");
                return;
            }

            GameObject instance =
                PrefabUtility.InstantiatePrefab(prefab, canvas.transform)
                    as GameObject;
            if (instance == null)
                return;

            Undo.RegisterCreatedObjectUndo(instance, "Place Player Circular HUD");
            RectTransform rect = instance.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(24f, 182f);
            rect.sizeDelta = new Vector2(145f, 150f);
            rect.localScale = Vector3.one;

            PartyCircularHudPrefabCreator.EnsureEventSystem();
            Selection.activeGameObject = instance;
            EditorSceneManager.MarkSceneDirty(instance.scene);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                "プレイヤー円形ゲージ",
                "HUDのPrefabを生成し、シーンに配置しました。" +
                "\n外側=HP、内側=MP、上部=EXP、中央=プレイヤー画像。" +
                "\n\n将来ステータス画面を実装したら、" +
                "PlayerCircularStatusHUD の On Status Menu Requested に" +
                "開く関数を接続してください。" +
                "\n\n" + PrefabPath, "OK");
        }

        private static GameObject BuildPrefab(
            Sprite ring, Sprite circle, Sprite solid)
        {
            GameObject root = new GameObject(
                "PlayerCircularStatusHUD",
                typeof(RectTransform),
                typeof(Image),
                typeof(PlayerCircularStatusHUD));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(145f, 150f);

            Image background = root.GetComponent<Image>();
            // Player keeps the original neutral background color.
            background.color = new Color(0.09f, 0.12f, 0.18f, 0.75f);
            background.raycastTarget = false;

            CreateImage(rootRect, "ExperienceTrack", solid,
                new Vector2(72.5f, 141f), new Vector2(119f, 11f),
                new Color(0.22f, 0.24f, 0.30f, 1f), false);

            Image exp = CreateFill(rootRect, "ExperienceFill", solid,
                new Vector2(72.5f, 141f), new Vector2(119f, 11f),
                new Color(1f, 0.76f, 0.19f, 1f), false);

            Vector2 center = new Vector2(72.5f, 78f);

            CreateImage(rootRect, "HPTrack", ring, center,
                new Vector2(112f, 112f),
                new Color(0.30f, 0.10f, 0.14f, 1f), false);
            Image hp = CreateFill(rootRect, "HPFill", ring, center,
                new Vector2(112f, 112f),
                new Color(1f, 0.22f, 0.28f, 1f), true);

            CreateImage(rootRect, "MPTrack", ring, center,
                new Vector2(93f, 93f),
                new Color(0.08f, 0.14f, 0.26f, 1f), false);
            Image mp = CreateFill(rootRect, "MPFill", ring, center,
                new Vector2(93f, 93f),
                new Color(0.18f, 0.62f, 1f, 1f), true);

            Image mask = CreateImage(
                rootRect, "PortraitButton", circle, center,
                new Vector2(72f, 72f),
                new Color(0.18f, 0.20f, 0.25f, 1f), true);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Button button = mask.gameObject.AddComponent<Button>();
            button.targetGraphic = mask;

            Image portrait = CreateImage(
                mask.rectTransform, "PlayerPortrait", null, Vector2.zero,
                new Vector2(72f, 72f), Color.white, false);
            RectTransform portraitRect = portrait.rectTransform;
            portraitRect.anchorMin = new Vector2(0.5f, 0.5f);
            portraitRect.anchorMax = new Vector2(0.5f, 0.5f);
            portraitRect.anchoredPosition = Vector2.zero;
            portrait.preserveAspect = true;
            portrait.enabled = false;

            TMP_Text level = CreateText(
                rootRect, "LevelText", "Lv.1",
                new Vector2(72.5f, 16f), new Vector2(120f, 24f));

            root.GetComponent<PlayerCircularStatusHUD>().SetReferences(
                hp, mp, exp, portrait, button, level);
            return root;
        }

        private static Image CreateImage(
            Transform parent, string name, Sprite sprite,
            Vector2 center, Vector2 size, Color color, bool raycast)
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

            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private static Image CreateFill(
            Transform parent, string name, Sprite sprite,
            Vector2 center, Vector2 size, Color color, bool radial)
        {
            Image image = CreateImage(
                parent, name, sprite, center, size, color, false);
            image.type = Image.Type.Filled;
            image.fillAmount = 0f;

            if (radial)
            {
                image.fillMethod = Image.FillMethod.Radial360;
                image.fillOrigin = (int)Image.Origin360.Top;
                image.fillClockwise = true;
            }
            else
            {
                image.fillMethod = Image.FillMethod.Horizontal;
                image.fillOrigin = (int)Image.OriginHorizontal.Left;
            }

            return image;
        }

        private static TMP_Text CreateText(
            Transform parent, string name, string value,
            Vector2 center, Vector2 size)
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

            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = 19f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }
    }
}
#endif
