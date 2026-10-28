#if UNITY_EDITOR
using MixMaster.UI;
using UnityEditor;
using UnityEngine;

namespace MixMaster.EditorTools
{
    public static class PartyPresetSetupEditor
    {
        [MenuItem("MixMaster/UI/パーティプリセットPrefabを配置")]
        public static void Place()
        {
            var existing = Object.FindFirstObjectByType<PartyPresetScreenUI>(FindObjectsInactive.Include);
            if (existing != null) { Selection.activeGameObject = existing.gameObject; return; }
            Canvas canvas = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<Canvas>() : null;
            if (canvas == null)
                foreach (Canvas candidate in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if (candidate.isRootCanvas && candidate.renderMode != RenderMode.WorldSpace) { canvas = candidate; break; }
            var prefab = Resources.Load<GameObject>(PartyPresetScreenUI.ResourcePath);
            if (canvas == null || prefab == null) { Debug.LogError("[PartyPresets] CanvasとPartyPresetPanel.prefabを確認してください。"); return; }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.rootCanvas.transform);
            Undo.RegisterCreatedObjectUndo(instance, "パーティプリセットPrefabを配置"); Selection.activeGameObject = instance;
        }
    }
}
#endif
