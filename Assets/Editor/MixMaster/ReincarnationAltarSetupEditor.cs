#if UNITY_EDITOR
using MixMaster.UI;
using MixMaster.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace MixMaster.EditorTools
{
    public static class ReincarnationAltarSetupEditor
    {
        private const string TriggerName = "ReincarnationAccessTrigger";
        private const string PrefabPath = "Assets/Prefab/UI/ReincarnationPanel.prefab";

        [MenuItem("MixMaster/Map/選択した転生の祭壇を設定")]
        public static void SetupSelectedAltar()
        {
            GameObject altar = Selection.activeGameObject;
            if (altar == null || !altar.scene.IsValid()) return;
            if (altar.name == TriggerName && altar.transform.parent != null)
                altar = altar.transform.parent.gameObject;
            if (altar.GetComponent<Tilemap>() != null || altar.GetComponent<TilemapCollider2D>() != null)
            {
                EditorUtility.DisplayDialog("転生の祭壇", "祭壇のGameObjectか、入口前の空のGameObjectを選択してください。", "OK");
                return;
            }
            Transform child = altar.transform.Find(TriggerName);
            GameObject trigger = child != null ? child.gameObject : new GameObject(TriggerName);
            if (child == null)
            {
                Undo.RegisterCreatedObjectUndo(trigger, "Create Reincarnation Trigger");
                trigger.transform.SetParent(altar.transform, false);
            }
            BoxCollider2D box = trigger.GetComponent<BoxCollider2D>();
            if (box == null) box = Undo.AddComponent<BoxCollider2D>(trigger);
            MapBuildingAccess access = trigger.GetComponent<MapBuildingAccess>();
            if (access == null && box != null) access = Undo.AddComponent<MapBuildingAccess>(trigger);
            if (box == null || access == null)
            {
                Debug.LogError("[ReincarnationAltarSetup] Triggerコンポーネントを追加できませんでした。", trigger);
                return;
            }
            Collider2D solid = altar.GetComponent<Collider2D>();
            SpriteRenderer sprite = altar.GetComponent<SpriteRenderer>();
            Bounds bounds = solid != null ? solid.bounds : sprite != null ? sprite.bounds :
                new Bounds(altar.transform.position, new Vector3(1.5f, 1.5f, 0f));
            Undo.RecordObject(trigger.transform, "Place Reincarnation Trigger");
            Undo.RecordObject(box, "Configure Reincarnation Collider");
            Undo.RecordObject(access, "Configure Reincarnation Access");
            trigger.transform.position = new Vector3(bounds.center.x, bounds.min.y - 0.30f, altar.transform.position.z);
            trigger.transform.localRotation = Quaternion.identity;
            trigger.transform.localScale = Vector3.one;
            Vector3 scale = altar.transform.lossyScale;
            box.size = new Vector2(Mathf.Clamp(bounds.size.x + 0.7f, 1.2f, 7f) /
                Mathf.Max(0.0001f, Mathf.Abs(scale.x)), 1.15f / Mathf.Max(0.0001f, Mathf.Abs(scale.y)));
            box.offset = Vector2.zero;
            box.isTrigger = true;
            access.Configure(MapBuildingType.Reincarnation);
            EditorUtility.SetDirty(box);
            EditorUtility.SetDirty(access);

            ReincarnationPanelUI panel = Object.FindFirstObjectByType<ReincarnationPanelUI>(FindObjectsInactive.Include);
            if (panel == null)
            {
                Canvas canvas = PartyCircularHudPrefabCreator.FindScreenCanvas();
                if (canvas == null) canvas = PartyCircularHudPrefabCreator.CreateCanvas();
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (canvas == null || prefab == null)
                {
                    Debug.LogError("[ReincarnationAltarSetup] Canvasまたは転生パネルPrefabが見つかりません。", altar);
                    return;
                }
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab, canvas.transform) as GameObject;
                if (instance == null) return;
                Undo.RegisterCreatedObjectUndo(instance, "Place Reincarnation Panel");
                panel = instance.GetComponent<ReincarnationPanelUI>();
            }
            if (panel == null) return;
            MapBuildingUIRouter router = Object.FindFirstObjectByType<MapBuildingUIRouter>(FindObjectsInactive.Include);
            if (router == null)
            {
                GameObject go = new GameObject("MapBuildingUIRouter");
                Undo.RegisterCreatedObjectUndo(go, "Create Map Building Router");
                router = Undo.AddComponent<MapBuildingUIRouter>(go);
            }
            if (router == null) return;
            Undo.RecordObject(router, "Connect Reincarnation Panel");
            router.SetReincarnationPanel(panel);
            EditorUtility.SetDirty(router);
            EditorSceneManager.MarkSceneDirty(router.gameObject.scene);
            EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
            EditorSceneManager.MarkSceneDirty(altar.scene);
            PartyCircularHudPrefabCreator.EnsureEventSystem();
            Selection.activeGameObject = trigger;
        }

        [MenuItem("MixMaster/Map/選択した村を起動地点に設定")]
        public static void SetupVillageSpawn()
        {
            GameObject selected = Selection.activeGameObject;
            TilemapMapRoot map = selected != null ? selected.GetComponentInParent<TilemapMapRoot>(true) : null;
            if (map == null || !map.gameObject.scene.IsValid() || map.MapDefinition != null)
            {
                EditorUtility.DisplayDialog("村の起動地点", "村のTilemapMapRootか、その子オブジェクトを選択してください。", "OK");
                return;
            }
            VillageSpawnPoint spawn = map.GetComponentInChildren<VillageSpawnPoint>(true);
            if (spawn == null)
            {
                GameObject go = new GameObject("VillageStartPoint");
                Undo.RegisterCreatedObjectUndo(go, "Create Village Start Point");
                go.transform.SetParent(map.transform, false);
                spawn = Undo.AddComponent<VillageSpawnPoint>(go);
            }
            if (spawn == null) return;
            Undo.RecordObject(spawn, "Set Village Spawn Map");
            spawn.Configure(map);
            EditorUtility.SetDirty(spawn);
            EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
            Selection.activeGameObject = spawn.gameObject;
        }
    }
}
#endif
