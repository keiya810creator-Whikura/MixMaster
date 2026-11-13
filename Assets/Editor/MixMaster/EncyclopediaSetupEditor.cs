#if UNITY_EDITOR
using MixMaster.UI;
using UnityEditor;
using UnityEngine;

namespace MixMaster.EditorTools
{
    public static class EncyclopediaSetupEditor
    {
        [MenuItem("MixMaster/UI/図鑑Prefabを配置")]
        public static void Place()
        {
            EncyclopediaMenuUI existing = Object.FindFirstObjectByType<EncyclopediaMenuUI>(FindObjectsInactive.Include);
            if (existing != null) { Selection.activeGameObject = existing.gameObject; return; }
            Canvas canvas = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<Canvas>() : null;
            if (canvas == null)
                foreach (Canvas candidate in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if (candidate.isRootCanvas && candidate.renderMode != RenderMode.WorldSpace) { canvas = candidate; break; }
            GameObject prefab = Resources.Load<GameObject>(EncyclopediaMenuUI.ResourcePath);
            if (prefab == null || canvas == null)
            {
                Debug.LogError("[Encyclopedia] 画面用CanvasとEncyclopediaMenu.prefabを確認してください。");
                return;
            }
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.rootCanvas.transform);
            Undo.RegisterCreatedObjectUndo(instance, "図鑑Prefabを配置");
            Selection.activeGameObject = instance;
        }
    }
}
#endif
