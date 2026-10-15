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
            // Optional 12-column terrain CSV exits; false values are explicit.
            public bool hasExitSettings;
            public bool exitUp;
            public bool exitDown;
            public bool exitLeft;
            public bool exitRight;
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
            public bool hasExitSettings;
            public bool exitUp;
            public bool exitDown;
            public bool exitLeft;
            public bool exitRight;
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
                    "ヘッダーは既存8列か、上下左右の出入口を加えた12列です。\n" +
                    "地形生成用JSON名,幅,高さ,Seed,マス,種類,素材名,配置率," +
                    "上出入口,下出入口,左出入口,右出入口", "OK");
                return;
            }

            bool hasExitColumns = rows[0].Count == 12;
            int expectedColumns = rows[0].Count;
            var maps = new Dictionary<string, MapSpec>(StringComparer.OrdinalIgnoreCase);
            var order = new List<MapSpec>();
            int warnings = 0;

            for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
            {
                List<string> row = rows[rowIndex];
                if (IsBlank(row))
                    continue;

                int number = rowIndex + 1;
                if (row.Count != expectedColumns)
                {
                    Debug.LogError("[MapTerrainCsvGenerator] " + number +
                        "行目: " + expectedColumns + "列で記述してください。");
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
                float percent = 0f;
                bool up = false, down = false, left = false, right = false;
                if (hasExitColumns &&
                    (!TryParseExit(Cell(row, 8), out up) ||
                     !TryParseExit(Cell(row, 9), out down) ||
                     !TryParseExit(Cell(row, 10), out left) ||
                     !TryParseExit(Cell(row, 11), out right)))
                {
                    Debug.LogError("[MapTerrainCsvGenerator] " + number +
                        "行目: 上下左右の出入口は TRUE または FALSE で指定してください。");
                    warnings++;
                    if (maps.TryGetValue(name, out MapSpec invalidExits))
                        invalidExits.invalid = true;
                    continue;
                }

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
                    width < 13 || height < 13 || width > 249 || height > 249 ||
                    width % 2 == 0 || height % 2 == 0 ||
                    ((!hasExitColumns || category != Category.Road) &&
                     (!float.TryParse(Cell(row, 7), NumberStyles.Float,
                         CultureInfo.InvariantCulture, out percent) ||
                      percent < 0f || percent > 100f)))
                {
                    Debug.LogError("[MapTerrainCsvGenerator] " + number +
                        "行目: 幅/高さは13～249の奇数、Seedは整数、" +
                        "地面・オブジェクトの配置率は0～100を指定してください.");
                    warnings++;
                    if (maps.TryGetValue(name, out MapSpec invalidMap))
                        invalidMap.invalid = true;
                    continue;
                }

                if (hasExitColumns && category == Category.Road)
                    percent = 0f; // Road coverage is derived from TRUE exits only.

                if (!maps.TryGetValue(name, out MapSpec spec))
                {
                    spec = new MapSpec
                    {
                        name = name, width = width, height = height, seed = seed,
                        hasExitSettings = hasExitColumns,
                        exitUp = up, exitDown = down, exitLeft = left, exitRight = right
                    };
                    maps.Add(name, spec);
                    order.Add(spec);
                }
                else if (spec.width != width || spec.height != height || spec.seed != seed ||
                    spec.hasExitSettings != hasExitColumns ||
                    (hasExitColumns && (spec.exitUp != up || spec.exitDown != down ||
                        spec.exitLeft != left || spec.exitRight != right)))
                {
                    Debug.LogError("[MapTerrainCsvGenerator] " + name +
                        ": 同じJSON名の幅・高さ・Seed・上下左右出入口が一致していません (CSV " +
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

                JsonLayout layout;
                try
                {
                    layout = BuildLayout(map);
                }
                catch (InvalidOperationException ex)
                {
                    Debug.LogError("[MapTerrainCsvGenerator] " + map.name +
                        ": " + ex.Message);
                    skipped++;
                    warnings++;
                    continue;
                }

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
            float propPercent = 0f;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<RoadFamily> roads = BuildRoadFamilies(map);

            if (map.hasExitSettings && roads.Count == 0)
            {
                Debug.LogError("[MapTerrainCsvGenerator] " + map.name +
                    ": 出入口が指定されていますが道素材がありません。_縦/_横/_十字を登録してください。");
                return false;
            }

            foreach (RoadFamily family in roads)
            {
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
                propPercent > 100f)
            {
                Debug.LogError("[MapTerrainCsvGenerator] " + map.name +
                    ": 地面は1種類、必要な記号数は " + Symbols.Length +
                    " 以下、装飾・障害物の配置率合計100%以下にしてください。" +
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
                    family = new RoadFamily { name = baseName };
                    familyMap.Add(baseName, family);
                    ordered.Add(family);
                }

                // Each CSV row contributes to the desired total coverage.
                // E.g. cross=3%, vertical=2%, horizontal=2% -> 7% roads.
                family.percent += tile.percent;

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

            // Field maps keep the player marker next to, not on, the
            // central crossing. Legacy maps retain the south spawn.
            if (map.hasExitSettings)
                grid[middleY + 1, middleX - 1] = 'P';
            else
                grid[map.height - 3, middleX] = 'P';

            if (roads.Count > 0)
            {
                if (map.hasExitSettings)
                    DrawExitConnectedRoads(grid, roads, random, map);
                else
                    DrawConnectedRoads(grid, roads, random);
            }

            // Place structures only after the final road network is decided.
            // A full 3-cell buffer is required between each building and road.
            var roadSymbols = new HashSet<char>();
            foreach (TileSpec tile in map.tiles)
                if (tile.category == Category.Road)
                    roadSymbols.Add(tile.symbol);

            if (!TryPlaceBuilding(grid, 'D', middleX, 2, roadSymbols))
                throw new InvalidOperationException(
                    "ダンジョン入口を道路から3マス離して配置できません。" +
                    " マップを広くするか配置条件を調整してください。");

            if (!TryPlaceBuilding(grid, 'A',
                Mathf.Max(2, middleX - 3), middleY, roadSymbols))
                throw new InvalidOperationException(
                    "祭壇を道路から3マス離して配置できません。" +
                    " マップを広くするか配置条件を調整してください。");

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
                hasExitSettings = map.hasExitSettings,
                exitUp = map.exitUp,
                exitDown = map.exitDown,
                exitLeft = map.exitLeft,
                exitRight = map.exitRight,
                legend = legend.ToArray(),
                rows = lines
            };
        }

        // A minimum of three empty cells must separate each building
        // from any road, including diagonally (Chebyshev distance >= 4).
        private const int BuildingRoadClearance = 3;
        private const int BuildingAccessClearance = 2;

        private static bool TryPlaceBuilding(
            char[,] grid,
            char symbol,
            int preferredX,
            int preferredY,
            HashSet<char> roadSymbols)
        {
            int height = grid.GetLength(0);
            int width = grid.GetLength(1);
            int bestX = -1;
            int bestY = -1;
            int bestDistance = int.MaxValue;

            // Avoid the outer collision border when selecting a site.
            for (int y = 2; y < height - 2; y++)
            {
                for (int x = 2; x < width - 2; x++)
                {
                    if (grid[y, x] != '.' ||
                        !IsBuildingLocationClear(grid, x, y, roadSymbols))
                        continue;

                    int dx = x - preferredX;
                    int dy = y - preferredY;
                    int distance = dx * dx + dy * dy;
                    if (distance >= bestDistance)
                        continue;

                    bestDistance = distance;
                    bestX = x;
                    bestY = y;
                }
            }

            if (bestX < 0)
                return false;

            grid[bestY, bestX] = symbol;
            return true;
        }

        private static bool IsBuildingLocationClear(
            char[,] grid,
            int x,
            int y,
            HashSet<char> roadSymbols)
        {
            int height = grid.GetLength(0);
            int width = grid.GetLength(1);
            for (int dy = -BuildingRoadClearance;
                dy <= BuildingRoadClearance; dy++)
            {
                int checkY = y + dy;
                if (checkY < 0 || checkY >= height)
                    continue;

                for (int dx = -BuildingRoadClearance;
                    dx <= BuildingRoadClearance; dx++)
                {
                    int checkX = x + dx;
                    if (checkX < 0 || checkX >= width)
                        continue;

                    char value = grid[checkY, checkX];
                    if (roadSymbols.Contains(value))
                        return false;

                    // Also keep access to player, altar and dungeon clear.
                    if (Mathf.Abs(dx) <= BuildingAccessClearance &&
                        Mathf.Abs(dy) <= BuildingAccessClearance &&
                        (value == 'P' || value == 'A' || value == 'D'))
                        return false;
                }
            }

            return true;
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

        // New field maps: exactly one-cell-wide roads, with a permanent
        // central cross and a single straight branch for each TRUE exit.
        // The road percentage and Seed do not alter its route.
        private static void DrawExitConnectedRoads(
            char[,] grid, List<RoadFamily> roadFamilies,
            System.Random random, MapSpec map)
        {
            int height = grid.GetLength(0);
            int width = grid.GetLength(1);
            int midX = width / 2;
            int midY = height / 2;
            RoadFamily road = roadFamilies[0];

            grid[midY, midX] = road.cross.symbol;
            if (map.exitUp)
                for (int y = 0; y < midY; y++)
                    grid[y, midX] = road.vertical.symbol;
            if (map.exitDown)
                for (int y = midY + 1; y < height; y++)
                    grid[y, midX] = road.vertical.symbol;
            if (map.exitLeft)
                for (int x = 0; x < midX; x++)
                    grid[midY, x] = road.horizontal.symbol;
            if (map.exitRight)
                for (int x = midX + 1; x < width; x++)
                    grid[midY, x] = road.horizontal.symbol;
        }

        // Directions are ordered clockwise: up, right, down, left.
        private static readonly Vector2Int[] RoadDirections =
        {
            new Vector2Int(0, -1),
            new Vector2Int(1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(-1, 0)
        };

        private static void DrawConnectedRoads(
            char[,] grid, List<RoadFamily> roadFamilies, System.Random random)
        {
            int height = grid.GetLength(0);
            int width = grid.GetLength(1);
            var membership = new RoadFamily[height, width];
            var cells = new List<Vector2Int>();

            float roadPercent = 0f;
            foreach (RoadFamily family in roadFamilies)
                roadPercent += family.percent;
            if (roadPercent <= 0f)
                return;

            int goal = Mathf.Max(1, Mathf.RoundToInt(
                width * height * roadPercent * 0.01f));
            RoadFamily mainFamily = PickRoadFamily(roadFamilies, random);

            // The main path starts beside the player spawn near the south
            // edge. Unlike the previous full-map +, it gently meanders
            // north with intermittent horizontal stretches.
            int x = width / 2;
            int y = height - 4;
            AddRoad(grid, membership, cells, x, y, mainFamily);
            int spineGoal = Mathf.Min(goal,
                Mathf.Max(6, Mathf.RoundToInt(goal * 0.48f)));

            while (cells.Count < spineGoal && y > 2)
            {
                int straight = Math.Min(random.Next(4, 9), y - 2);
                for (int i = 0; i < straight && cells.Count < spineGoal; i++)
                {
                    y--;
                    AddRoad(grid, membership, cells, x, y, mainFamily);
                }

                if (cells.Count >= spineGoal || y <= 2)
                    break;

                int shift = random.Next(3,
                    Mathf.Max(4, Mathf.Min(9, width / 5) + 1));
                int direction = random.Next(2) == 0 ? -1 : 1;
                if ((direction < 0 && x - 2 < shift) ||
                    (direction > 0 && width - 3 - x < shift))
                    direction = -direction;

                for (int i = 0; i < shift && cells.Count < spineGoal; i++)
                {
                    int nextX = x + direction;
                    if (!IsRoadInside(width, height, nextX, y))
                        break;

                    x = nextX;
                    AddRoad(grid, membership, cells, x, y, mainFamily);
                }
            }

            // Grow winding side roads from randomly selected existing
            // segments. All new cells touch exactly one existing road
            // neighbor, preventing detached paths, dense 2x2 blocks, and
            // excessive unintended crossings.
            int attempts = 0;
            int maxAttempts = Math.Max(200, goal * 25);
            while (cells.Count < goal && attempts++ < maxAttempts)
            {
                Vector2Int start = cells[random.Next(cells.Count)];
                if (CountRoadNeighbors(membership, start.x, start.y) > 2)
                    continue;

                int first = -1;
                int offset = random.Next(4);
                for (int i = 0; i < 4; i++)
                {
                    int direction = (offset + i) % 4;
                    Vector2Int next = start + RoadDirections[direction];
                    if (!CanExtendRoad(grid, membership, next.x, next.y))
                        continue;
                    first = direction;
                    break;
                }

                if (first < 0)
                    continue;

                RoadFamily family = PickRoadFamily(roadFamilies, random);
                int directionIndex = first;
                Vector2Int tip = start;
                int sameDirection = 0;
                int turnAfter = random.Next(3, 8);
                int length = random.Next(7, 20);

                for (int i = 0; i < length && cells.Count < goal; i++)
                {
                    bool shouldTurn = sameDirection >= turnAfter ||
                        (sameDirection >= 3 && random.NextDouble() < 0.14d);
                    int turn = random.Next(2) == 0 ? 1 : -1;
                    int leftOrRight = (directionIndex + turn + 4) % 4;
                    int otherTurn = (directionIndex - turn + 4) % 4;
                    int[] choices = shouldTurn
                        ? new[] { leftOrRight, otherTurn, directionIndex }
                        : new[] { directionIndex, leftOrRight, otherTurn };

                    int selected = -1;
                    for (int j = 0; j < choices.Length; j++)
                    {
                        Vector2Int next = tip + RoadDirections[choices[j]];
                        if (!CanExtendRoad(grid, membership, next.x, next.y))
                            continue;
                        selected = choices[j];
                        break;
                    }

                    if (selected < 0)
                        break;

                    if (selected != directionIndex)
                    {
                        sameDirection = 0;
                        turnAfter = random.Next(3, 8);
                    }

                    directionIndex = selected;
                    tip += RoadDirections[selected];
                    if (!AddRoad(grid, membership, cells,
                        tip.x, tip.y, family))
                        break;
                    sameDirection++;
                }
            }

            if (cells.Count < goal)
            {
                Debug.LogWarning("[MapTerrainCsvGenerator] 道路の配置率を" +
                    "満たす空きマスが不足しています: " +
                    cells.Count + " / " + goal + " マス。" +
                    " 高密度の場合は道の配置率を下げてください。");
            }

            // Road graphics are selected only after every path has been
            // connected. Existing vertical/horizontal/cross sprites are
            // preserved; corners and T-junctions use the cross fallback.
            for (int row = 1; row < height - 1; row++)
            {
                for (int column = 1; column < width - 1; column++)
                {
                    RoadFamily family = membership[row, column];
                    if (family == null) continue;

                    bool up = membership[row - 1, column] != null;
                    bool down = membership[row + 1, column] != null;
                    bool left = membership[row, column - 1] != null;
                    bool right = membership[row, column + 1] != null;
                    int neighbors = (up ? 1 : 0) + (down ? 1 : 0) +
                        (left ? 1 : 0) + (right ? 1 : 0);

                    grid[row, column] = family.SymbolFor(
                        neighbors, up, down, left, right);
                }
            }
        }

        private static bool IsRoadInside(
            int width, int height, int x, int y)
        {
            // Keep two ground cells between roads and the outer border.
            return x >= 2 && y >= 2 && x < width - 2 && y < height - 2;
        }

        private static bool AddRoad(
            char[,] grid, RoadFamily[,] membership,
            List<Vector2Int> cells, int x, int y, RoadFamily family)
        {
            if (!IsRoadInside(grid.GetLength(1), grid.GetLength(0), x, y) ||
                !MarkRoad(grid, membership, x, y, family))
                return false;

            cells.Add(new Vector2Int(x, y));
            return true;
        }

        private static bool CanExtendRoad(
            char[,] grid, RoadFamily[,] membership, int x, int y)
        {
            return IsRoadInside(grid.GetLength(1), grid.GetLength(0), x, y) &&
                grid[y, x] == '.' && membership[y, x] == null &&
                CountRoadNeighbors(membership, x, y) == 1;
        }

        private static int CountRoadNeighbors(
            RoadFamily[,] membership, int x, int y)
        {
            int neighbors = 0;
            for (int i = 0; i < RoadDirections.Length; i++)
            {
                Vector2Int next = new Vector2Int(x, y) + RoadDirections[i];
                if (next.x < 0 || next.y < 0 ||
                    next.x >= membership.GetLength(1) ||
                    next.y >= membership.GetLength(0))
                    continue;

                if (membership[next.y, next.x] != null)
                    neighbors++;
            }
            return neighbors;
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
                "地形生成用JSON名", "幅", "高さ", "Seed", "マス", "種類", "素材名", "配置率",
                "上出入口", "下出入口", "左出入口", "右出入口"
            };
            if (row.Count != 8 && row.Count != 12) return false;
            for (int i = 0; i < row.Count; i++)
                if (!string.Equals(Cell(row, i), expected[i],
                    StringComparison.OrdinalIgnoreCase))
                    return false;
            return true;
        }

        private static bool TryParseExit(string raw, out bool enabled)
        {
            if (string.Equals(raw, "TRUE", StringComparison.OrdinalIgnoreCase) ||
                raw == "1" || raw == "有" || raw == "あり")
            {
                enabled = true;
                return true;
            }
            if (string.Equals(raw, "FALSE", StringComparison.OrdinalIgnoreCase) ||
                raw == "0" || raw == "無" || raw == "なし")
            {
                enabled = false;
                return true;
            }

            enabled = false;
            return false;
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
