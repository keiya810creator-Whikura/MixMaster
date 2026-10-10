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
            public int cellsX = 1;
            public int cellsY = 1;
            public char[] componentSymbols = Array.Empty<char>();
        }

        private sealed class RoadFamily
        {
            public string name;
            public TileSpec vertical;
            public TileSpec horizontal;
            public TileSpec cross;
            public float percent;

            public char SymbolFor(int neighbors, bool up, bool down, bool left, bool right)
            {
                // Only three tile variants are used: vertical, horizontal, cross.
                // A corner, T-junction, crossroad, or isolated road cell uses cross.
                if (up && down && !left && !right)
                    return vertical.symbol;
                if (left && right && !up && !down)
                    return horizontal.symbol;
                if (neighbors == 1)
                {
                    if (up || down) return vertical.symbol;
                    if (left || right) return horizontal.symbol;
                }

                return cross.symbol;
            }
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
                    "ヘッダーは次の8列を使用してください:\n" +
                    "地形生成用JSON名,幅,高さ,Seed,マス,種類,素材名,配置率", "OK");
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
                if (row.Count != 8)
                {
                    Debug.LogError("[MapTerrainCsvGenerator] " + number +
                        "行目: 8列で記述してください。");
                    warnings++;
                    continue;
                }

                string name = Cell(row, 0);
                string tileName = Cell(row, 6);

                if (string.IsNullOrWhiteSpace(name) ||
                    string.IsNullOrWhiteSpace(tileName))
                {
                    Debug.LogError("[MapTerrainCsvGenerator] " + number +
                        "行目: JSON名または素材名が空です。");
                    warnings++;
                    continue;
                }

                Category category;
                if (!TryCategory(Cell(row, 5), out category))
                {
                    Debug.LogError("[MapTerrainCsvGenerator] " + number +
                        "行目: 不明な種類: " + Cell(row, 5));
                    warnings++;
                    continue;
                }

                int width, height, seed, cellsX, cellsY;
                float percent;

                if (!TryParseFootprint(Cell(row, 4), out cellsX, out cellsY) ||
                    cellsX > 8 || cellsY > 8 ||
                    ((category == Category.Ground || category == Category.Road) &&
                     (cellsX != 1 || cellsY != 1)))
                {
                    Debug.LogError("[MapTerrainCsvGenerator] " + number +
                        "行目: マスは 1*1、2*2、3*2 など（最大8*8）。" +
                        "地面・道は1*1のみ対応しています。");
                    warnings++;
                    if (maps.TryGetValue(name, out MapSpec invalidFootprint))
                        invalidFootprint.invalid = true;
                    continue;
                }

                if (!int.TryParse(Cell(row, 1), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out width) ||
                    !int.TryParse(Cell(row, 2), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out height) ||
                    !int.TryParse(Cell(row, 3), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out seed) ||
                    !float.TryParse(Cell(row, 7), NumberStyles.Float,
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
                    category = category, name = tileName, percent = percent,
                    cellsX = cellsX, cellsY = cellsY
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
            int groundCount = 0, symbolsRequired = 0;
            float totalRoadPercent = 0f, propPercent = 0f;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<RoadFamily> roads = BuildRoadFamilies(map);

            foreach (RoadFamily family in roads)
            {
                totalRoadPercent += family.percent;
                if (family.vertical == null || family.horizontal == null ||
                    family.cross == null)
                {
                    Debug.LogError("[MapTerrainCsvGenerator] " + map.name +
                        ": 道の素材は '" + family.name +
                        "_縦', '_横', '_十字' の3種類を登録してください。" +
                        " 全面タイルは不要です。");
                    return false;
                }
            }

            foreach (TileSpec tile in map.tiles)
            {
                if (!names.Add(tile.name))
                {
                    Debug.LogError("[MapTerrainCsvGenerator] 素材名が重複しています: " +
                        tile.name);
                    return false;
                }

                if (tile.category == Category.Road &&
                    !IsRoadVariant(tile.name))
                {
                    Debug.LogError("[MapTerrainCsvGenerator] " + map.name +
                        ": 道は '_縦', '_横', '_十字' のみ対応しています: " +
                        tile.name);
                    return false;
                }

                if (tile.category == Category.Ground)
                {
                    groundCount++;
                    if (tile.cellsX != 1 || tile.cellsY != 1)
                        return false;
                }
                else
                {
                    symbolsRequired += tile.cellsX * tile.cellsY;
                    if (tile.category == Category.Decoration ||
                        tile.category == Category.Collision)
                        propPercent += tile.percent;
                }

                // Multi-cell objects use _1, _2, ... in row-major order.
                int partCount = tile.cellsX * tile.cellsY;
                for (int part = 1; part <= partCount; part++)
                {
                    string spriteName = partCount > 1
                        ? tile.name + "_" + part : tile.name;
                    if (FindOrImportSprite(spriteName) != null)
                        continue;

                    Debug.LogError("[MapTerrainCsvGenerator] " + map.name +
                        ": PNGが見つかりません: " + CsvSpriteFolder + "/" +
                        spriteName + ".png");
                    return false;
                }
            }

            if (groundCount != 1 || symbolsRequired > Symbols.Length ||
                totalRoadPercent > 40f || propPercent > 100f)
            {
                Debug.LogError("[MapTerrainCsvGenerator] " + map.name +
                    ": 地面は1種類、必要な記号数は " + Symbols.Length +
                    " 以下、道の配置率合計40%以下、装飾・障害物の配置率合計100%以下にしてください。" +
                    " 実際の記号数=" + symbolsRequired);
                return false;
            }

            return true;
        }

        // '.', 'A', 'D', 'P' are reserved by the existing JSON Tilemap importer.
        private const string SymbolAlphabet =
            "abcdefghijklmnoqrstuvwxyz0123456789BCEFGHIJKLMNOQRSTUVWXYZ";
        private static readonly char[] Symbols = SymbolAlphabet.ToCharArray();

        private static bool IsRoadVariant(string name)
        {
            return name.EndsWith("_十字", StringComparison.Ordinal) ||
                   name.EndsWith("_縦", StringComparison.Ordinal) ||
                   name.EndsWith("_横", StringComparison.Ordinal);
        }

        private static string RoadFamilyName(string name)
        {
            string[] suffixes = { "_十字", "_縦", "_横" };
            foreach (string suffix in suffixes)
                if (name.EndsWith(suffix, StringComparison.Ordinal))
                    return name.Substring(0, name.Length - suffix.Length);
            return name;
        }

        private static List<RoadFamily> BuildRoadFamilies(MapSpec map)
        {
            var familyMap = new Dictionary<string, RoadFamily>(
                StringComparer.OrdinalIgnoreCase);
            var ordered = new List<RoadFamily>();

            foreach (TileSpec tile in map.tiles)
            {
                if (tile.category != Category.Road)
                    continue;

                string baseName = RoadFamilyName(tile.name);
                if (!familyMap.TryGetValue(baseName, out RoadFamily family))
                {
                    family = new RoadFamily { name = baseName, percent = tile.percent };
                    familyMap.Add(baseName, family);
                    ordered.Add(family);
                }

                if (tile.name.EndsWith("_十字", StringComparison.Ordinal))
                    family.cross = tile;
                else if (tile.name.EndsWith("_縦", StringComparison.Ordinal))
                    family.vertical = tile;
                else if (tile.name.EndsWith("_横", StringComparison.Ordinal))
                    family.horizontal = tile;
            }

            return ordered;
        }

        private static JsonLayout BuildLayout(MapSpec map)
        {
            System.Random random = new System.Random(map.seed);
            TileSpec ground = null;
            var props = new List<TileSpec>();
            var legend = new List<JsonLegend>();
            List<RoadFamily> roads = BuildRoadFamilies(map);
            int nextSymbol = 0;

            foreach (TileSpec tile in map.tiles)
            {
                if (tile.category == Category.Ground)
                {
                    ground = tile;
                    continue;
                }

                int count = tile.cellsX * tile.cellsY;
                tile.componentSymbols = new char[count];
                for (int i = 0; i < count; i++)
                {
                    char symbol = Symbols[nextSymbol++];
                    tile.componentSymbols[i] = symbol;
                    if (i == 0) tile.symbol = symbol;

                    legend.Add(new JsonLegend
                    {
                        symbol = symbol.ToString(),
                        tile = count == 1 ? tile.name : tile.name + "_" + (i + 1),
                        layer = tile.category == Category.Road
                            ? "Ground" : "Decoration",
                        collision = tile.category == Category.Collision
                    });
                }

                if (tile.category == Category.Decoration ||
                    tile.category == Category.Collision)
                    props.Add(tile);
            }

            // Existing JSON rows are top to bottom. A single char maps to
            // exactly one tile in the TilemapLayoutGenerator.
            char[,] grid = new char[map.height, map.width];
            for (int y = 0; y < map.height; y++)
                for (int x = 0; x < map.width; x++)
                    grid[y, x] = '.';

            int middleX = map.width / 2;
            int middleY = map.height / 2;
            grid[map.height - 3, middleX] = 'P';
            grid[2, middleX] = 'D';
            grid[middleY, Mathf.Max(2, middleX - 3)] = 'A';

            if (roads.Count > 0)
                DrawConnectedRoads(grid, roads, random);

            // Place large objects first: all footprint cells must be empty.
            // Rates are the fraction of the total map AREA (not object count).
            props.Sort((a, b) =>
                (b.cellsX * b.cellsY).CompareTo(a.cellsX * a.cellsY));
            foreach (TileSpec tile in props)
                PlaceObjects(grid, tile, map.width, map.height, random);

            string[] lines = new string[map.height];
            for (int y = 0; y < map.height; y++)
            {
                char[] line = new char[map.width];
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

        private static void PlaceObjects(char[,] grid, TileSpec tile,
            int width, int height, System.Random random)
        {
            if (tile.percent <= 0f)
                return;

            int footprint = tile.cellsX * tile.cellsY;
            int desired = Mathf.RoundToInt(
                width * height * tile.percent / (100f * footprint));
            int placed = 0;
            if (desired <= 0)
                return;

            // Exclude outer one-cell border. Shuffle all legal anchors once,
            // so placement is deterministic, efficient and non-overlapping.
            var anchors = new List<int>();
            for (int y = 1; y <= height - tile.cellsY - 1; y++)
                for (int x = 1; x <= width - tile.cellsX - 1; x++)
                    anchors.Add(y * width + x);

            for (int i = anchors.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int tmp = anchors[i];
                anchors[i] = anchors[j];
                anchors[j] = tmp;
            }

            foreach (int anchor in anchors)
            {
                if (placed >= desired) break;
                int x = anchor % width;
                int y = anchor / width;
                if (!IsFootprintClear(grid, x, y, tile.cellsX, tile.cellsY))
                    continue;

                // _1 _2 ... are assigned from top-left, left-to-right,
                // then row-by-row toward the bottom.
                for (int dy = 0; dy < tile.cellsY; dy++)
                    for (int dx = 0; dx < tile.cellsX; dx++)
                        grid[y + dy, x + dx] =
                            tile.componentSymbols[dy * tile.cellsX + dx];

                placed++;
            }

            if (placed < desired)
                Debug.LogWarning("[MapTerrainCsvGenerator] " + tile.name +
                    ": 配置可能な空きマスが足りません (希望 " + desired +
                    " 個 / 実際 " + placed + " 個)。");
        }

        private static bool IsFootprintClear(
            char[,] grid, int x, int y, int columns, int rows)
        {
            for (int dy = 0; dy < rows; dy++)
            {
                for (int dx = 0; dx < columns; dx++)
                {
                    int cellX = x + dx;
                    int cellY = y + dy;
                    if (grid[cellY, cellX] != '.')
                        return false;

                    // Leave at least a one-cell clearance around the player,
                    // dungeon entrance and altar so blocking objects cannot
                    // seal their access.
                    for (int ny = Math.Max(0, cellY - 1);
                        ny <= Math.Min(grid.GetLength(0) - 1, cellY + 1); ny++)
                    {
                        for (int nx = Math.Max(0, cellX - 1);
                            nx <= Math.Min(grid.GetLength(1) - 1, cellX + 1); nx++)
                        {
                            char nearby = grid[ny, nx];
                            if (nearby == 'P' || nearby == 'D' || nearby == 'A')
                                return false;
                        }
                    }
                }
            }
            return true;
        }

        private static void DrawConnectedRoads(
            char[,] grid, List<RoadFamily> roadFamilies, System.Random random)
        {
            int height = grid.GetLength(0);
            int width = grid.GetLength(1);
            var membership = new RoadFamily[height, width];
            int centerX = Mathf.Clamp(width / 2 + random.Next(-2, 3), 2, width - 3);
            int centerY = Mathf.Clamp(height / 2 + random.Next(-2, 3), 2, height - 3);
            float roadPercent = 0f;
            foreach (RoadFamily family in roadFamilies)
                roadPercent += family.percent;
            if (roadPercent <= 0f)
                return;

            RoadFamily backbone = PickRoadFamily(roadFamilies, random);

            // Continuous central horizontal + vertical backbone.
            for (int x = 1; x < width - 1; x++)
                MarkRoad(grid, membership, x, centerY, backbone);
            for (int y = 1; y < height - 1; y++)
                MarkRoad(grid, membership, centerX, y, backbone);

            int goal = Mathf.RoundToInt(width * height * roadPercent * 0.01f);
            int attempts = 0;
            int count = CountRoadCells(membership);
            int maxAttempts = Mathf.Max(150, goal * 4);

            while (count < goal && attempts++ < maxAttempts)
            {
                bool fromHorizontal = random.Next(2) == 0;
                int startX = fromHorizontal
                    ? random.Next(2, width - 2) : centerX;
                int startY = fromHorizontal
                    ? centerY : random.Next(2, height - 2);
                int endX = random.Next(2, width - 2);
                int endY = random.Next(2, height - 2);

                RoadFamily family = PickRoadFamily(roadFamilies, random);
                count += DrawRoadSegment(grid, membership,
                    startX, startY, endX, startY, family);
                count += DrawRoadSegment(grid, membership,
                    endX, startY, endX, endY, family);
            }

            // Resolve road shape only AFTER the full network is complete.
            // This allows crossroads and straight sections to join naturally.
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    RoadFamily family = membership[y, x];
                    if (family == null) continue;

                    bool up = membership[y - 1, x] != null;
                    bool down = membership[y + 1, x] != null;
                    bool left = membership[y, x - 1] != null;
                    bool right = membership[y, x + 1] != null;
                    int neighbors = (up ? 1 : 0) + (down ? 1 : 0) +
                        (left ? 1 : 0) + (right ? 1 : 0);

                    grid[y, x] = family.SymbolFor(
                        neighbors, up, down, left, right);
                }
            }
        }

        private static int DrawRoadSegment(
            char[,] grid, RoadFamily[,] membership,
            int startX, int startY, int endX, int endY, RoadFamily family)
        {
            int added = 0;
            int x = startX, y = startY;
            int dx = Math.Sign(endX - startX);
            int dy = Math.Sign(endY - startY);
            while (true)
            {
                if (MarkRoad(grid, membership, x, y, family))
                    added++;
                if (x == endX && y == endY)
                    break;
                if (x != endX) x += dx;
                else if (y != endY) y += dy;
            }
            return added;
        }

        private static bool MarkRoad(
            char[,] grid, RoadFamily[,] membership,
            int x, int y, RoadFamily family)
        {
            if (x <= 0 || y <= 0 ||
                x >= grid.GetLength(1) - 1 ||
                y >= grid.GetLength(0) - 1 ||
                grid[y, x] != '.' || membership[y, x] != null)
                return false;

            membership[y, x] = family;
            return true;
        }

        private static int CountRoadCells(RoadFamily[,] membership)
        {
            int total = 0;
            foreach (RoadFamily family in membership)
                if (family != null) total++;
            return total;
        }

        private static RoadFamily PickRoadFamily(
            List<RoadFamily> families, System.Random random)
        {
            double total = 0;
            foreach (RoadFamily family in families)
                total += Math.Max(0f, family.percent);

            if (total <= 0)
                return families[random.Next(families.Count)];

            double roll = random.NextDouble() * total;
            foreach (RoadFamily family in families)
            {
                roll -= Math.Max(0f, family.percent);
                if (roll < 0)
                    return family;
            }
            return families[families.Count - 1];
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

        private static bool TryParseFootprint(string cell, out int columns, out int rows)
        {
            columns = 0;
            rows = 0;
            string[] values = cell.ToLowerInvariant().Replace("×", "*")
                .Replace("x", "*").Split('*');

            return values.Length == 2 &&
                int.TryParse(values[0].Trim(), out columns) &&
                int.TryParse(values[1].Trim(), out rows) &&
                columns >= 1 && rows >= 1;
        }

        private static bool IsHeaderValid(List<string> row)
        {
            string[] expected =
            {
                "地形生成用JSON名", "幅", "高さ", "Seed", "マス", "種類", "素材名", "配置率"
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
