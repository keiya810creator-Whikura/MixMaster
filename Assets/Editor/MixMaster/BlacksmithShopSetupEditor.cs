#if UNITY_EDITOR
using MixMaster.UI;
using MixMaster.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace MixMaster.EditorTools
{
    public static class BlacksmithShopSetupEditor
    {
        private const string MenuPath = "MixMaster/Map/選択した鍛冶屋にアクセスTriggerを追加";

        [MenuItem(MenuPath)]
        public static void SetupSelectedShop()
        {
            GameObject shop = Selection.activeGameObject;
            if (shop == null || !shop.scene.IsValid()) return;
            if (shop.GetComponent<Tilemap>() != null || shop.GetComponent<TilemapCollider2D>() != null)
            {
                EditorUtility.DisplayDialog("鍛冶屋",
                    "鍛冶屋のGameObjectを選択してください。Tilemap上の建物の場合は、" +
                    "入口前に空のGameObjectを置いて選択してください。", "OK");
                return;
            }

            Transform child = shop.transform.Find("BlacksmithAccessTrigger");
            GameObject trigger = child != null ? child.gameObject : new GameObject("BlacksmithAccessTrigger");
            if (child == null)
            {
                Undo.RegisterCreatedObjectUndo(trigger, "Create Blacksmith Access Trigger");
                trigger.transform.SetParent(shop.transform, false);
            }

            Collider2D solid = shop.GetComponent<Collider2D>();
            SpriteRenderer sprite = shop.GetComponent<SpriteRenderer>();
            Bounds bounds = solid != null ? solid.bounds : sprite != null ? sprite.bounds :
                new Bounds(shop.transform.position, new Vector3(1.5f, 1.5f, 0f));
            BoxCollider2D box = trigger.GetComponent<BoxCollider2D>() ?? Undo.AddComponent<BoxCollider2D>(trigger);
            MapBuildingAccess access = trigger.GetComponent<MapBuildingAccess>() ??
                Undo.AddComponent<MapBuildingAccess>(trigger);
            Undo.RecordObject(box, "Configure Blacksmith Trigger");
            Undo.RecordObject(access, "Configure Blacksmith Interaction");
            Undo.RecordObject(trigger.transform, "Place Blacksmith Trigger");

            // Use the same approach area as the existing breeding shop;
            // leave the building's solid collider untouched.
            trigger.transform.position = new Vector3(bounds.center.x,
                bounds.min.y - 0.30f, shop.transform.position.z);
            trigger.transform.localRotation = Quaternion.identity;
            trigger.transform.localScale = Vector3.one;
            Vector3 scale = shop.transform.lossyScale;
            box.size = new Vector2(Mathf.Clamp(bounds.size.x + 0.7f, 1.2f, 7f) /
                Mathf.Max(0.0001f, Mathf.Abs(scale.x)), 1.15f /
                Mathf.Max(0.0001f, Mathf.Abs(scale.y)));
            box.offset = Vector2.zero;
            access.Configure(MapBuildingType.Blacksmith);
            EditorUtility.SetDirty(box);
            EditorUtility.SetDirty(access);

            Canvas canvas = PartyCircularHudPrefabCreator.FindScreenCanvas() ??
                PartyCircularHudPrefabCreator.CreateCanvas();
            PlayerStatusMenuUI status = canvas.GetComponentInChildren<PlayerStatusMenuUI>(true);
            // EnsureAndConnect creates/connects the inventory as part of its
            // normal setup when the status menu is missing.
            if (status == null) status = PlayerStatusMenuPrefabCreator.EnsureAndConnect(canvas);
            EquipmentInventoryUI inventory = EquipmentInventoryPrefabCreator.EnsureAndConnect(canvas, status);
            if (inventory == null)
            {
                Debug.LogError("[BlacksmithShopSetup] 装備インベントリを接続できませんでした。", shop);
                return;
            }
            MapBuildingUIRouter router = Object.FindFirstObjectByType<MapBuildingUIRouter>(FindObjectsInactive.Include);
            if (router == null)
            {
                GameObject routerObject = new GameObject("MapBuildingUIRouter");
                Undo.RegisterCreatedObjectUndo(routerObject, "Create Map Building Router");
                router = Undo.AddComponent<MapBuildingUIRouter>(routerObject);
            }
            Undo.RecordObject(router, "Connect Blacksmith Inventory");
            router.SetBlacksmithInventory(inventory);
            EditorUtility.SetDirty(router);
            PartyCircularHudPrefabCreator.EnsureEventSystem();
            EditorSceneManager.MarkSceneDirty(shop.scene);
            EditorSceneManager.MarkSceneDirty(router.gameObject.scene);
            Selection.activeGameObject = trigger;
            Debug.Log("[BlacksmithShopSetup] 鍛冶屋の接触Triggerと装備インベントリを接続しました。", shop);
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateSelection() => Selection.activeGameObject != null &&
            Selection.activeGameObject.scene.IsValid();
    }
}
#endif
