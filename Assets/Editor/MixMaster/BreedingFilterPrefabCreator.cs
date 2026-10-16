#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using MixMaster.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.EditorTools
{
    /// <summary>
    /// Pre-bakes TextMeshPro dropdown templates. This avoids constructing TMP
    /// dropdowns in Play Mode and uses exactly the same option definitions as
    /// the owned monster inventory.
    /// </summary>
    [InitializeOnLoad]
    public static class BreedingFilterPrefabCreator
    {
        public const string PrefabPath = "Assets/Resources/UI/BreedingFilterControls.prefab";

        private static readonly string[] ThresholdOptions =
        {
            "上位25%", "上位50%", "上位75%", "0以外"
        };

        static BreedingFilterPrefabCreator()
        {
            EditorApplication.delayCall += CreateIfMissing;
        }

        private static void CreateIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += CreateIfMissing;
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                return;

            try
            {
                Build();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MixMaster] 配合フィルターPrefab自動生成に失敗: " +
                    ex.Message + "。MixMaster/UI/配合フィルターPrefabを再生成 を実行してください。");
            }
        }

        [MenuItem("MixMaster/UI/配合フィルターPrefabを再生成")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/UI"))
                AssetDatabase.CreateFolder("Assets/Resources", "UI");

            GameObject root = new GameObject("BreedingFilterControls",
                typeof(RectTransform), typeof(VerticalLayoutGroup),
                typeof(LayoutElement));
            try
            {
                RectTransform rootRect = root.GetComponent<RectTransform>();
                rootRect.anchorMin = new Vector2(0f, 1f);
                rootRect.anchorMax = new Vector2(1f, 1f);
                rootRect.pivot = new Vector2(0.5f, 1f);
                rootRect.sizeDelta = new Vector2(0f, 106f);

                LayoutElement rootLayout = root.GetComponent<LayoutElement>();
                rootLayout.preferredHeight = 106f;
                rootLayout.minHeight = 100f;
                VerticalLayoutGroup columns = root.GetComponent<VerticalLayoutGroup>();
                columns.padding = new RectOffset(2, 2, 2, 2);
                columns.spacing = 6f;
                columns.childControlWidth = true;
                columns.childControlHeight = true;
                columns.childForceExpandWidth = true;
                columns.childForceExpandHeight = false;

                RectTransform top = Row(root.transform, "TopRow");
                RectTransform bottom = Row(root.transform, "BottomRow");

                List<string> sortNames = new List<string>
                {
                    "入手順", "レベル"
                };
                for (int i = 1; i < InventoryStatFilter.Labels.Length; i++)
                    sortNames.Add(InventoryStatFilter.Labels[i]);
                sortNames.Add("お気に入り優先");
                sortNames.Add("ロック優先");

                Dropdown(top, "SortDropdown", sortNames);
                Dropdown(top, "StatFilter",
                    InventoryProtectionIcons.CombinedStatFilterLabels);
                Dropdown(bottom, "ThresholdFilter", ThresholdOptions);
                Button direction = InventoryUiFactory.Button(
                    bottom, "SortDirection", "降順",
                    Vector2.zero, new Vector2(320f, 46f));
                RectTransform buttonRect = direction.GetComponent<RectTransform>();
                InventoryUiFactory.Stretch(buttonRect);
                LayoutElement buttonLayout = direction.gameObject.AddComponent<LayoutElement>();
                buttonLayout.preferredWidth = 300f;
                buttonLayout.flexibleWidth = 1f;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (saved == null)
                    throw new InvalidOperationException("Prefabの保存に失敗しました。");
                AssetDatabase.SaveAssets();
                Debug.Log("[MixMaster] 配合フィルターPrefabを更新しました: " + PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static RectTransform Row(Transform parent, string name)
        {
            GameObject go = new GameObject(name,
                typeof(RectTransform), typeof(HorizontalLayoutGroup),
                typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            LayoutElement height = go.GetComponent<LayoutElement>();
            height.preferredHeight = 48f;
            height.minHeight = 45f;
            HorizontalLayoutGroup layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 7f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return go.GetComponent<RectTransform>();
        }

        private static void Dropdown(
            Transform parent, string name, IList<string> choices)
        {
            TMP_Dropdown dropdown = InventoryUiFactory.Dropdown(parent, name,
                Vector2.zero, new Vector2(320f, 46f), choices);
            LayoutElement element = dropdown.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 300f;
            element.flexibleWidth = 1f;

            // Runtime LayoutGroups size this dropdown. Stretch the caption,
            // otherwise a long label can overflow a narrow mobile screen.
            if (dropdown.captionText != null)
            {
                RectTransform caption = dropdown.captionText.rectTransform;
                InventoryUiFactory.Stretch(caption);
                caption.offsetMin = new Vector2(8f, 3f);
                caption.offsetMax = new Vector2(-8f, -3f);
                dropdown.captionText.fontSizeMin = 13f;
                dropdown.captionText.fontSizeMax = 19f;
            }
            if (dropdown.itemText != null)
            {
                RectTransform item = dropdown.itemText.rectTransform;
                InventoryUiFactory.Stretch(item);
                item.offsetMin = new Vector2(7f, 2f);
                item.offsetMax = new Vector2(-7f, -2f);
                dropdown.itemText.fontSizeMin = 12f;
                dropdown.itemText.fontSizeMax = 18f;
            }
        }
    }
}
#endif
