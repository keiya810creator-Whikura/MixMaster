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
    /// Bakes dropdowns and their full option lists into prefab ASSETS.
    /// The game scripts only Find() and bind existing serialized controls.
    /// Automatically upgrades older assets after the code is imported.
    /// </summary>
    [InitializeOnLoad]
    public static class InventoryFilterPrefabBaker
    {
        private const string MonsterPrefab =
            "Assets/Prefab/UI/OwnedMonsterListPanel.prefab";

        private static readonly string[] MonsterThreshold =
        {
            "上位25%", "上位50%", "上位75%", "0以外"
        };

        private static readonly string[] EquipmentRarities = EquipmentRarityUtility.FilterLabels;

        static InventoryFilterPrefabBaker()
        {
            EditorApplication.delayCall += AutoBake;
        }

        private static void AutoBake()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoBake;
                return;
            }

            try
            {
                BakeAll(false);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[MixMaster] Prefabフィルター自動更新に失敗: " +
                    exception.Message +
                    " / MixMaster/UI/インベントリフィルターをPrefabに反映 を実行してください。");
            }
        }

        [MenuItem("MixMaster/UI/インベントリフィルターをPrefabに反映")]
        public static void BakeFromMenu()
        {
            BakeAll(true);
        }

        public static void BakeAll(bool log)
        {
            bool changed = false;

            if (AssetDatabase.LoadAssetAtPath<GameObject>(MonsterPrefab) != null)
                changed |= Bake(MonsterPrefab, true);
            else if (log)
                Debug.LogWarning("[MixMaster] 仲間一覧Prefabがありません。" +
                    " MixMaster/UI/仲間一覧UI Prefabを作成・更新 から生成してください。");

            PartyCircularHudPrefabCreator.EnsureFolder("Assets/Prefab/UI");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(
                EquipmentInventoryPrefabCreator.PrefabPath) == null)
            {
                GameObject prototype = EquipmentInventoryPrefabCreator.BuildPrefab();
                try
                {
                    AssetDatabase.SaveAssets();
                    PrefabUtility.SaveAsPrefabAsset(prototype,
                        EquipmentInventoryPrefabCreator.PrefabPath);
                    changed = true;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(prototype);
                }
            }
            changed |= Bake(EquipmentInventoryPrefabCreator.PrefabPath, false);

            if (changed)
                AssetDatabase.SaveAssets();

            if (log)
                Debug.Log("[MixMaster] 既存のソートと条件フィルターをPrefabに反映しました。" +
                    " 実行時にプルダウンは新規生成されません。");
        }

        private static bool Bake(string prefabPath, bool monster)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            bool changed = false;
            try
            {
                RectTransform host = root.GetComponent<RectTransform>();
                if (host == null)
                    throw new InvalidOperationException(prefabPath +
                        " に RectTransform がありません。");

                changed = monster
                    ? BakeMonster(host) : BakeEquipment(host);
                if (changed)
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return changed;
        }

        private static bool BakeMonster(RectTransform root)
        {
            bool changed = false;
            RectTransform controls = EnsureControls(root, "InventoryFilters",
                -95f, 173f, ref changed);

            string[] sort = BuildSort();
            Dropdown(controls, "SortDropdown", new Vector2(32f, -8f),
                new Vector2(225f, 56f), sort, ref changed);
            Dropdown(controls, "StatFilter", new Vector2(267f, -8f),
                new Vector2(260f, 56f),
                InventoryProtectionIcons.CombinedStatFilterLabels, ref changed);
            Dropdown(controls, "ThresholdFilter", new Vector2(537f, -8f),
                new Vector2(205f, 56f), MonsterThreshold, ref changed);
            Button(controls, "SortDirection", "降順",
                new Vector2(753f, -8f), new Vector2(130f, 56f), ref changed);

            Text(controls, "SortHint", "並び替え",
                new Vector2(36f, -68f), new Vector2(210f, 25f), ref changed);
            Text(controls, "FilterHint", "条件絞り込み",
                new Vector2(271f, -68f), new Vector2(250f, 25f), ref changed);
            Text(controls, "ThresholdHint", "基準",
                new Vector2(542f, -68f), new Vector2(195f, 25f), ref changed);
            Button(controls, "SellFilteredMonsters", "絞り込みを一括売却",
                new Vector2(549f, -99f), new Vector2(330f, 56f), ref changed);
            Button(controls, "AutoSellMonsters", "自動売却設定",
                new Vector2(65f, -99f), new Vector2(335f, 56f), ref changed);

            // Remove obsolete EXTRA dropdowns in older generated prefab versions.
            changed |= DeleteIfPresent(controls, "FavoriteFilter");
            changed |= DeleteIfPresent(controls, "LockFilter");
            changed |= DeleteIfPresent(controls, "FavoriteFilterLabel");
            changed |= DeleteIfPresent(controls, "LockFilterLabel");

            if (changed)
            {
                RectTransform scroll = root.Find("ScrollView") as RectTransform;
                if (scroll != null)
                {
                    Vector2 offset = scroll.offsetMax;
                    scroll.offsetMax = new Vector2(offset.x,
                        Mathf.Min(offset.y, -283f));
                }
            }
            return changed;
        }

        private static bool BakeEquipment(RectTransform root)
        {
            bool changed = false;
            RectTransform controls = EnsureControls(root, "FilterControls",
                -110f, 155f, ref changed);

            Dropdown(controls, "SortDropdown", new Vector2(20f, -6f),
                new Vector2(240f, 54f), BuildSort(), ref changed);
            Dropdown(controls, "StatFilter", new Vector2(270f, -6f),
                new Vector2(245f, 54f),
                InventoryProtectionIcons.CombinedStatFilterLabels, ref changed);
            Dropdown(controls, "RarityFilter", new Vector2(525f, -6f),
                new Vector2(240f, 54f), EquipmentRarities, ref changed);
            Button(controls, "Direction", "降順",
                new Vector2(775f, -6f), new Vector2(135f, 54f), ref changed);

            Text(controls, "SortHint", "並び替え",
                new Vector2(24f, -70f), new Vector2(230f, 28f), ref changed);
            Text(controls, "StatHint", "条件絞り込み",
                new Vector2(273f, -70f), new Vector2(270f, 28f), ref changed);
            Text(controls, "RarityHint", "レア度絞り込み",
                new Vector2(528f, -70f), new Vector2(240f, 28f), ref changed);
            Button(controls, "BulkSellEquipment", "絞り込みを一括売却",
                new Vector2(560f, -96f), new Vector2(310f, 50f), ref changed);
            Button(controls, "AutoSellEquipment", "自動売却設定",
                new Vector2(58f, -96f), new Vector2(330f, 50f), ref changed);

            changed |= DeleteIfPresent(controls, "FavoriteFilter");
            changed |= DeleteIfPresent(controls, "LockFilter");
            changed |= DeleteIfPresent(controls, "FavoriteLabel");
            changed |= DeleteIfPresent(controls, "LockLabel");

            if (changed)
            {
                RectTransform scroll = root.Find("ScrollView") as RectTransform;
                if (scroll != null)
                {
                    Vector2 offset = scroll.offsetMax;
                    scroll.offsetMax = new Vector2(offset.x,
                        Mathf.Min(offset.y, -290f));
                }
            }
            return changed;
        }

        private static string[] BuildSort()
        {
            var options = new List<string> { "入手順", "レベル" };
            for (int i = 1; i < InventoryStatFilter.Labels.Length; i++)
                options.Add(InventoryStatFilter.Labels[i]);
            options.Add("お気に入り優先");
            options.Add("ロック優先");
            return options.ToArray();
        }

        private static RectTransform EnsureControls(RectTransform root,
            string name, float offsetY, float height, ref bool changed)
        {
            Transform child = root.Find(name);
            RectTransform controls = child as RectTransform;
            if (controls == null)
            {
                GameObject go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(root, false);
                controls = go.GetComponent<RectTransform>();
                controls.anchorMin = new Vector2(0f, 1f);
                controls.anchorMax = new Vector2(1f, 1f);
                controls.pivot = new Vector2(0.5f, 1f);
                controls.anchoredPosition = new Vector2(0f, offsetY);
                controls.sizeDelta = new Vector2(0f, height);
                changed = true;
            }
            else if (controls.sizeDelta.y < height)
            {
                controls.sizeDelta = new Vector2(
                    controls.sizeDelta.x, height);
                changed = true;
            }
            return controls;
        }

        private static void Dropdown(RectTransform parent, string name,
            Vector2 position, Vector2 size, string[] choices,
            ref bool changed)
        {
            Transform child = parent.Find(name);
            TMP_Dropdown dropdown = child != null
                ? child.GetComponent<TMP_Dropdown>() : null;
            if (dropdown == null)
            {
                dropdown = InventoryUiFactory.Dropdown(
                    parent, name, position, size, choices);
                changed = true;
                return;
            }

            bool different = dropdown.options.Count != choices.Length;
            if (!different)
            {
                for (int i = 0; i < choices.Length; i++)
                    if (dropdown.options[i].text != choices[i])
                    {
                        different = true;
                        break;
                    }
            }
            if (!different) return;

            int selected = dropdown.value;
            dropdown.ClearOptions();
            dropdown.AddOptions(new List<string>(choices));
            dropdown.SetValueWithoutNotify(
                Mathf.Clamp(selected, 0, choices.Length - 1));
            changed = true;
        }

        private static void Button(RectTransform parent, string name,
            string label, Vector2 position, Vector2 size, ref bool changed)
        {
            Transform child = parent.Find(name);
            if (child == null || child.GetComponent<Button>() == null)
            {
                InventoryUiFactory.Button(parent, name, label, position, size);
                changed = true;
            }
        }

        private static void Text(RectTransform parent, string name,
            string label, Vector2 position, Vector2 size, ref bool changed)
        {
            Transform child = parent.Find(name);
            TMP_Text text = child != null ? child.GetComponent<TMP_Text>() : null;
            if (text == null)
            {
                InventoryUiFactory.Text(parent, name, label,
                    position, size, 18f);
                changed = true;
            }
            else if (text.text != label)
            {
                text.text = label;
                changed = true;
            }
        }

        private static bool DeleteIfPresent(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child == null)
                return false;

            UnityEngine.Object.DestroyImmediate(child.gameObject);
            return true;
        }
    }
}
#endif
