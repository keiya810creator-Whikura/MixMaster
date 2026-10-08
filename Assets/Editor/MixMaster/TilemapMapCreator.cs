#if UNITY_EDITOR
using MixMaster.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace MixMaster.EditorTools
{
    public static class TilemapMapCreator
    {
        [MenuItem("MixMaster/Map/Tilemapマップ土台を作成")]
        public static void CreateTilemapMapRoot()
        {
            GameObject root =
                new GameObject(
                    GameObjectUtility.GetUniqueNameForSibling(
                        null,
                        "TilemapMap"));

            Undo.RegisterCreatedObjectUndo(
                root,
                "Create Tilemap Map");

            Grid grid = root.AddComponent<Grid>();
            grid.cellSize = Vector3.one;
            grid.cellGap = Vector3.zero;
            grid.cellLayout = GridLayout.CellLayout.Rectangle;
            grid.cellSwizzle = GridLayout.CellSwizzle.XYZ;

            Tilemap ground =
                CreateLayer(
                    root.transform,
                    "Ground",
                    0);

            Tilemap decoration =
                CreateLayer(
                    root.transform,
                    "Decoration",
                    5);

            Tilemap collision =
                CreateLayer(
                    root.transform,
                    "Collision",
                    10);

            Tilemap above =
                CreateLayer(
                    root.transform,
                    "Above",
                    20);

            SetupCollision(collision.gameObject);

            TilemapMapRoot mapRoot =
                root.AddComponent<TilemapMapRoot>();

            mapRoot.SetLayers(
                ground,
                decoration,
                collision,
                above);

            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();

            EditorUtility.SetDirty(root);

            Debug.Log(
                "[TilemapMapCreator] Tilemapマップ土台を作成しました。 " +
                "Window > 2D > Tile Palette を開き、Groundから塗ってみてください。",
                root);
        }

        private static Tilemap CreateLayer(
            Transform parent,
            string layerName,
            int sortingOrder)
        {
            GameObject layerObject =
                new GameObject(layerName);

            Undo.RegisterCreatedObjectUndo(
                layerObject,
                "Create " + layerName + " Tilemap");

            layerObject.transform.SetParent(
                parent,
                false);

            Tilemap tilemap =
                layerObject.AddComponent<Tilemap>();

            TilemapRenderer renderer =
                layerObject.AddComponent<TilemapRenderer>();

            renderer.sortingOrder = sortingOrder;

            return tilemap;
        }

        private static void SetupCollision(
            GameObject collisionObject)
        {
            Rigidbody2D body =
                collisionObject.AddComponent<Rigidbody2D>();

            body.bodyType = RigidbodyType2D.Static;
            body.simulated = true;

            CompositeCollider2D composite =
                collisionObject.AddComponent<CompositeCollider2D>();

            composite.geometryType =
                CompositeCollider2D.GeometryType.Polygons;

            TilemapCollider2D tilemapCollider =
                collisionObject.AddComponent<TilemapCollider2D>();

#if UNITY_6000_0_OR_NEWER
            tilemapCollider.compositeOperation =
                Collider2D.CompositeOperation.Merge;
#else
#pragma warning disable CS0618
            tilemapCollider.usedByComposite = true;
#pragma warning restore CS0618
#endif
        }
    }
}
#endif
