#if UNITY_EDITOR
using MixMaster.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.EditorTools
{
    /// <summary>
    /// Optional scene-editable version of the automatically displayed party
    /// behavior selector. The same control is created at runtime for existing
    /// scenes that have not yet run this menu command.
    /// </summary>
    public static class PartyCombatOrdersHudPrefabCreator
    {
        private const string PrefabPath =
            "Assets/Prefab/UI/PartyCombatOrdersHUD.prefab";

        [MenuItem("MixMaster/UI/パーティ全体行動HUDを作成・配置")]
        public static void CreateAndPlace()
        {
            Canvas canvas = PartyCircularHudPrefabCreator.FindScreenCanvas();
            PartyCircularStatusHUD[] partyHuds =
                Object.FindObjectsByType<PartyCircularStatusHUD>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (PartyCircularStatusHUD hud in partyHuds)
            {
                if (hud == null || !hud.gameObject.scene.IsValid())
                    continue;
                Canvas ownerCanvas = hud.GetComponentInParent<Canvas>();
                if (ownerCanvas != null)
                {
                    canvas = ownerCanvas.rootCanvas;
                    break;
                }
            }

            if (canvas == null)
                canvas = PartyCircularHudPrefabCreator.CreateCanvas();

            PartyCombatOrdersHUD orders = EnsureAndPlace(canvas);
            PartyCircularHudPrefabCreator.EnsureEventSystem();
            if (orders != null)
            {
                Selection.activeGameObject = orders.gameObject;
                EditorUtility.DisplayDialog(
                    "パーティ全体行動HUD",
                    "戦闘画面に行動プルダウンを配置・接続しました。" +
                    "\n「個別設定を優先」が初期値です。" +
                    "\nパーティの行動を一括指定しても個別設定は保持されます。" +
                    "\n\n位置はRectTransformから調整できます。",
                    "OK");
            }
        }

        internal static PartyCombatOrdersHUD EnsureAndPlace(Canvas canvas)
        {
            if (canvas == null) return null;
            canvas = canvas.rootCanvas;

            PartyCombatOrdersHUD existing =
                canvas.GetComponentInChildren<PartyCombatOrdersHUD>(true);
            if (existing != null)
                return existing;

            PartyCircularHudPrefabCreator.EnsureFolder("Assets/Prefab/UI");
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                GameObject root = new GameObject(
                    "PartyCombatOrdersHUD", typeof(RectTransform),
                    typeof(Image), typeof(PartyCombatOrdersHUD));
                try
                {
                    PartyCombatOrdersHUD.ConfigureRect(
                        root.GetComponent<RectTransform>());
                    root.GetComponent<PartyCombatOrdersHUD>().BuildVisuals();
                    prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }

            if (prefab == null)
            {
                Debug.LogError("[PartyCombatOrdersHUD] Prefab生成に失敗しました。");
                return null;
            }

            GameObject placed =
                PrefabUtility.InstantiatePrefab(prefab, canvas.transform)
                    as GameObject;
            if (placed == null) return null;

            Undo.RegisterCreatedObjectUndo(placed, "Place Party Orders HUD");
            placed.transform.SetAsLastSibling();
            PartyCombatOrdersHUD orders =
                placed.GetComponent<PartyCombatOrdersHUD>();
            if (orders != null)
                orders.BuildVisuals();
            EditorSceneManager.MarkSceneDirty(placed.scene);
            AssetDatabase.SaveAssets();
            return orders;
        }
    }
}
#endif
