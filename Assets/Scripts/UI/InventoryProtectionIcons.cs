using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    /// <summary>
    /// Protection icon views shared by pooled monster rows and equipment rows.
    /// Inspector-assigned sprites are preferred. Self-contained icon sprites
    /// are generated once at runtime when no custom sprites are assigned.
    /// </summary>
    public static class InventoryProtectionIcons
    {
        public static readonly string[] FilterOptions =
        {
            "すべて", "設定ありのみ", "設定なしのみ"
        };

        private static Sprite defaultFavorite;
        private static Sprite defaultLock;

        public static bool Pass(bool value, int filter)
        {
            return filter == 0 || (filter == 1 ? value : !value);
        }

        public static void Set(Transform row, bool favorite, bool locked,
            Sprite favoriteSprite, Sprite lockSprite,
            Vector2 favoritePosition, Vector2 lockPosition, float size = 34f)
        {
            Image star = GetOrCreate(row, "FavoriteSprite", favoritePosition, size);
            Image padlock = GetOrCreate(row, "LockSprite", lockPosition, size);
            star.sprite = favoriteSprite != null ? favoriteSprite :
                (defaultFavorite != null ? defaultFavorite : (defaultFavorite = MakeStar()));
            padlock.sprite = lockSprite != null ? lockSprite :
                (defaultLock != null ? defaultLock : (defaultLock = MakeLock()));
            star.enabled = favorite;
            padlock.enabled = locked;
        }

        private static Image GetOrCreate(Transform row, string name,
            Vector2 position, float size)
        {
            Transform existing = row.Find(name);
            Image image = existing != null ? existing.GetComponent<Image>() : null;
            if (image == null)
            {
                GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(row, false);
                image = go.GetComponent<Image>();
                InventoryUiFactory.TopLeft(image.rectTransform, position,
                    new Vector2(size, size));
            }
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.color = Color.white;
            return image;
        }

        private static Sprite MakeStar()
        {
            return MakeSprite((x, y) =>
            {
                // Five-pointed gold star; alternating radii.
                bool inside = false;
                for (int i = 0, previous = 9; i < 10; previous = i++)
                {
                    float angleA = (i * 36f - 90f) * Mathf.Deg2Rad;
                    float angleB = (previous * 36f - 90f) * Mathf.Deg2Rad;
                    float ra = i % 2 == 0 ? 27f : 12f;
                    float rb = previous % 2 == 0 ? 27f : 12f;
                    float xa = 32f + ra * Mathf.Cos(angleA);
                    float ya = 32f + ra * Mathf.Sin(angleA);
                    float xb = 32f + rb * Mathf.Cos(angleB);
                    float yb = 32f + rb * Mathf.Sin(angleB);
                    if ((ya > y) != (yb > y) &&
                        x < (xb - xa) * (y - ya) / (yb - ya) + xa)
                        inside = !inside;
                }
                return inside;
            }, new Color32(255, 209, 62, 255), "FavoriteStar");
        }

        private static Sprite MakeLock()
        {
            return MakeSprite((x, y) =>
            {
                bool body = x >= 12 && x <= 52 && y >= 7 && y <= 35;
                float dx = x - 32f, dy = y - 34f;
                float sq = dx * dx + dy * dy;
                bool arch = y >= 34 && sq <= 19f * 19f &&
                    sq >= 11f * 11f;
                bool shank = y >= 29 && y <= 39 &&
                    (x >= 13 && x <= 21 || x >= 43 && x <= 51);
                return body || arch || shank;
            }, new Color32(174, 205, 239, 255), "InventoryPadlock");
        }

        private static Sprite MakeSprite(System.Func<int, int, bool> draw,
            Color color, string name)
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            texture.name = name;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[64 * 64];
            Color32 filled = color;
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    pixels[y * 64 + x] = draw(x, y)
                        ? filled : new Color32(0, 0, 0, 0);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, 64, 64),
                new Vector2(0.5f, 0.5f), 64f);
        }
    }
}
