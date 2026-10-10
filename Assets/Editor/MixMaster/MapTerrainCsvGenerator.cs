#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MixMaster.Core;
using UnityEditor;
using UnityEngine;

namespace MixMaster.EditorTools
{
    /// <summary>
    /// Batch-produce TilemapLayoutGenerator-compatible layout JSONs from a
    /// per-tile terrain CSV. Image names must match PNG files in CsvSpriteFolder.
    /// Map geometry is procedural and deterministic for a given seed.
    /// </summary>
    public static class MapTerrainCsvGenerator
    {
        private const string CsvPath = "Assets/DataCSV/マップ地形.csv";
        private const string JsonFolder = "Assets/Data/MapLayouts";
        public const string CsvSpriteFolder = "Assets/素材/map/Tiles/CSV";

        private const string Menu = "MixMaster/Map/地形CSVからJSONを一括生成";
        private const string OverwriteMenu = "MixMaster/Map/地形CSVからJSONを一括生成 (既存JSON上書き)";

        [Serializable]
        private sealed class JsonLegend
        {
            public string symbol;
            public string tile;
            public string layer;
            public bool collision;
        }

        [Serializable]
        private sealed class JsonLayout
        {
            public string mapName;
            public int width;
            public int height;
            public string defaultGroundTile;
            public bool blockOuterBorder = true;
            public JsonLegend[] legend;
            public string[] rows;
        }

        private enum Category { Ground, Road, Decoration, Collision }

        private sealed class TileSpec
        {
            public Category category;
            public string name;
            public float percent;
            public char symbol;
        }

        private sealed class MapSpec
        {
            public string name;
            public int width;
            public int height;
            public int seed;
            public bool invalid;
            public readonly List<TileSpec> tiles = new List<TileSpec>();
        }

        [MenuItem(Menu)]
        public static void Generate()
        {
            GenerateInternal(false);
        }

        [MenuItem(OverwriteMenu)]
        public static void GenerateAndOverwrite()
        {
            if (EditorUtility.DisplayDialog(
                "地形CSVからJSON生成",
                "既存の同名JSONを上書きします。手編集した配置も失われます。続けますか？",
                "上書きする", "キャンセル"))
            {
                GenerateInternal(true);
            }
        }

        private static void GenerateInternal(bool overwrite)
        {
            string absoluteCsv = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, CsvPath);

            if (!File.Exists(absoluteCsv))
            {
                EditorUtility.DisplayDialog("マップ地形CSV",
                    "CSVが見つかりません。\n" + CsvPath, "OK");
                return;
            }

            EnsureFolder(JsonFolder);
            AssetDatabase.Refresh();

            List<List<string>> rows;
            try
            {
                rows = ParseCsv(ReadText(absoluteCsv));
            }
            catch (Exception ex)
            {
                Debug.LogError("[MapTerrainCsvGenerator] CSV解析エラー: " + ex.Message);
                return;
            }

            if (rows.Count == 0 || !IsHeaderValid(rows[0]))
            {
                EditorUtility.DisplayDialog("マップ地形CSV",
                    "ヘッダーは次の7列を使用してください:\n" +
                    "地形生成用JSON名,幅,高さ,Seed,種類,素材名,配置率", "OK");
                return;
            }

            var maps = new Dictionary<string, MapSpec>(StringComparer.OrdinalIgnoreCase);
            var order = new List<MapSpec>();
            int warnings = 0;

