using UnityEngine;

namespace MixMaster.Combat
{
    // Shared measurements keep player and party swords aligned to world reach.
    public static class SwordSpriteUtility
    {
        public static void MeasureOpaqueBounds(
            Sprite sprite,
            bool horizontal,
            ref float nearEdge,
            ref float visibleLength)
        {
            if (sprite == null)
                return;

            // Tight sprite meshes already exclude transparent margins.
            // Their vertices can be measured even when Read/Write is off.
            Vector2[] vertices = sprite.vertices;
            if (vertices != null && vertices.Length >= 3)
            {
                float minCoordinate = float.MaxValue;
                float maxCoordinate = float.MinValue;
                for (int i = 0; i < vertices.Length; i++)
                {
                    float coordinate = horizontal
                        ? vertices[i].x : vertices[i].y;
                    minCoordinate = Mathf.Min(minCoordinate, coordinate);
                    maxCoordinate = Mathf.Max(maxCoordinate, coordinate);
                }

                if (maxCoordinate > minCoordinate)
                {
                    nearEdge = minCoordinate;
                    visibleLength = maxCoordinate - minCoordinate;
                }
            }

            if (sprite.texture == null || !sprite.texture.isReadable)
                return;

            try
            {
                Texture2D texture = sprite.texture;
                Rect rect = sprite.textureRect;
                Color32[] pixels = texture.GetPixels32();
                int minX = Mathf.Max(0, Mathf.FloorToInt(rect.x));
                int minY = Mathf.Max(0, Mathf.FloorToInt(rect.y));
                int maxX = Mathf.Min(texture.width,
                    Mathf.CeilToInt(rect.xMax));
                int maxY = Mathf.Min(texture.height,
                    Mathf.CeilToInt(rect.yMax));
                int first = horizontal ? maxX : maxY;
                int last = horizontal ? minX : minY;

                for (int y = minY; y < maxY; y++)
                {
                    for (int x = minX; x < maxX; x++)
                    {
                        if (pixels[y * texture.width + x].a < 32)
                            continue;

                        int coordinate = horizontal ? x : y;
                        first = Mathf.Min(first, coordinate);
                        last = Mathf.Max(last, coordinate);
                    }
                }

                if (last < first)
                    return;

                float ppu = Mathf.Max(0.001f, sprite.pixelsPerUnit);
                float pivotPixel = horizontal
                    ? sprite.pivot.x : sprite.pivot.y;
                float rectStart = horizontal ? rect.x : rect.y;
                nearEdge = ((first - rectStart) - pivotPixel) / ppu;
                visibleLength = Mathf.Max(
                    0.01f, (last - first + 1) / ppu);
            }
            catch (UnityException)
            {
                // Tight-packed/non-readable textures use sprite bounds.
            }
        }

        public static Sprite CreateDefault(out Texture2D texture, bool monochrome = false)
        {
            const int width = 20;
            const int height = 80;
            texture = new Texture2D(
                width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 steel = new Color32(224, 237, 252, 255);
            Color32 edge = new Color32(132, 169, 203, 255);
            Color32 gold = new Color32(232, 181, 61, 255);
            Color32 grip = new Color32(78, 52, 48, 255);

            if (monochrome)
            {
                steel = new Color32(255, 255, 255, 255);
                edge = new Color32(200, 200, 200, 255);
                gold = new Color32(255, 255, 255, 255);
                grip = new Color32(180, 180, 180, 255);
            }

            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color32 value = clear;
                    int dx = Mathf.Abs(x - width / 2);
                    if (y >= 15 && y <= 72 && dx <= 3)
                        value = dx == 3 ? edge : steel;
                    if (y >= 73 && y < 80 && dx <= (80 - y) / 2)
                        value = steel;
                    if (y >= 12 && y <= 15 && dx <= 9)
                        value = gold;
                    if (y >= 3 && y < 12 && dx <= 2)
                        value = grip;
                    if (y <= 3 && dx <= 4)
                        value = gold;
                    pixels[y * width + x] = value;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(
                texture, new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f), 80f);
            sprite.name = "_RuntimeSwordSprite";
            return sprite;
        }
    }
}
