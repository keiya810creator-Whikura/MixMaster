#if UNITY_EDITOR
using MixMaster.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MixMaster.EditorTools
{
    [CustomEditor(typeof(MapExitTransition))]
    public sealed class MapExitTransitionInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            MapExitTransition portal = (MapExitTransition)target;
            TilemapMapRoot sourceMap = portal.GetComponentInParent<TilemapMapRoot>();
            TilemapMapRoot destinationMap = portal.DestinationMap;

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "Destination MapにはHierarchyのTilemapMapRootをドラッグします。" +
                "街はMapSOがなくても指定できます。接続先には反対方向の出入口が必要です。",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(
                sourceMap == null || destinationMap == null))
            {
                if (GUILayout.Button("反対側の出入口にも接続先を自動設定"))
                    SetupReverseLink(sourceMap, destinationMap, portal.ExitDirection);
            }
        }

        private static void SetupReverseLink(
            TilemapMapRoot source, TilemapMapRoot destination,
            MapExitDirection direction)
        {
            if (source == destination ||
                source.gameObject.scene != destination.gameObject.scene)
            {
                EditorUtility.DisplayDialog("マップ接続",
                    "同一シーン内にある別のTilemapMapRootを指定してください。", "OK");
                return;
            }

            MapExitDirection reverse = MapExitTransition.Opposite(direction);
            string path = "_MapConnectionPoints/MapConnection_" +
                MapExitTransition.JapaneseName(reverse);
            Transform opposite = destination.transform.Find(path);
            if (opposite == null || !opposite.gameObject.activeSelf)
            {
                EditorUtility.DisplayDialog("マップ接続",
                    destination.name + " の反対側出入口がありません。\n" +
                    "JSONの該当方向をTRUEにして再生成してください。", "OK");
                return;
            }

            MapExitMarkerEditorSetup.EnsureMarker(opposite, reverse);
            MapExitTransition returnPortal = opposite.GetComponent<MapExitTransition>();
            if (returnPortal == null)
                return;

            if (returnPortal.DestinationMap != null &&
                returnPortal.DestinationMap != source &&
                !EditorUtility.DisplayDialog("既存の接続を変更",
                    "移動先の出入口には別のマップが既に登録されています。\n" +
                    "接続先を " + source.name + " に変更しますか？",
                    "変更する", "キャンセル"))
                return;

            Undo.RecordObject(returnPortal, "Connect Reverse Map Exit");
            returnPortal.SetDestinationMap(source);
            EditorUtility.SetDirty(returnPortal);
            EditorSceneManager.MarkSceneDirty(destination.gameObject.scene);
            Debug.Log("[MapExitTransition] 双方向接続完了: " +
                source.name + " <-> " + destination.name);
        }
    }
}
#endif