            for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
            {
                List<string> row = rows[rowIndex];
                if (IsBlank(row))
                    continue;

                int number = rowIndex + 1;
                if (row.Count != 7)
                {
                    Debug.LogError("[MapTerrainCsvGenerator] " + number +
                        "行目: 7列で記述してください。");
                    warnings++;
                    continue;
                }

                string name = Cell(row, 0);
                string tileName = Cell(row, 5);

                if (string.IsNullOrWhiteSpace(name) ||
                    string.IsNullOrWhiteSpace(tileName))
                {
                    Debug.LogError("[MapTerrainCsvGenerator] " + number +
                        "行目: JSON名または素材名が空です。");
                    warnings++;
                    continue;
                }

                Category category;
                if (!TryCategory(Cell(row, 4), out category))
                {
                    Debug.LogError("[MapTerrainCsvGenerator] " + number +
                        "行目: 不明な種類: " + Cell(row, 4));
                    warnings++;
                    continue;
                }

                int width, height, seed;
                float percent;

                if (!int.TryParse(Cell(row, 1), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out width) ||
                    !int.TryParse(Cell(row, 2), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out height) ||
                    !int.TryParse(Cell(row, 3), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out seed) ||
                    !float.TryParse(Cell(row, 6), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out percent) ||
                    width < 12 || height < 12 || width > 250 || height > 250 ||
                    percent < 0f || percent > 100f)
                {
                    Debug.LogError("[MapTerrainCsvGenerator] " + number +
                        "行目: 幅/高さは12～250、Seedは整数、配置率は0～100を指定してください。");
                    warnings++;
                    if (maps.TryGetValue(name, out MapSpec invalidMap))
                        invalidMap.invalid = true;
                    continue;
                }

                if (!maps.TryGetValue(name, out MapSpec spec))
                {
                    spec = new MapSpec
                    {
                        name = name, width = width, height = height, seed = seed
                    };
                    maps.Add(name, spec);
                    order.Add(spec);
                }
                else if (spec.width != width || spec.height != height || spec.seed != seed)
                {
                    Debug.LogError("[MapTerrainCsvGenerator] " + name +
                        ": 同じJSON名の幅・高さ・Seedが一致していません (CSV " +
                        number + "行目)。");
                    spec.invalid = true;
                    warnings++;
                    continue;
                }

                foreach (TileSpec existing in spec.tiles)
                {
                    if (string.Equals(existing.name, tileName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.LogError("[MapTerrainCsvGenerator] " + name +
                            ": 素材名が重複しています: " + tileName);
                        spec.invalid = true;
                        break;
                    }
                }

                spec.tiles.Add(new TileSpec
                {
                    category = category, name = tileName, percent = percent
                });
            }

            int created = 0, skipped = 0;
            foreach (MapSpec map in order)
            {
                string path = JsonFolder + "/" + Sanitize(map.name) + ".json";
                if (map.invalid)
                {
                    skipped++;
                    continue;
                }

                if (File.Exists(Path.Combine(
                    Directory.GetParent(Application.dataPath).FullName, path)) &&
                    !overwrite)
                {
                    Debug.Log("[MapTerrainCsvGenerator] 既存JSONを維持: " + path);
                    skipped++;
                    continue;
                }

                if (!ValidateMap(map))
                {
                    skipped++;
                    warnings++;
                    continue;
                }

                JsonLayout layout = BuildLayout(map);
                string json = JsonUtility.ToJson(layout, true) + "\n";
                string absolute = Path.Combine(
                    Directory.GetParent(Application.dataPath).FullName, path);
                File.WriteAllText(absolute, json, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                LinkExistingMapSo(map.name, AssetDatabase.LoadAssetAtPath<TextAsset>(path));
                created++;
                Debug.Log("[MapTerrainCsvGenerator] JSON生成: " + path +
                    " (" + map.width + "x" + map.height + ", Seed=" +
                    map.seed + ")");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            string summary = "生成・更新 " + created +
                " / スキップ " + skipped + " / エラー等 " + warnings;
            Debug.Log("[MapTerrainCsvGenerator] " + summary);
            EditorUtility.DisplayDialog("マップ地形CSV", summary, "OK");
        }

        private static bool ValidateMap(MapSpec map)
        {
            int groundCount = 0, symbolCount = 0;
            float roadPercent = 0f, propPercent = 0f;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (TileSpec tile in map.tiles)
            {
                if (!names.Add(tile.name))
                {
                    Debug.LogError("[MapTerrainCsvGenerator] 重複する素材名: " + tile.name);
                    return false;
                }

                if (tile.category == Category.Ground) groundCount++;
                else
                {
                    symbolCount++;
                    if (tile.category == Category.Road)
                        roadPercent += tile.percent;
                    else
                        propPercent += tile.percent;
                }

                if (FindOrImportSprite(tile.name) == null)
                {
                    Debug.LogError("[MapTerrainCsvGenerator] 素材がありません: " +
                        tile.name + " / " + CsvSpriteFolder + "/" +
                        tile.name + ".png (画像名とCSV名を一致させてください)");
                    return false;
                }
            }

            if (groundCount != 1 || symbolCount > Symbols.Length ||
                roadPercent > 40f || propPercent > 100f)
            {
                Debug.LogError("[MapTerrainCsvGenerator] " + map.name +
                    ": 地面は1種類、その他は最大 " + Symbols.Length +
                    " 種類、道の配置率合計40%以下、オブジェクトの配置率合計100%以下にしてください。");
                return false;
            }

            return true;
        }

        // Exclude '.', 'A', 'D', and 'P': reserved by TilemapLayoutGenerator.
        private const string SymbolAlphabet = "abcdefghijklmnoqrstuvwxyz0123456789BCEFGHIJKLMNOQRSTUVWXYZ";
        private static readonly char[] Symbols =
            SymbolAlphabet.ToCharArray();

        private static JsonLayout BuildLayout(MapSpec map)
        {
            System.Random random = new System.Random(map.seed);
            TileSpec ground = null;
            var roads = new List<TileSpec>();
            var props = new List<TileSpec>();
            var legend = new List<JsonLegend>();
            int nextSymbol = 0;

            foreach (TileSpec tile in map.tiles)
            {
                if (tile.category == Category.Ground)
                {
                    ground = tile;
                    continue;
                }

                tile.symbol = Symbols[nextSymbol++];
                legend.Add(new JsonLegend
                {
                    symbol = tile.symbol.ToString(),
                    tile = tile.name,
                    layer = tile.category == Category.Road ? "Ground" : "Decoration",
                    collision = tile.category == Category.Collision
                });

                if (tile.category == Category.Road)
                    roads.Add(tile);
                else
                    props.Add(tile);
            }

            char[,] grid = new char[map.height, map.width];
            for (int y = 0; y < map.height; y++)
                for (int x = 0; x < map.width; x++)
                    grid[y, x] = '.';

            // Connected road backbone, extended by branches toward target coverage.
            if (roads.Count > 0)
            {
                int cx = map.width / 2 + random.Next(-2, 3);
                int cy = map.height / 2 + random.Next(-2, 3);
                cx = Mathf.Clamp(cx, 2, map.width - 3);
                cy = Mathf.Clamp(cy, 2, map.height - 3);

                for (int x = 1; x < map.width - 1; x++)
                    grid[cy, x] = WeightedSymbol(roads, random);
                for (int y = 1; y < map.height - 1; y++)
                    grid[y, cx] = WeightedSymbol(roads, random);

                float roadPercent = 0f;
                foreach (TileSpec road in roads) roadPercent += road.percent;
                int goal = Mathf.RoundToInt(map.width * map.height *
                    roadPercent * 0.01f);
                int attempts = 0;

                while (CountRoads(grid, roads) < goal && attempts++ < 120)
                {
                    bool fromHorizontal = random.Next(2) == 0;
                    int startX = fromHorizontal
                        ? random.Next(2, map.width - 2) : cx;
                    int startY = fromHorizontal
                        ? cy : random.Next(2, map.height - 2);
                    int endX = random.Next(2, map.width - 2);
                    int endY = random.Next(2, map.height - 2);

                    // L-shaped branches are connected to the cross at their start.
                    DrawRoad(grid, startX, startY, endX, startY, roads, random);
                    DrawRoad(grid, endX, startY, endX, endY, roads, random);
                }
            }

            // One item per cell. Rates are percentages of eligible free ground cells.
            for (int y = 1; y < map.height - 1; y++)
            {
                for (int x = 1; x < map.width - 1; x++)
                {
                    if (grid[y, x] != '.') continue;

                    double roll = random.NextDouble() * 100.0;
                    foreach (TileSpec tile in props)
                    {
                        roll -= tile.percent;
                        if (roll >= 0.0) continue;

                        grid[y, x] = tile.symbol;
                        break;
                    }
                }
            }

            // Buildings and player spawn are always kept free of collision.
            // Row zero is the top of the map in the existing JSON format.
            int middleX = map.width / 2;
            int middleY = map.height / 2;
            grid[map.height - 3, middleX] = 'P';
            grid[2, middleX] = 'D';
            grid[middleY, Mathf.Max(2, middleX - 3)] = 'A';

            string[] lines = new string[map.height];
            for (int y = 0; y < map.height; y++)
            {
                var line = new char[map.width];
                for (int x = 0; x < map.width; x++)
                    line[x] = grid[y, x];
                lines[y] = new string(line);
            }

            return new JsonLayout
            {
                mapName = map.name,
                width = map.width,
                height = map.height,
                defaultGroundTile = ground.name,
                blockOuterBorder = true,
                legend = legend.ToArray(),
                rows = lines
            };
        }

        private static int CountRoads(char[,] grid, List<TileSpec> roads)
        {
            var roadSymbols = new HashSet<char>();
            foreach (TileSpec t in roads) roadSymbols.Add(t.symbol);

            int count = 0;
            for (int y = 1; y < grid.GetLength(0) - 1; y++)
                for (int x = 1; x < grid.GetLength(1) - 1; x++)
                    if (roadSymbols.Contains(grid[y, x])) count++;
            return count;
        }

        private static void DrawRoad(char[,] grid, int x0, int y0,
            int x1, int y1, List<TileSpec> roads, System.Random random)
        {
            int dx = Math.Sign(x1 - x0), dy = Math.Sign(y1 - y0);
            int x = x0, y = y0;
            while (true)
            {
                if (x > 0 && y > 0 && x < grid.GetLength(1) - 1 &&
                    y < grid.GetLength(0) - 1)
                    grid[y, x] = WeightedSymbol(roads, random);

                if (x == x1 && y == y1) break;
                if (x != x1) x += dx;
                else if (y != y1) y += dy;
            }
        }

        private static char WeightedSymbol(List<TileSpec> tiles, System.Random random)
        {
            double total = 0;
            foreach (TileSpec tile in tiles) total += tile.percent;
            if (total <= 0)
                return tiles[random.Next(tiles.Count)].symbol;

            double remaining = random.NextDouble() * total;
            foreach (TileSpec tile in tiles)
            {
                remaining -= tile.percent;
                if (remaining < 0) return tile.symbol;
            }

            return tiles[tiles.Count - 1].symbol;
        }

        private static Sprite FindOrImportSprite(string name)
        {
            string[] extensions = { ".png", ".jpg", ".jpeg" };
            string path = null;

            foreach (string ext in extensions)
            {
                string candidate = CsvSpriteFolder + "/" + name + ext;
                if (AssetDatabase.LoadMainAssetAtPath(candidate) != null)
                {
                    path = candidate;
                    break;
                }
            }

            if (path == null)
                return null;

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null &&
                (importer.textureType != TextureImporterType.Sprite ||
                 importer.spriteImportMode != SpriteImportMode.Single ||
                 Mathf.Abs(importer.spritePixelsPerUnit - 32f) > 0.001f))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 32f;
                importer.filterMode = FilterMode.Point;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void LinkExistingMapSo(string jsonName, TextAsset json)
        {
            if (json == null) return;
            string[] guids = AssetDatabase.FindAssets("t:MapSO");

            foreach (string guid in guids)
            {
                MapSO map = AssetDatabase.LoadAssetAtPath<MapSO>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (map == null || !string.Equals(
                    map.layoutJsonName, jsonName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (map.layoutJson == json) continue;
                Undo.RecordObject(map, "Link map layout JSON");
                map.layoutJson = json;
                EditorUtility.SetDirty(map);
            }
        }

        private static bool TryCategory(string value, out Category category)
        {
            switch (value.Trim().ToLowerInvariant())
            {
                case "地面": case "ground":
                    category = Category.Ground; return true;
                case "道": case "road":
                    category = Category.Road; return true;
                case "当たり判定なしobject":
                case "当たり判定なしオブジェクト":
                case "装飾": case "decoration":
                    category = Category.Decoration; return true;
                case "コリジョン": case "collision":
                    category = Category.Collision; return true;
                default:
                    category = Category.Ground; return false;
            }
        }

        private static bool IsHeaderValid(List<string> row)
        {
            string[] expected =
            {
                "地形生成用JSON名", "幅", "高さ", "Seed", "種類", "素材名", "配置率"
            };
            if (row.Count != expected.Length) return false;
            for (int i = 0; i < row.Count; i++)
                if (!string.Equals(Cell(row, i), expected[i],
                    StringComparison.OrdinalIgnoreCase))
                    return false;
            return true;
        }

        private static string ReadText(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            try
            {
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.GetEncoding(932).GetString(bytes);
            }
        }

        private static List<List<string>> ParseCsv(string text)
        {
            var result = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool quote = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"')
                {
                    if (quote && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"'); i++;
                    }
                    else quote = !quote;
                    continue;
                }

                if (!quote && c == ',')
                {
                    row.Add(cell.ToString());
                    cell.Length = 0;
                    continue;
                }

                if (!quote && (c == '\n' || c == '\r'))
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        i++;
                    row.Add(cell.ToString());
                    cell.Length = 0;
                    result.Add(row);
                    row = new List<string>();
                    continue;
                }

                cell.Append(c);
            }

            if (quote) throw new FormatException("CSV内の引用符が閉じられていません。");
            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString());
                result.Add(row);
            }
            return result;
        }

        private static string Cell(List<string> row, int index)
        {
            return index < row.Count
                ? (row[index] ?? "").Trim().TrimStart('\uFEFF')
                : "";
        }

        private static bool IsBlank(List<string> row)
        {
            foreach (string value in row)
                if (!string.IsNullOrWhiteSpace(value)) return false;
            return true;
        }

        private static string Sanitize(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        private static void EnsureFolder(string folder)
        {
            string current = "Assets";
            string[] parts = folder.Split('/');
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
