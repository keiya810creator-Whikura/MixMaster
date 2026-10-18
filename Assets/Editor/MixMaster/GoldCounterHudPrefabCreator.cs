#if UNITY_EDITOR
using MixMaster.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MixMaster.EditorTools
{
    public static class GoldCounterHudPrefabCreator
    {
        private const string PrefabPath = "Assets/Prefab/UI/GoldCounterHUD.prefab";

        [MenuItem("MixMaster/UI/ゴールド表示HUDを配置")]
        public static void CreateAndPlace()
        {
            GoldCounterHUD existing = Object.FindFirstObjectByType<GoldCounterHUD>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            Canvas canvas = null;
            PlayerCircularStatusHUD playerHud =
                Object.FindFirstObjectByType<PlayerCircularStatusHUD>(FindObjectsInactive.Include);
            if (playerHud != null)
                canvas = playerHud.GetComponentInParent<Canvas>();
            if (canvas == null)
                canvas = PartyCircularHudPrefabCreator.FindScreenCanvas();
            if (canvas == null)
                canvas = PartyCircularHudPrefabCreator.CreateCanvas();

            GoldCounterHUD placed = EnsureAndPlace(canvas);
            if (placed != null)
                Selection.activeGameObject = placed.gameObject;
        }

        internal static GoldCounterHUD EnsureAndPlace(Canvas canvas)
        {
            if (canvas == null) return null;
            canvas = canvas.rootCanvas;
            GoldCounterHUD existing = canvas.GetComponentInChildren<GoldCounterHUD>(true);
            if (existing != null) return existing;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[GoldCounterHUD] ゴールド表示Prefabが見つかりません: " + PrefabPath);
                return null;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, canvas.transform) as GameObject;
            if (instance == null) return null;
            Undo.RegisterCreatedObjectUndo(instance, "Place Gold Counter HUD");
            EditorSceneManager.MarkSceneDirty(instance.scene);
            return instance.GetComponent<GoldCounterHUD>();
        }
    }
}
#endif
