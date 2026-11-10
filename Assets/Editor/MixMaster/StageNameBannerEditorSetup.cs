#if UNITY_EDITOR
using MixMaster.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MixMaster.EditorTools
{
    public static class StageNameBannerEditorSetup
    {
        [MenuItem("MixMaster/UI/ステージ名表示をシーンに配置")]
        public static void Create()
        {
            var existing = Object.FindFirstObjectByType<StageNameBannerUI>();
            if (existing != null) { Selection.activeGameObject = existing.gameObject; return; }
            var banner = StageNameBannerUI.CreateDefault();
            Undo.RegisterCreatedObjectUndo(banner.gameObject, "Create Stage Name Banner");
            Selection.activeGameObject = banner.gameObject;
            EditorSceneManager.MarkSceneDirty(banner.gameObject.scene);
        }
    }
}
#endif
