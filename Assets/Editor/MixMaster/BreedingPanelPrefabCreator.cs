#if UNITY_EDITOR
using System;
using MixMaster.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.EditorTools
{
    /// <summary>
    /// Builds one editable BreedingShopPanel prefab under Resources/UI.
    /// Never overwrites existing assets, so custom layout edits are safe.
    /// </summary>
    [InitializeOnLoad]
    public static class BreedingPanelPrefabCreator
    {
        public const string PrefabPath =
            "Assets/Resources/UI/BreedingShopPanel.prefab";

        static BreedingPanelPrefabCreator()
        {
            EditorApplication.delayCall += AutoCreateIfMissing;
        }

        private static void AutoCreateIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoCreateIfMissing;
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                return;

            try
            {
                CreateIfMissing();
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    "[MixMaster] 配合パネルPrefabの自動作成に失敗: " +
                    e.Message +
                    "（MixMaster/UI/配合パネルPrefabを作成 から再実行できます）");
            }
        }

        [MenuItem("MixMaster/UI/配合パネルPrefabを作成")]
        public static void CreateFromMenu()
        {
            CreateIfMissing();
        }

        public static void CreateIfMissing()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                Debug.Log("[MixMaster] 編集済みの配合パネルPrefabを保護しました: " +
                    PrefabPath);
                EditorGUIUtility.PingObject(existing);
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/UI"))
                AssetDatabase.CreateFolder("Assets/Resources", "UI");

            if (AssetDatabase.LoadAssetAtPath<GameObject>(
                BreedingFilterPrefabCreator.PrefabPath) == null)
                BreedingFilterPrefabCreator.Build();

            // The temporary Canvas gives the prototype builder a proper UI
            // host. Only its BreedingShopPanel child is saved to the asset.
            GameObject temporaryCanvas = new GameObject(
                "TemporaryBreedingPrefabCanvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            try
            {
                Canvas canvas = temporaryCanvas.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 630;
                BreedingPanelUI panel =
                    BreedingPanelUI.BuildPrototype(temporaryCanvas.transform);
                if (panel == null)
                    throw new InvalidOperationException(
                        "BreedingPanelUI.BuildPrototype returned null.");

                if (PrefabUtility.SaveAsPrefabAsset(
                        panel.gameObject, PrefabPath) == null)
                    throw new InvalidOperationException(
                        "PrefabUtility.SaveAsPrefabAsset failed.");

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[MixMaster] 配合パネルPrefabを作成しました: " +
                    PrefabPath +
                    "。今後はこのPrefabを読み込み、実行時にレイアウト生成しません。");
                EditorGUIUtility.PingObject(
                    AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temporaryCanvas);
            }
        }
    }
}
#endif
