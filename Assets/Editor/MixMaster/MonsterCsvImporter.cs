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
    public static class MonsterCsvImporter
    {
        private const string CsvAssetPath =
            "Assets/DataCSV/モンスター.csv";

        private const string MonsterOutputFolder =
            "Assets/Data/Monsters";

        private const string MaterialOutputFolder =
            "Assets/Data/Materials";

        private const string TitleOutputFolder =
            "Assets/Data/Titles";

        private const string SkillOutputFolder =
            "Assets/Data/Skills";

        private const string MonsterSpriteFolder =
            "Assets/素材/Monster";

        private const string CommonEnemyPrefabPath =
            "Assets/Prefab/Enemy.prefab";

        [MenuItem("MixMaster/CSV/モンスターCSVをインポート・更新")]
        public static void ImportOrUpdate()
        {
            string absoluteCsvPath =
                GetAbsoluteProjectPath(CsvAssetPath);

            if (!File.Exists(absoluteCsvPath))
            {
                Debug.LogError(
                    "[MonsterCsvImporter] CSVが見つかりません: " +
                    CsvAssetPath);

                EditorUtility.DisplayDialog(
                    "モンスターCSVインポート",
                    "CSVが見つかりません。\n\n" +
                    CsvAssetPath,
                    "OK");

                return;
            }

            EnsureFolder(MonsterOutputFolder);
            EnsureFolder(MaterialOutputFolder);
            EnsureFolder(TitleOutputFolder);
            EnsureFolder(SkillOutputFolder);

            string csvText = ReadCsvText(absoluteCsvPath);
            List<List<string>> rows = ParseCsv(csvText);

            if (rows.Count <= 1)
            {
                EditorUtility.DisplayDialog(
                    "モンスターCSVインポート",
                    "CSVにデータ行がありません。",
                    "OK");

                return;
            }

            Dictionary<string, int> header =
                BuildHeaderMap(rows[0]);

            string[] requiredHeaders =
            {
                "ID",
                "名前",
                "属性",
                "攻撃方式",
                "基礎HP",
                "基礎MP",
                "基礎攻撃力",
                "基礎魔力",
                "基礎防御力",
                "基礎魔防",
                "クリ率",
                "クリ倍",
                "移動速度",
                "攻撃速度",
                "攻撃範囲",
                "火耐性",
                "水耐性",
                "風耐性",
                "雷耐性",
                "光耐性",
                "闇耐性",
                "索敵範囲",
                "リス秒",
                "素材ID",
                "素材名",
                "素材ドロ率",
                "経験値",
                "配合用値",
                "魔弾Prefab名",
                "魔弾速度",
                "魔弾寿命秒数",
                "魔弾追尾",
                "称号ID",
                "SkillIds"
            };

            for (int i = 0; i < requiredHeaders.Length; i++)
            {
                if (header.ContainsKey(requiredHeaders[i]))
                    continue;

                Debug.LogError(
                    "[MonsterCsvImporter] 必要な列がありません: " +
                    requiredHeaders[i]);

                EditorUtility.DisplayDialog(
                    "モンスターCSVインポート",
                    "必要な列がありません:\n" +
                    requiredHeaders[i],
                    "OK");

                return;
            }

            Dictionary<string, MonsterSO> monstersById =
                BuildAssetMap<MonsterSO>(
                    asset => asset.monsterId);

            Dictionary<string, MaterialSO> materialsById =
                BuildAssetMap<MaterialSO>(
                    asset => asset.materialId);

            Dictionary<string, TitleSO> titlesById =
                BuildAssetMap<TitleSO>(
                    asset => asset.titleId);

            Dictionary<string, SkillSO> skillsById =
                BuildAssetMap<SkillSO>(
                    asset => asset.skillId);

            GameObject commonEnemyPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    CommonEnemyPrefabPath);

            if (commonEnemyPrefab == null)
            {
                Debug.LogWarning(
                    "[MonsterCsvImporter] 共通Enemy Prefabが見つかりません: " +
                    CommonEnemyPrefabPath);
            }

            int createdCount = 0;
            int updatedCount = 0;
            int skippedCount = 0;
            int warningCount = 0;

            HashSet<string> importedIds =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            try
            {
                for (int rowIndex = 1;
                     rowIndex < rows.Count;
                     rowIndex++)
                {
                    List<string> row = rows[rowIndex];

                    if (IsEmptyRow(row))
                        continue;

                    string monsterId =
                        GetCell(row, header, "ID").Trim();

                    if (string.IsNullOrWhiteSpace(monsterId))
                    {
                        skippedCount++;
                        warningCount++;

                        Debug.LogWarning(
                            "[MonsterCsvImporter] " +
                            (rowIndex + 1) +
                            "行目: IDが空なのでスキップしました。");

                        continue;
                    }

                    if (!importedIds.Add(monsterId))
                    {
                        skippedCount++;
                        warningCount++;

                        Debug.LogWarning(
                            "[MonsterCsvImporter] " +
                            (rowIndex + 1) +
                            "行目: ID '" +
                            monsterId +
                            "' がCSV内で重複しているためスキップしました。");

                        continue;
                    }

                    EditorUtility.DisplayProgressBar(
                        "モンスターCSVインポート",
                        monsterId + " を更新中...",
                        (float)rowIndex /
                        Mathf.Max(1, rows.Count - 1));

                    bool isNew =
                        !monstersById.TryGetValue(
                            monsterId,
                            out MonsterSO monster) ||
                        monster == null;

                    if (isNew)
                    {
                        monster =
                            ScriptableObject.CreateInstance<MonsterSO>();

                        string displayName =
                            GetCell(row, header, "名前").Trim();

                        string fileName =
                            SanitizeFileName(
                                monsterId + "_" + displayName);

                        string assetPath =
                            AssetDatabase.GenerateUniqueAssetPath(
                                MonsterOutputFolder +
                                "/" +
                                fileName +
                                ".asset");

                        AssetDatabase.CreateAsset(
                            monster,
                            assetPath);

                        monstersById[monsterId] =
                            monster;

                        createdCount++;
                    }
                    else
                    {
                        Undo.RecordObject(
                            monster,
                            "Update MonsterSO from CSV");

                        updatedCount++;
                    }

                    try
                    {
                        ApplyRow(
                            monster,
                            row,
                            header,
                            commonEnemyPrefab,
                            materialsById,
                            titlesById,
                            skillsById,
                            ref warningCount);

                        EditorUtility.SetDirty(monster);
                    }
                    catch (Exception ex)
                    {
                        warningCount++;

                        Debug.LogError(
                            "[MonsterCsvImporter] " +
                            (rowIndex + 1) +
                            "行目 / " +
                            monsterId +
                            " の更新に失敗しました。\n" +
                            ex);
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                MonsterCatalogBuilder.RebuildCatalog();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            string resultMessage =
                "完了しました。\n\n" +
                "新規作成: " + createdCount + "\n" +
                "更新: " + updatedCount + "\n" +
                "スキップ: " + skippedCount + "\n" +
                "警告: " + warningCount;

            Debug.Log(
                "[MonsterCsvImporter] " +
                resultMessage.Replace("\n", " / "));

            EditorUtility.DisplayDialog(
                "モンスターCSVインポート",
                resultMessage,
                "OK");
        }

        private static void ApplyRow(
            MonsterSO monster,
            List<string> row,
            Dictionary<string, int> header,
            GameObject commonEnemyPrefab,
            Dictionary<string, MaterialSO> materialsById,
            Dictionary<string, TitleSO> titlesById,
            Dictionary<string, SkillSO> skillsById,
            ref int warningCount)
        {
            monster.monsterId =
                GetCell(row, header, "ID").Trim();

            monster.displayName =
                GetCell(row, header, "名前").Trim();

            if (commonEnemyPrefab != null)
                monster.enemyPrefab = commonEnemyPrefab;

            ApplyMonsterSprite(
                monster,
                ref warningCount);

            monster.baseStats ??=
                new CharacterStats();

            monster.baseStats.resistances ??=
                new ElementResistanceSet();

            monster.baseStats.element =
                ParseElement(
                    GetCell(row, header, "属性"));

            monster.attackType =
                ParseAttackType(
                    GetCell(row, header, "攻撃方式"));

            monster.baseStats.maxHp =
                Math.Max(
                    1L,
                    ReadLong(
                        row,
                        header,
                        "基礎HP",
                        1L));

            monster.baseStats.maxMp =
                Math.Max(
                    0L,
                    ReadLong(
                        row,
                        header,
                        "基礎MP",
                        0L));

            monster.baseStats.attack =
                Math.Max(
                    0L,
                    ReadLong(
                        row,
                        header,
                        "基礎攻撃力",
                        0L));

            monster.baseStats.magic =
                Math.Max(
                    0L,
                    ReadLong(
                        row,
                        header,
                        "基礎魔力",
                        0L));

            monster.baseStats.defense =
                Math.Max(
                    0L,
                    ReadLong(
                        row,
                        header,
                        "基礎防御力",
                        0L));

            monster.baseStats.magicDefense =
                Math.Max(
                    0L,
                    ReadLong(
                        row,
                        header,
                        "基礎魔防",
                        0L));

            monster.baseStats.criticalRate =
                Mathf.Clamp01(
                    ReadFloat(
                        row,
                        header,
                        "クリ率",
                        0f));

            monster.baseStats.criticalMultiplier =
                Mathf.Max(
                    1f,
                    ReadFloat(
                        row,
                        header,
                        "クリ倍",
                        1f));

            monster.baseStats.moveSpeed =
                Mathf.Max(
                    0.1f,
                    ReadFloat(
                        row,
                        header,
                        "移動速度",
                        0.1f));

            monster.baseStats.attackSpeed =
                Mathf.Max(
                    0.1f,
                    ReadFloat(
                        row,
                        header,
                        "攻撃速度",
                        0.1f));

            monster.baseStats.attackRange =
                Mathf.Max(
                    0.1f,
                    ReadFloat(
                        row,
                        header,
                        "攻撃範囲",
                        0.1f));

            monster.baseStats.resistances.fire =
                ReadFloat(
                    row,
                    header,
                    "火耐性",
                    0f);

            monster.baseStats.resistances.water =
                ReadFloat(
                    row,
                    header,
                    "水耐性",
                    0f);

            monster.baseStats.resistances.wind =
                ReadFloat(
                    row,
                    header,
                    "風耐性",
                    0f);

            monster.baseStats.resistances.lightning =
                ReadFloat(
                    row,
                    header,
                    "雷耐性",
                    0f);

            monster.baseStats.resistances.light =
                ReadFloat(
                    row,
                    header,
                    "光耐性",
                    0f);

            monster.baseStats.resistances.dark =
                ReadFloat(
                    row,
                    header,
                    "闇耐性",
                    0f);

            monster.baseStats.resistances.ClampAll();

            monster.detectionRange =
                Mathf.Max(
                    0.1f,
                    ReadFloat(
                        row,
                        header,
                        "索敵範囲",
                        4f));

            monster.baseRespawnInterval =
                Mathf.Max(
                    0f,
                    ReadFloat(
                        row,
                        header,
                        "リス秒",
                        8f));

            string materialId =
                GetCell(
                    row,
                    header,
                    "素材ID").Trim();

            string materialName =
                GetCell(
                    row,
                    header,
                    "素材名").Trim();

            monster.uniqueMaterial =
                GetOrCreateMaterial(
                    materialId,
                    materialName,
                    materialsById);

            monster.materialBaseDropRate =
                Mathf.Clamp01(
                    ReadFloat(
                        row,
                        header,
                        "素材ドロ率",
                        0f));

            monster.experienceReward =
                Math.Max(
                    0L,
                    ReadLong(
                        row,
                        header,
                        "経験値",
                        0L));

            monster.breedingValue =
                ReadInt(
                    row,
                    header,
                    "配合用値",
                    0);

            string projectileName =
                GetCell(
                    row,
                    header,
                    "魔弾Prefab名").Trim();

            ApplyProjectilePrefab(
                monster,
                projectileName,
                ref warningCount);

            monster.projectileSpeed =
                Mathf.Max(
                    0.1f,
                    ReadFloat(
                        row,
                        header,
                        "魔弾速度",
                        0.1f));

            monster.projectileLifetime =
                Mathf.Max(
                    0.1f,
                    ReadFloat(
                        row,
                        header,
                        "魔弾寿命秒数",
                        0.1f));

            monster.projectileHoming =
                ReadBool(
                    row,
                    header,
                    "魔弾追尾",
                    false);

            monster.titlePool =
                ResolveTitles(
                    GetCell(
                        row,
                        header,
                        "称号ID"),
                    titlesById);

            monster.availableSkills =
                ResolveSkills(
                    GetCell(
                        row,
                        header,
                        "SkillIds"),
                    skillsById);
        }

        private static void ApplyMonsterSprite(
            MonsterSO monster,
            ref int warningCount)
        {
            if (!TryGetMonsterNumber(
                    monster.monsterId,
                    out int number))
            {
                warningCount++;

                Debug.LogWarning(
                    "[MonsterCsvImporter] Sprite番号をIDから取得できません: " +
                    monster.monsterId);

                return;
            }

            string spritePath =
                MonsterSpriteFolder +
                "/" +
                number +
                ".png";

            Sprite sprite =
                LoadSpriteAtPath(spritePath);

            if (sprite == null)
            {
                warningCount++;

                Debug.LogWarning(
                    "[MonsterCsvImporter] Monster Spriteが見つかりません: " +
                    spritePath);

                return;
            }

            monster.sprite = sprite;

            // UI用アイコンがまだ未設定なら同じSpriteを初期値にする。
            // 既存アイコンは上書きしない。
            if (monster.icon == null)
                monster.icon = sprite;
        }

        private static Sprite LoadSpriteAtPath(
            string assetPath)
        {
            Sprite direct =
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    assetPath);

            if (direct != null)
                return direct;

            UnityEngine.Object[] assets =
                AssetDatabase.LoadAllAssetsAtPath(
                    assetPath);

            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite)
                    return sprite;
            }

            return null;
        }

        private static bool TryGetMonsterNumber(
            string monsterId,
            out int number)
        {
            number = 0;

            if (string.IsNullOrWhiteSpace(monsterId))
                return false;

            int end = monsterId.Length - 1;

            while (end >= 0 &&
                   char.IsDigit(monsterId[end]))
            {
                end--;
            }

            string digits =
                monsterId.Substring(end + 1);

            return int.TryParse(
                digits,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out number);
        }

        private static void ApplyProjectilePrefab(
            MonsterSO monster,
            string prefabName,
            ref int warningCount)
        {
            if (string.IsNullOrWhiteSpace(prefabName) ||
                prefabName.Equals(
                    "None",
                    StringComparison.OrdinalIgnoreCase) ||
                prefabName == "なし")
            {
                monster.projectilePrefab = null;
                return;
            }

            GameObject prefab =
                FindPrefabByExactName(prefabName);

            if (prefab == null)
            {
                warningCount++;

                Debug.LogWarning(
                    "[MonsterCsvImporter] 魔弾Prefab '" +
                    prefabName +
                    "' が見つかりません。既存設定を維持します。");

                return;
            }

            monster.projectilePrefab = prefab;
        }

        private static GameObject FindPrefabByExactName(
            string prefabName)
        {
            string preferredPath =
                "Assets/Prefab/" +
                prefabName +
                ".prefab";

            GameObject preferred =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    preferredPath);

            if (preferred != null)
                return preferred;

            string[] guids =
                AssetDatabase.FindAssets(
                    prefabName + " t:Prefab",
                    new[] { "Assets" });

            for (int i = 0; i < guids.Length; i++)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(
                        guids[i]);

                if (!string.Equals(
                        Path.GetFileNameWithoutExtension(path),
                        prefabName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        path);

                if (prefab != null)
                    return prefab;
            }

            return null;
        }

        private static MaterialSO GetOrCreateMaterial(
            string materialId,
            string materialName,
            Dictionary<string, MaterialSO> assetsById)
        {
            if (string.IsNullOrWhiteSpace(materialId))
                return null;

            if (assetsById.TryGetValue(
                    materialId,
                    out MaterialSO existing) &&
                existing != null)
            {
                Undo.RecordObject(
                    existing,
                    "Update MaterialSO from Monster CSV");

                existing.materialId =
                    materialId;

                existing.displayName =
                    string.IsNullOrWhiteSpace(materialName)
                        ? materialId
                        : materialName;

                existing.category =
                    MaterialCategory.Monster;

                MaterialSpriteAutoAssigner.TryApply(
                    existing,
                    out _);

                EditorUtility.SetDirty(
                    existing);

                return existing;
            }

            MaterialSO asset =
                ScriptableObject.CreateInstance<MaterialSO>();

            asset.materialId = materialId;
            asset.displayName =
                string.IsNullOrWhiteSpace(materialName)
                    ? materialId
                    : materialName;

            asset.category = MaterialCategory.Monster;

            string path =
                AssetDatabase.GenerateUniqueAssetPath(
                    MaterialOutputFolder +
                    "/" +
                    SanitizeFileName(materialId) +
                    ".asset");

            AssetDatabase.CreateAsset(asset, path);

            MaterialSpriteAutoAssigner.TryApply(
                asset,
                out _);

            EditorUtility.SetDirty(asset);

            assetsById[materialId] = asset;
            return asset;
        }

        private static List<TitleSO> ResolveTitles(
            string cell,
            Dictionary<string, TitleSO> assetsById)
        {
            List<TitleSO> result =
                new List<TitleSO>();

            string[] ids = SplitIds(cell);

            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];

                if (assetsById.TryGetValue(
                        id,
                        out TitleSO existing) &&
                    existing != null)
                {
                    result.Add(existing);
                    continue;
                }

                TitleSO asset =
                    ScriptableObject.CreateInstance<TitleSO>();

                asset.titleId = id;
                asset.displayName = id;

                string path =
                    AssetDatabase.GenerateUniqueAssetPath(
                        TitleOutputFolder +
                        "/" +
                        SanitizeFileName(id) +
                        ".asset");

                AssetDatabase.CreateAsset(asset, path);
                EditorUtility.SetDirty(asset);

                assetsById[id] = asset;
                result.Add(asset);
            }

            return result;
        }

        private static List<SkillSO> ResolveSkills(
            string cell,
            Dictionary<string, SkillSO> assetsById)
        {
            List<SkillSO> result =
                new List<SkillSO>();

            string[] ids = SplitIds(cell);

            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];

                if (assetsById.TryGetValue(
                        id,
                        out SkillSO existing) &&
                    existing != null)
                {
                    result.Add(existing);
                    continue;
                }

                SkillSO asset =
                    ScriptableObject.CreateInstance<SkillSO>();

                asset.skillId = id;
                asset.displayName = id;

                string path =
                    AssetDatabase.GenerateUniqueAssetPath(
                        SkillOutputFolder +
                        "/" +
                        SanitizeFileName(id) +
                        ".asset");

                AssetDatabase.CreateAsset(asset, path);
                EditorUtility.SetDirty(asset);

                assetsById[id] = asset;
                result.Add(asset);
            }

            return result;
        }

        private static string[] SplitIds(string cell)
        {
            if (string.IsNullOrWhiteSpace(cell))
                return Array.Empty<string>();

            string[] raw =
                cell.Split(
                    new[] { ';', '；', '|' },
                    StringSplitOptions.RemoveEmptyEntries);

            List<string> result =
                new List<string>();

            HashSet<string> unique =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < raw.Length; i++)
            {
                string id = raw[i].Trim();

                if (!string.IsNullOrWhiteSpace(id) &&
                    unique.Add(id))
                {
                    result.Add(id);
                }
            }

            return result.ToArray();
        }

        private static ElementType ParseElement(string value)
        {
            string normalized =
                (value ?? string.Empty).Trim();

            switch (normalized)
            {
                case "火":
                case "Fire":
                case "fire":
                    return ElementType.Fire;

                case "水":
                case "Water":
                case "water":
                    return ElementType.Water;

                case "風":
                case "Wind":
                case "wind":
                    return ElementType.Wind;

                case "雷":
                case "Lightning":
                case "lightning":
                case "Thunder":
                case "thunder":
                    return ElementType.Lightning;

                case "光":
                case "Light":
                case "light":
                    return ElementType.Light;

                case "闇":
                case "Dark":
                case "dark":
                    return ElementType.Dark;

                case "":
                case "None":
                case "none":
                case "無":
                    return ElementType.None;
            }

            if (Enum.TryParse(
                    normalized,
                    true,
                    out ElementType parsed))
            {
                return parsed;
            }

            Debug.LogWarning(
                "[MonsterCsvImporter] 不明な属性 '" +
                normalized +
                "'。Noneとして扱います。");

            return ElementType.None;
        }

        private static MonsterAttackType ParseAttackType(
            string value)
        {
            string normalized =
                (value ?? string.Empty).Trim();

            if (normalized == "遠距離" ||
                normalized.Equals(
                    "Ranged",
                    StringComparison.OrdinalIgnoreCase))
            {
                return MonsterAttackType.Ranged;
            }

            if (normalized == "近距離" ||
                normalized.Equals(
                    "Melee",
                    StringComparison.OrdinalIgnoreCase))
            {
                return MonsterAttackType.Melee;
            }

            Debug.LogWarning(
                "[MonsterCsvImporter] 不明な攻撃方式 '" +
                normalized +
                "'。Meleeとして扱います。");

            return MonsterAttackType.Melee;
        }

        private static Dictionary<string, T>
            BuildAssetMap<T>(
                Func<T, string> idSelector)
            where T : ScriptableObject
        {
            Dictionary<string, T> result =
                new Dictionary<string, T>(
                    StringComparer.OrdinalIgnoreCase);

            string[] guids =
                AssetDatabase.FindAssets(
                    "t:" + typeof(T).Name,
                    new[] { "Assets" });

            for (int i = 0; i < guids.Length; i++)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(
                        guids[i]);

                T asset =
                    AssetDatabase.LoadAssetAtPath<T>(
                        path);

                if (asset == null)
                    continue;

                string id =
                    idSelector(asset);

                if (string.IsNullOrWhiteSpace(id))
                    continue;

                if (!result.ContainsKey(id))
                {
                    result.Add(id, asset);
                }
                else
                {
                    Debug.LogWarning(
                        "[MonsterCsvImporter] 重複IDを持つ " +
                        typeof(T).Name +
                        " があります: " +
                        id +
                        " / " +
                        path);
                }
            }

            return result;
        }

        private static string ReadCsvText(
            string absolutePath)
        {
            byte[] bytes =
                File.ReadAllBytes(absolutePath);

            try
            {
                return new UTF8Encoding(
                    false,
                    true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                try
                {
                    return Encoding.GetEncoding(
                        932).GetString(bytes);
                }
                catch
                {
                    return Encoding.UTF8.GetString(bytes);
                }
            }
        }

        private static List<List<string>> ParseCsv(
            string text)
        {
            List<List<string>> rows =
                new List<List<string>>();

            List<string> row =
                new List<string>();

            StringBuilder cell =
                new StringBuilder();

            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '"')
                {
                    if (inQuotes &&
                        i + 1 < text.Length &&
                        text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }

                    continue;
                }

                if (!inQuotes && c == ',')
                {
                    row.Add(cell.ToString());
                    cell.Clear();
                    continue;
                }

                if (!inQuotes &&
                    (c == '\r' || c == '\n'))
                {
                    if (c == '\r' &&
                        i + 1 < text.Length &&
                        text[i + 1] == '\n')
                    {
                        i++;
                    }

                    row.Add(cell.ToString());
                    cell.Clear();

                    rows.Add(row);
                    row = new List<string>();
                    continue;
                }

                cell.Append(c);
            }

            if (cell.Length > 0 ||
                row.Count > 0)
            {
                row.Add(cell.ToString());
                rows.Add(row);
            }

            return rows;
        }

        private static Dictionary<string, int>
            BuildHeaderMap(
                List<string> headerRow)
        {
            Dictionary<string, int> result =
                new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < headerRow.Count; i++)
            {
                string name =
                    (headerRow[i] ?? string.Empty)
                    .Trim()
                    .TrimStart('\uFEFF');

                if (!string.IsNullOrWhiteSpace(name) &&
                    !result.ContainsKey(name))
                {
                    result.Add(name, i);
                }
            }

            return result;
        }

        private static string GetCell(
            List<string> row,
            Dictionary<string, int> header,
            string column)
        {
            if (!header.TryGetValue(
                    column,
                    out int index))
            {
                return string.Empty;
            }

            if (index < 0 || index >= row.Count)
                return string.Empty;

            return row[index] ?? string.Empty;
        }

        private static long ReadLong(
            List<string> row,
            Dictionary<string, int> header,
            string column,
            long fallback)
        {
            string value =
                GetCell(
                    row,
                    header,
                    column).Trim();

            if (long.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out long result))
            {
                return result;
            }

            Debug.LogWarning(
                "[MonsterCsvImporter] " +
                column +
                " の数値を読めません: '" +
                value +
                "'");

            return fallback;
        }

        private static int ReadInt(
            List<string> row,
            Dictionary<string, int> header,
            string column,
            int fallback)
        {
            string value =
                GetCell(
                    row,
                    header,
                    column).Trim();

            if (int.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int result))
            {
                return result;
            }

            Debug.LogWarning(
                "[MonsterCsvImporter] " +
                column +
                " の数値を読めません: '" +
                value +
                "'");

            return fallback;
        }

        private static float ReadFloat(
            List<string> row,
            Dictionary<string, int> header,
            string column,
            float fallback)
        {
            string value =
                GetCell(
                    row,
                    header,
                    column).Trim();

            if (float.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float result))
            {
                return result;
            }

            if (float.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.CurrentCulture,
                    out result))
            {
                return result;
            }

            Debug.LogWarning(
                "[MonsterCsvImporter] " +
                column +
                " の数値を読めません: '" +
                value +
                "'");

            return fallback;
        }

        private static bool ReadBool(
            List<string> row,
            Dictionary<string, int> header,
            string column,
            bool fallback)
        {
            string value =
                GetCell(
                    row,
                    header,
                    column).Trim();

            if (bool.TryParse(
                    value,
                    out bool result))
            {
                return result;
            }

            switch (value.ToUpperInvariant())
            {
                case "1":
                case "YES":
                case "ON":
                    return true;

                case "0":
                case "NO":
                case "OFF":
                    return false;
            }

            return fallback;
        }

        private static bool IsEmptyRow(
            List<string> row)
        {
            if (row == null || row.Count == 0)
                return true;

            for (int i = 0; i < row.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(row[i]))
                    return false;
            }

            return true;
        }

        private static void EnsureFolder(
            string folderPath)
        {
            string normalized =
                folderPath.Replace('\\', '/');

            string[] parts =
                normalized.Split('/');

            if (parts.Length == 0 ||
                parts[0] != "Assets")
            {
                throw new ArgumentException(
                    "Assets配下のフォルダを指定してください: " +
                    folderPath);
            }

            string current = "Assets";

            for (int i = 1; i < parts.Length; i++)
            {
                string next =
                    current + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(
                        current,
                        parts[i]);
                }

                current = next;
            }
        }

        private static string GetAbsoluteProjectPath(
            string assetPath)
        {
            string projectRoot =
                Directory.GetParent(
                    Application.dataPath).FullName;

            return Path.GetFullPath(
                Path.Combine(
                    projectRoot,
                    assetPath));
        }

        private static string SanitizeFileName(
            string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "Unnamed";

            char[] invalid =
                Path.GetInvalidFileNameChars();

            StringBuilder builder =
                new StringBuilder(fileName.Length);

            for (int i = 0; i < fileName.Length; i++)
            {
                char c = fileName[i];

                bool isInvalid = false;

                for (int j = 0; j < invalid.Length; j++)
                {
                    if (c == invalid[j])
                    {
                        isInvalid = true;
                        break;
                    }
                }

                builder.Append(
                    isInvalid ? '_' : c);
            }

            return builder.ToString();
        }
    }
}
#endif
