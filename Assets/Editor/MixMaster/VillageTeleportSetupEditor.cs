#if UNITY_EDITOR
using MixMaster.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MixMaster.EditorTools
{
    public static class VillageTeleportSetupEditor
    {
        [MenuItem("MixMaster/Map/選択した魔方陣に転移を設定")]
        private static void ConfigureSelected()
        {
            GameObject circle = Selection.activeGameObject;
            var village = circle != null ? circle.GetComponentInParent<TilemapMapRoot>() : null;
            if (village == null || circle == village.gameObject ||
                (village.MapDefinition != null && !village.MapDefinition.isTown))
            {
                EditorUtility.DisplayDialog("転移魔方陣",
                    "村のTilemapMapRoot配下にある魔方陣オブジェクトを選択してください。", "OK");
                return;
            }

            var collider = circle.GetComponent<Collider2D>();
            if (collider == null) collider = Undo.AddComponent<BoxCollider2D>(circle);
            Undo.RecordObject(collider, "Configure Teleport Trigger");

            var access = circle.GetComponent<MapBuildingAccess>();
            if (access == null) access = Undo.AddComponent<MapBuildingAccess>(circle);
            Undo.RecordObject(access, "Configure Teleport Circle");
            access.Configure(MapBuildingType.TeleportCircle);
            PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
            PrefabUtility.RecordPrefabInstancePropertyModifications(access);
            EditorSceneManager.MarkSceneDirty(circle.scene);
        }
    }
}
#endif
