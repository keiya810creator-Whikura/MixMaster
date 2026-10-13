#if UNITY_EDITOR
using MixMaster.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.EditorTools
{
    public static class EquipmentInventoryPrefabCreator
    {
        internal const string PrefabPath =
            "Assets/Prefab/UI/EquipmentInventoryPanel.prefab";

        [MenuItem("MixMaster/UI/装備インベントリを作成・接続")]
        public static void CreateAndAttach()
        {
            Canvas canvas = PartyCircularHudPrefabCreator.FindScreenCanvas();
            PlayerStatusMenuUI[] menus =
                Object.FindObjectsByType<PlayerStatusMenuUI>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (PlayerStatusMenuUI menu in menus)
            {
                if (menu == null || !menu.gameObject.scene.IsValid())
                    continue;
                Canvas owner = menu.GetComponentInParent<Canvas>();
                if (owner != null)
                {
                    canvas = owner.rootCanvas;
                    break;
                }
            }

            if (canvas == null)
                canvas = PartyCircularHudPrefabCreator.CreateCanvas();

            PlayerStatusMenuUI status =
                canvas.GetComponentInChildren<PlayerStatusMenuUI>(true);
            if (status == null)
                status = PlayerStatusMenuPrefabCreator.EnsureAndConnect(canvas);

            EquipmentInventoryUI inventory = EnsureAndConnect(canvas, status);
            if (inventory == null)
            {
                EditorUtility.DisplayDialog(
                    "装備インベントリ",
                    "Prefabの作成に失敗しました。Consoleを確認してください。", "OK");
                return;
            }

            PartyCircularHudPrefabCreator.EnsureEventSystem();
            Selection.activeGameObject = inventory.gameObject;
            EditorUtility.DisplayDialog(
                "装備インベントリ",
                "装備インベントリを生成・接続しました。" +
                "\nステータスメニューの「装備」から開けます。" +
                "\n入手順・レベル・全18ステータス項目でソート、" +
                "ステータスと5段階レア度で絞り込みが可能です。",
                "OK");
        }

        internal static EquipmentInventoryUI EnsureAndConnect(
            Canvas canvas, PlayerStatusMenuUI status)
        {
            if (canvas == null)
                return null;

            PartyCircularHudPrefabCreator.EnsureFolder("Assets/Prefab/UI");

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                GameObject prototype = BuildPrefab();
                try
                {
                    prefab = PrefabUtility.SaveAsPrefabAsset(
                        prototype, PrefabPath);
                }
                finally
                {
                    Object.DestroyImmediate(prototype);
                }
            }

            if (prefab == null)
                return null;

            EquipmentInventoryUI inventory =
                canvas.GetComponentInChildren<EquipmentInventoryUI>(true);
            if (inventory == null)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(
                    prefab, canvas.transform) as GameObject;
                if (instance == null)
                    return null;

                Undo.RegisterCreatedObjectUndo(
                    instance, "Place Equipment Inventory");
                RectTransform rect = instance.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                instance.transform.SetAsLastSibling();
                inventory = instance.GetComponent<EquipmentInventoryUI>();
                instance.SetActive(false);
            }

            if (status != null)
            {
                Undo.RecordObject(status, "Connect Equipment Inventory");
                status.SetEquipmentInventory(inventory);
                PrefabUtility.RecordPrefabInstancePropertyModifications(status);
                EditorUtility.SetDirty(status);
            }

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            AssetDatabase.SaveAssets();
            return inventory;
        }

        internal static GameObject BuildPrefab()
        {
            GameObject root = new GameObject(
                "EquipmentInventoryPanel", typeof(RectTransform),
                typeof(Image), typeof(EquipmentInventoryUI));
            RectTransform main = root.GetComponent<RectTransform>();
            main.anchorMin = Vector2.zero;
            main.anchorMax = Vector2.one;
            main.offsetMin = Vector2.zero;
            main.offsetMax = Vector2.zero;
            main.sizeDelta = new Vector2(1080f, 1920f);

            Image bg = root.GetComponent<Image>();
            bg.color = new Color(0.032f, 0.053f, 0.085f, 0.98f);
            bg.raycastTarget = true;

            TMP_Text heading = InventoryUiFactory.Text(
                main, "Title", "装備インベントリ",
                new Vector2(33f, -24f), new Vector2(530f, 60f), 41f);
            heading.color = new Color(1f, 0.83f, 0.46f);

            TMP_Text count = InventoryUiFactory.Text(
                main, "Count", "装備 0 / 0",
                new Vector2(35f, -80f), new Vector2(480f, 42f), 25f);

            // top-right close button anchored to the edge of any screen width
            GameObject closeGo = new GameObject(
                "CloseButton", typeof(RectTransform),
                typeof(Image), typeof(Button));
            closeGo.transform.SetParent(main, false);
            RectTransform closeRect = closeGo.GetComponent<RectTransform>();
            closeRect.anchorMin = Vector2.one;
            closeRect.anchorMax = Vector2.one;
            closeRect.pivot = Vector2.one;
            closeRect.anchoredPosition = new Vector2(-24f, -27f);
            closeRect.sizeDelta = new Vector2(154f, 60f);
            Image closeBg = closeGo.GetComponent<Image>();
            closeBg.color = new Color(0.40f, 0.23f, 0.25f);
            Button close = closeGo.GetComponent<Button>();
            close.targetGraphic = closeBg;
            TMP_Text label = InventoryUiFactory.Text(
                closeRect, "Label", "戻る", Vector2.zero,
                new Vector2(154f, 60f), 27f);
            InventoryUiFactory.Stretch(label.rectTransform);
            label.alignment = TextAlignmentOptions.Center;

            GameObject viewportRoot = new GameObject(
                "ScrollView", typeof(RectTransform),
                typeof(ScrollRect));
            viewportRoot.transform.SetParent(main, false);
            RectTransform scrollRect =
                viewportRoot.GetComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(20f, 208f);
            scrollRect.offsetMax = new Vector2(-20f, -250f);

            GameObject viewportGo = new GameObject(
                "Viewport", typeof(RectTransform),
                typeof(Image), typeof(RectMask2D));
            viewportGo.transform.SetParent(scrollRect, false);
            RectTransform viewport = viewportGo.GetComponent<RectTransform>();
            InventoryUiFactory.Stretch(viewport);
            Image maskBg = viewportGo.GetComponent<Image>();
            maskBg.color = new Color(1f, 1f, 1f, 0.001f);
            maskBg.raycastTarget = true;

            GameObject contentGo = new GameObject(
                "Content", typeof(RectTransform),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewport, false);
            RectTransform content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            VerticalLayoutGroup v = contentGo.GetComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(12, 12, 8, 8);
            v.spacing = 8f;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            ContentSizeFitter fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewportRoot.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            TMP_Text selected = InventoryUiFactory.Text(
                main, "SelectedEquipment", "装備をタップすると詳細を表示します。",
                new Vector2(28f, -20f), new Vector2(920f, 140f), 25f);
            RectTransform selectedRect = selected.rectTransform;
            selectedRect.anchorMin = new Vector2(0f, 0f);
            selectedRect.anchorMax = new Vector2(1f, 0f);
            selectedRect.pivot = new Vector2(0f, 0f);
            selectedRect.anchoredPosition = new Vector2(28f, 25f);
            selectedRect.sizeDelta = new Vector2(-56f, 150f);

            EquipmentInventoryUI inventory =
                root.GetComponent<EquipmentInventoryUI>();
            inventory.SetReferences(content, count, selected, close);
            root.SetActive(false);
            return root;
        }
    }
}
#endif
