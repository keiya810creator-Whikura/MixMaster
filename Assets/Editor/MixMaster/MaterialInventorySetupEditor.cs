#if UNITY_EDITOR
using MixMaster.UI;
using UnityEditor;
using UnityEngine;

namespace MixMaster.EditorTools
{
    public static class MaterialInventorySetupEditor
    {
        [MenuItem("MixMaster/UI/素材インベントリPrefabを配置")]
        public static void Place()
        {
            var existing = Object.FindFirstObjectByType<MaterialInventoryPanelUI>(FindObjectsInactive.Include);
            if (existing != null) { Selection.activeGameObject = existing.gameObject; return; }
            Canvas canvas = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<Canvas>() : null;
            if (canvas == null)
                foreach (Canvas candidate in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if (candidate.isRootCanvas && candidate.renderMode != RenderMode.WorldSpace) { canvas = candidate; break; }
            var prefab = Resources.Load<GameObject>(MaterialInventoryPanelUI.ResourcePath);
            if (prefab == null || canvas == null) { Debug.LogError("[MaterialInventory] 画面用Canvasと素材インベントリPrefabを確認してください。"); return; }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.rootCanvas.transform);
            Undo.RegisterCreatedObjectUndo(instance, "素材インベントリPrefabを配置");
            Selection.activeGameObject = instance;
        }
    }
}
#endif
