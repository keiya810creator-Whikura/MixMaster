#if UNITY_EDITOR
using MixMaster.UI;
using MixMaster.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace MixMaster.EditorTools
{
    public static class ConsumableShopSetupEditor
    {
        private const string TriggerName = "ConsumableShopAccessTrigger";

        [MenuItem("MixMaster/UI/消耗品UIを配置・接続")]
        public static void PlaceUI()
        {
            Canvas canvas = PartyCircularHudPrefabCreator.FindScreenCanvas();
            if (canvas == null) canvas = PartyCircularHudPrefabCreator.CreateCanvas();
            if (canvas == null) return;
            ConsumablePanelUI shop = ConsumablePanelUI.Find(true);
            if (shop == null) shop = Place("ConsumableShopPanel", canvas)?.GetComponent<ConsumablePanelUI>();
            if (ConsumablePanelUI.Find(false) == null) Place("ConsumableInventoryPanel", canvas);
            if (Object.FindFirstObjectByType<ConsumableShortcutsHUD>(FindObjectsInactive.Include) == null)
                Place("ConsumableShortcutsHUD", canvas);
            MapBuildingUIRouter router = Object.FindFirstObjectByType<MapBuildingUIRouter>(FindObjectsInactive.Include);
            if (router == null)
            {
                var root = new GameObject("MapBuildingUIRouter");
                Undo.RegisterCreatedObjectUndo(root, "Create Building Router");
                router = Undo.AddComponent<MapBuildingUIRouter>(root);
            }
            if (router != null)
            {
                Undo.RecordObject(router, "Connect Consumable Shop");
                router.SetConsumableShop(shop);
                EditorUtility.SetDirty(router);
                EditorSceneManager.MarkSceneDirty(router.gameObject.scene);
            }
            PartyCircularHudPrefabCreator.EnsureEventSystem();
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        }
        private static GameObject Place(string name, Canvas canvas)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/" + name + ".prefab");
            if (prefab == null) { Debug.LogError("消耗品UI Prefabが見つかりません：" + name); return null; }
            var instance = PrefabUtility.InstantiatePrefab(prefab, canvas.transform) as GameObject;
            if (instance != null) Undo.RegisterCreatedObjectUndo(instance, "Place " + name);
            return instance;
        }

        [MenuItem("MixMaster/Map/選択した消耗品屋にアクセスTriggerを追加")]
        public static void SetupSelectedShop()
        {
            GameObject shop = Selection.activeGameObject;
            if (shop == null || !shop.scene.IsValid()) return;
            if (shop.name == TriggerName && shop.transform.parent != null) shop = shop.transform.parent.gameObject;
            if (shop.GetComponent<Tilemap>() != null || shop.GetComponent<TilemapCollider2D>() != null)
            {
                EditorUtility.DisplayDialog("消耗品屋", "建物のGameObject、または入口前に置いた空のGameObjectを選択してください。", "OK");
                return;
            }
            Transform existing = shop.transform.Find(TriggerName);
            GameObject trigger = existing != null ? existing.gameObject : new GameObject(TriggerName);
            if (existing == null)
            {
                Undo.RegisterCreatedObjectUndo(trigger, "Create Consumable Shop Trigger");
                trigger.transform.SetParent(shop.transform, false);
            }
            var box = trigger.GetComponent<BoxCollider2D>();
            if (box == null) box = Undo.AddComponent<BoxCollider2D>(trigger);
            var access = trigger.GetComponent<MapBuildingAccess>();
            if (access == null) access = Undo.AddComponent<MapBuildingAccess>(trigger);
            if (box == null || access == null) return;
            Collider2D solid = shop.GetComponent<Collider2D>();
            SpriteRenderer sprite = shop.GetComponent<SpriteRenderer>();
            Bounds bounds = solid != null ? solid.bounds : sprite != null ? sprite.bounds :
                new Bounds(shop.transform.position, new Vector3(1.5f, 1.5f, 0f));
            Undo.RecordObject(box, "Configure Shop Collider");
            Undo.RecordObject(access, "Configure Shop Access");
            Undo.RecordObject(trigger.transform, "Place Shop Trigger");
            trigger.transform.position = new Vector3(bounds.center.x, bounds.min.y - 0.3f, shop.transform.position.z);
            trigger.transform.localRotation = Quaternion.identity;
            trigger.transform.localScale = Vector3.one;
            Vector3 scale = shop.transform.lossyScale;
            box.size = new Vector2(Mathf.Clamp(bounds.size.x + 0.7f, 1.2f, 7f) / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                1.15f / Mathf.Max(0.0001f, Mathf.Abs(scale.y)));
            box.offset = Vector2.zero; box.isTrigger = true;
            access.Configure(MapBuildingType.ConsumableShop);
            EditorUtility.SetDirty(box); EditorUtility.SetDirty(access);
            PlaceUI();
            EditorSceneManager.MarkSceneDirty(shop.scene);
            Selection.activeGameObject = trigger;
        }
    }
}
#endif
