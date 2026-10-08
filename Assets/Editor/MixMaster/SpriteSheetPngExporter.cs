#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MixMaster.EditorTools
{
    public static class SpriteSheetPngExporter
    {
        private const string MenuPath =
            "MixMaster/Map/選択SpriteSheetを個別PNGへ書き出し";

        [MenuItem(MenuPath)]
        public static void ExportSelectedSpriteSheets()
        {
            List<Texture2D> textures =
                GetSelectedTextures();

            if (textures.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "SpriteSheet PNG書き出し",
                    "ProjectウィンドウでSpriteSheet画像を選択してください。",
                    "OK");

                return;
            }

            int exportedSheetCount = 0;
            int exportedSpriteCount = 0;
            int skippedCount = 0;

            try
            {
                for (int i = 0; i < textures.Count; i++)
                {
                    Texture2D texture = textures[i];

                    EditorUtility.DisplayProgressBar(
                        "SpriteSheet PNG書き出し",
                        texture.name + " を処理中...",
                        (float)i / Mathf.Max(1, textures.Count));

                    if (ExportTexture(
                            texture,
                            out int spriteCount))
                    {
                        exportedSheetCount++;
                        exportedSpriteCount += spriteCount;
                    }
                    else
                    {
                        skippedCount++;
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            string message =
                "完了しました。\n\n" +
                "SpriteSheet: " + exportedSheetCount + "\n" +
                "書き出したSprite: " + exportedSpriteCount + "\n" +
                "スキップ: " + skippedCount;

            Debug.Log(
                "[SpriteSheetPngExporter] " +
                message.Replace("\n", " / "));

            EditorUtility.DisplayDialog(
                "SpriteSheet PNG書き出し",
                message,
                "OK");
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateExportSelectedSpriteSheets()
        {
            return GetSelectedTextures().Count > 0;
        }

        private static List<Texture2D> GetSelectedTextures()
        {
            Dictionary<string, Texture2D> texturesByPath =
                new Dictionary<string, Texture2D>(
                    StringComparer.OrdinalIgnoreCase);

            UnityEngine.Object[] selected =
                Selection.objects;

            for (int i = 0; i < selected.Length; i++)
            {
                UnityEngine.Object obj = selected[i];

                if (obj == null)
                    continue;

                string path =
                    AssetDatabase.GetAssetPath(obj);

                if (string.IsNullOrWhiteSpace(path))
                    continue;

                Texture2D texture =
                    AssetDatabase.LoadAssetAtPath<Texture2D>(
                        path);

                if (texture != null &&
                    !texturesByPath.ContainsKey(path))
                {
                    texturesByPath.Add(path, texture);
                }
            }

            return texturesByPath.Values.ToList();
        }

        private static bool ExportTexture(
            Texture2D texture,
            out int exportedCount)
        {
            exportedCount = 0;

            if (texture == null)
                return false;

            string sourceAssetPath =
                AssetDatabase.GetAssetPath(texture);

            if (string.IsNullOrWhiteSpace(sourceAssetPath))
                return false;

            TextureImporter sourceImporter =
                AssetImporter.GetAtPath(sourceAssetPath)
                    as TextureImporter;

            if (sourceImporter == null)
            {
                Debug.LogWarning(
                    "[SpriteSheetPngExporter] TextureImporterを取得できません: " +
                    sourceAssetPath);

                return false;
            }

            Sprite[] sprites =
                AssetDatabase
                    .LoadAllAssetRepresentationsAtPath(
                        sourceAssetPath)
                    .OfType<Sprite>()
                    .OrderBy(sprite => sprite.name)
                    .ToArray();

            // Sprite Mode = Single の画像でも1枚として書き出せるようにする。
            if (sprites.Length == 0)
            {
                Sprite singleSprite =
                    AssetDatabase.LoadAssetAtPath<Sprite>(
                        sourceAssetPath);

                if (singleSprite != null)
                    sprites = new[] { singleSprite };
            }

            if (sprites.Length == 0)
            {
                Debug.LogWarning(
                    "[SpriteSheetPngExporter] Spriteが見つかりません: " +
                    sourceAssetPath);

                return false;
            }

            string sourceDirectory =
                Path.GetDirectoryName(sourceAssetPath)
                    ?.Replace('\\', '/');

            if (string.IsNullOrWhiteSpace(sourceDirectory))
                sourceDirectory = "Assets";

            string sourceFileName =
                Path.GetFileNameWithoutExtension(
                    sourceAssetPath);

            string outputAssetFolder =
                sourceDirectory +
                "/" +
                SanitizeFileName(sourceFileName) +
                "_Split";

            EnsureAssetFolder(outputAssetFolder);

            bool originalReadable =
                sourceImporter.isReadable;

            TextureImporterCompression originalCompression =
                sourceImporter.textureCompression;

            bool importerChanged = false;

            try
            {
                if (!sourceImporter.isReadable ||
                    sourceImporter.textureCompression !=
                    TextureImporterCompression.Uncompressed)
                {
                    sourceImporter.isReadable = true;
                    sourceImporter.textureCompression =
                        TextureImporterCompression.Uncompressed;

                    sourceImporter.SaveAndReimport();
                    importerChanged = true;
                }

                // Reimport後のTexture参照を取り直す。
                Texture2D readableTexture =
                    AssetDatabase.LoadAssetAtPath<Texture2D>(
                        sourceAssetPath);

                if (readableTexture == null)
                {
                    Debug.LogError(
                        "[SpriteSheetPngExporter] Texture再読込に失敗しました: " +
                        sourceAssetPath);

                    return false;
                }

                // ReimportでSpriteインスタンスが更新されるため取り直す。
                sprites =
                    AssetDatabase
                        .LoadAllAssetRepresentationsAtPath(
                            sourceAssetPath)
                        .OfType<Sprite>()
                        .OrderBy(sprite => sprite.name)
                        .ToArray();

                if (sprites.Length == 0)
                {
                    Sprite singleSprite =
                        AssetDatabase.LoadAssetAtPath<Sprite>(
                            sourceAssetPath);

                    if (singleSprite != null)
                        sprites = new[] { singleSprite };
                }

                Dictionary<string, int> fileNameUseCount =
                    new Dictionary<string, int>(
                        StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < sprites.Length; i++)
                {
                    Sprite sprite = sprites[i];

                    if (sprite == null)
                        continue;

                    Rect rect = sprite.rect;

                    int x = Mathf.RoundToInt(rect.x);
                    int y = Mathf.RoundToInt(rect.y);
                    int width = Mathf.RoundToInt(rect.width);
                    int height = Mathf.RoundToInt(rect.height);

                    if (width <= 0 || height <= 0)
                        continue;

                    Color[] pixels =
                        readableTexture.GetPixels(
                            x,
                            y,
                            width,
                            height);

                    Texture2D outputTexture =
                        new Texture2D(
                            width,
                            height,
                            TextureFormat.RGBA32,
                            false);

                    outputTexture.SetPixels(pixels);
                    outputTexture.Apply(
                        updateMipmaps: false,
                        makeNoLongerReadable: false);

                    byte[] pngBytes =
                        outputTexture.EncodeToPNG();

                    UnityEngine.Object.DestroyImmediate(
                        outputTexture);

                    string baseFileName =
                        SanitizeFileName(sprite.name);

                    if (string.IsNullOrWhiteSpace(baseFileName))
                    {
                        baseFileName =
                            SanitizeFileName(sourceFileName) +
                            "_" +
                            i;
                    }

                    string uniqueFileName =
                        GetUniqueExportFileName(
                            baseFileName,
                            fileNameUseCount);

                    string outputAssetPath =
                        outputAssetFolder +
                        "/" +
                        uniqueFileName +
                        ".png";

                    string absoluteOutputPath =
                        GetAbsoluteProjectPath(
                            outputAssetPath);

                    File.WriteAllBytes(
                        absoluteOutputPath,
                        pngBytes);

                    AssetDatabase.ImportAsset(
                        outputAssetPath,
                        ImportAssetOptions.ForceUpdate);

                    ConfigureOutputImporter(
                        outputAssetPath,
                        sourceImporter,
                        sprite,
                        width,
                        height);

                    exportedCount++;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[SpriteSheetPngExporter] 書き出しに失敗しました: " +
                    sourceAssetPath +
                    "\n" +
                    ex);

                return false;
            }
            finally
            {
                if (importerChanged)
                {
                    TextureImporter restoreImporter =
                        AssetImporter.GetAtPath(sourceAssetPath)
                            as TextureImporter;

                    if (restoreImporter != null)
                    {
                        restoreImporter.isReadable =
                            originalReadable;

                        restoreImporter.textureCompression =
                            originalCompression;

                        restoreImporter.SaveAndReimport();
                    }
                }
            }

            Debug.Log(
                "[SpriteSheetPngExporter] " +
                sourceAssetPath +
                " → " +
                outputAssetFolder +
                " / " +
                exportedCount +
                "枚");

            return exportedCount > 0;
        }

        private static void ConfigureOutputImporter(
            string outputAssetPath,
            TextureImporter sourceImporter,
            Sprite sourceSprite,
            int width,
            int height)
        {
            TextureImporter outputImporter =
                AssetImporter.GetAtPath(outputAssetPath)
                    as TextureImporter;

            if (outputImporter == null)
                return;

            outputImporter.textureType =
                TextureImporterType.Sprite;

            outputImporter.spriteImportMode =
                SpriteImportMode.Single;

            outputImporter.spritePixelsPerUnit =
                Mathf.Max(
                    0.01f,
                    sourceSprite.pixelsPerUnit);

            outputImporter.mipmapEnabled = false;
            outputImporter.alphaIsTransparency = true;
            outputImporter.wrapMode = TextureWrapMode.Clamp;

            if (sourceImporter != null)
            {
                outputImporter.filterMode =
                    sourceImporter.filterMode;

                outputImporter.textureCompression =
                    sourceImporter.textureCompression;
            }

            outputImporter.SaveAndReimport();
        }

        private static string GetUniqueExportFileName(
            string baseFileName,
            Dictionary<string, int> useCount)
        {
            if (!useCount.TryGetValue(
                    baseFileName,
                    out int count))
            {
                useCount.Add(baseFileName, 1);
                return baseFileName;
            }

            count++;
            useCount[baseFileName] = count;

            return baseFileName + "_" + count;
        }

        private static void EnsureAssetFolder(
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
                    Application.dataPath)?.FullName;

            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new InvalidOperationException(
                    "Unityプロジェクトのルートを取得できません。");
            }

            return Path.GetFullPath(
                Path.Combine(
                    projectRoot,
                    assetPath));
        }

        private static string SanitizeFileName(
            string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "Sprite";

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
