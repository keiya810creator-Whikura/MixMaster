#if UNITY_EDITOR
using MixMaster.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace MixMaster.EditorTools
{
    /// <summary>
    /// Configures approach-only access for a manually placed shop without
    /// changing its existing solid collision (or the town's MapSO setup).
    /// </summary>
    public static class BreedingShopSetupEditor
    {
        [MenuItem("MixMaster/Map/選択した配合屋にアクセスTriggerを追加")]
        public static void SetupSelectedShop()
        {
            GameObject shop = Selection.activeGameObject;
            if (shop == null || !shop.scene.IsValid())
            {
                EditorUtility.DisplayDialog("配合屋",
                    "Hierarchyで街に配置した配合屋GameObjectを選択してください。", "OK");
                return;
            }

            if (shop.GetComponent<Tilemap>() != null ||
                shop.GetComponent<TilemapCollider2D>() != null)
            {
                EditorUtility.DisplayDialog("配合屋",
                    "Tilemap全体は選択できません。配合屋の前に空のGameObjectを置いて選択してください。\n" +
                    "そのGameObjectに入口Triggerを作ります。", "OK");
                return;
            }

            Transform child = shop.transform.Find("BreedingShopAccessTrigger");
            GameObject triggerObject;
            if (child == null)
            {
                triggerObject = new GameObject("BreedingShopAccessTrigger");
                Undo.RegisterCreatedObjectUndo(triggerObject,
                    "Create Breeding Shop Access Trigger");
                triggerObject.transform.SetParent(shop.transform, false);
            }
            else
            {
                triggerObject = child.gameObject;
            }

            Collider2D shopCollision = shop.GetComponent<Collider2D>();
            SpriteRenderer shopSprite = shop.GetComponent<SpriteRenderer>();

            Bounds bounds = shopCollision != null
                ? shopCollision.bounds
                : shopSprite != null
                    ? shopSprite.bounds
                    : new Bounds(shop.transform.position, new Vector3(1.5f, 1.5f, 0f));

            BoxCollider2D box = triggerObject.GetComponent<BoxCollider2D>();
            if (box == null)
                box = Undo.AddComponent<BoxCollider2D>(triggerObject);

            MapBuildingAccess access = triggerObject.GetComponent<MapBuildingAccess>();
            if (access == null)
                access = Undo.AddComponent<MapBuildingAccess>(triggerObject);

            Undo.RecordObject(box, "Configure Breeding Shop Trigger");
            Undo.RecordObject(access, "Configure Breeding Shop Interaction");
            Undo.RecordObject(triggerObject.transform, "Place Breeding Shop Trigger");

            // Outside/below the shop's solid collision, so a player can walk
            // close enough to interact without crossing the building wall.
            Vector3 worldPosition = new Vector3(bounds.center.x,
                bounds.min.y - 0.30f, shop.transform.position.z);
            triggerObject.transform.position = worldPosition;
            triggerObject.transform.localRotation = Quaternion.identity;
            triggerObject.transform.localScale = Vector3.one;

            Vector3 parentScale = shop.transform.lossyScale;
            float sx = Mathf.Max(0.0001f, Mathf.Abs(parentScale.x));
            float sy = Mathf.Max(0.0001f, Mathf.Abs(parentScale.y));
            box.size = new Vector2(
                Mathf.Clamp(bounds.size.x + 0.7f, 1.2f, 7f) / sx,
                1.15f / sy);
            box.offset = Vector2.zero;
            box.isTrigger = true;
            access.Configure(MapBuildingType.BreedingShop);
            EditorUtility.SetDirty(box);
            EditorUtility.SetDirty(access);
            EditorSceneManager.MarkSceneDirty(shop.scene);

            Selection.activeGameObject = triggerObject;
            Debug.Log("[BreedingShopSetupEditor] 配合屋のアクセスTriggerを設定しました: " +
                shop.name + "（既存コリジョンは変更しません）");
        }

        [MenuItem("MixMaster/Map/選択した配合屋にアクセスTriggerを追加", true)]
        private static bool ValidateSelection()
        {
            return Selection.activeGameObject != null &&
                Selection.activeGameObject.scene.IsValid();
        }
    }
}
#endif
