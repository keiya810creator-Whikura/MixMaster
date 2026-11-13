#if UNITY_EDITOR
using MixMaster.UI;
using UnityEditor;
using UnityEngine;

namespace MixMaster.EditorTools
{
    public static class GameSettingsSetupEditor
    {
        [MenuItem("MixMaster/UI/設定画面Prefabを配置")]
        public static void Place()
        {
            var existing = Object.FindFirstObjectByType<GameSettingsMenuUI>(FindObjectsInactive.Include);
            if (existing != null) { Selection.activeGameObject = existing.gameObject; return; }
            Canvas canvas = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<Canvas>() : null;
            if (canvas == null)
                foreach (Canvas candidate in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if (candidate.isRootCanvas && candidate.renderMode != RenderMode.WorldSpace) { canvas = candidate; break; }
            var prefab = Resources.Load<GameObject>(GameSettingsMenuUI.ResourcePath);
            if (canvas == null || prefab == null) { Debug.LogError("[GameSettings] CanvasとGameSettingsMenu.prefabを確認してください。"); return; }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.rootCanvas.transform);
            Undo.RegisterCreatedObjectUndo(instance, "設定画面Prefabを配置"); Selection.activeGameObject = instance;
        }
    }
}
#endif
