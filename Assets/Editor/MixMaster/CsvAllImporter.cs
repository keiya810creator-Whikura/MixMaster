#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using MixMaster.Core;
using UnityEditor;
using UnityEngine;

namespace MixMaster.EditorTools
{
    /// <summary>
    /// One menu command for the existing CSV importers.
    /// Ordering matters: Titles -> Monsters -> Materials -> Maps -> Equipment -> EXP.
    /// Older unchanged importers may still show their own completion dialogs.
    /// </summary>
    public static class CsvAllImporter
    {
        private sealed class Step
        {
            public string label;
            public string csvPath;
            public Action action;
        }

        [MenuItem("MixMaster/CSV/一括取り込み")]
        public static void ImportAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                EditorUtility.DisplayDialog("CSV一括取り込み",
                    "再生中・コンパイル中には実行できません。", "OK");
                return;
            }

            var steps = new List<Step>
            {
                new Step { label = "スキル", csvPath = File.Exists(SkillExcelImporter.DefaultPath) ? SkillExcelImporter.DefaultPath : "Assets/DataCSV/スキル.csv",
                    action = () => SkillExcelImporter.Import(File.Exists(SkillExcelImporter.DefaultPath) ? SkillExcelImporter.DefaultPath : "Assets/DataCSV/スキル.csv", false) },
                new Step { label = "称号", csvPath = "Assets/DataCSV/称号.csv",
                    action = TitleCsvImporter.ImportOrUpdate },
                new Step { label = "モンスター", csvPath = "Assets/DataCSV/モンスター.csv",
                    action = MonsterCsvImporter.ImportOrUpdate },
                new Step { label = "特殊配合", csvPath = "Assets/DataCSV/特殊配合.csv",
                    action = BreedingRecipeCsvImporter.Import },
                new Step { label = "素材", csvPath = "Assets/DataCSV/素材.csv",
                    action = () =>
                    {
                        if (!MaterialCsvImporter.Import(true, out string message))
                            throw new InvalidOperationException(message);
                    } },
                new Step { label = "マップ", csvPath = "Assets/DataCSV/マップ.csv",
                    action = MapCsvImporter.ImportOrUpdate },
                new Step { label = "装備", csvPath = "Assets/DataCSV/装備.csv",
                    action = () =>
                    {
                        if (!EquipmentCsvImporter.Import(true, out string message))
                            throw new InvalidOperationException(message);
                    } },
                new Step { label = "経験値", csvPath = "Assets/DataCSV/経験値テーブル.csv",
                    action = ExperienceCsvImporter.Import }
            };

            UnityEngine.Object previousSelection = Selection.activeObject;
            var report = new List<string>();
            int succeeded = 0, skipped = 0, failed = 0;
            int stepErrors = 0;
            Application.LogCallback errorCallback = (condition, trace, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    stepErrors++;
            };

            try
            {
                Selection.activeObject = null; // Force every importer to read its own default CSV.
                AssetDatabase.Refresh();

                foreach (Step step in steps)
                {
                    string absolutePath = Path.GetFullPath(Path.Combine(
                        Directory.GetParent(Application.dataPath).FullName, step.csvPath));
                    if (!File.Exists(absolutePath))
                    {
                        skipped++;
                        report.Add("スキップ: " + step.label + " (CSVなし)");
                        continue;
                    }

                    stepErrors = 0;
                    Debug.Log("[CsvAllImporter] 開始: " + step.label + " / " + step.csvPath);
                    Application.logMessageReceived += errorCallback;
                    try
                    {
                        step.action();
                        if (stepErrors == 0)
                        {
                            succeeded++;
                            report.Add("完了: " + step.label);
                        }
                        else
                        {
                            failed++;
                            report.Add("要確認: " + step.label + " (エラー " + stepErrors + " 件)");
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        report.Add("失敗: " + step.label + " - " + ex.Message);
                        Debug.LogError("[CsvAllImporter] " + step.label + " のインポート失敗: " + ex);
                    }
                    finally
                    {
                        Application.logMessageReceived -= errorCallback;
                    }
                }

                // Re-apply images even when a CSV was absent, or a legacy importer
                // modified material definitions during its own import.
                try
                {
                    report.Add("素材画像: " + MaterialSpriteAutoAssigner.AssignAll(true));
                    report.Add("装備画像: " + EquipmentCommonSpriteAssigner.AssignAll());
                }
                catch (Exception ex)
                {
                    failed++;
                    report.Add("画像の再設定で失敗: " + ex.Message);
                    Debug.LogError("[CsvAllImporter] Image assignment failure: " + ex);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                Application.logMessageReceived -= errorCallback;
                Selection.activeObject = previousSelection;
            }

            string summary = "CSV: 完了 " + succeeded + " / スキップ " + skipped +
                " / 要確認・失敗 " + failed + "\n\n" + string.Join("\n", report.ToArray()) +
                "\n\n※ 旧インポーターの完了ダイアログは従来どおり表示されます。";
            Debug.Log("[CsvAllImporter] " + summary.Replace("\n", " / "));
            EditorUtility.DisplayDialog("MixMaster CSV 一括取り込み結果", summary, "OK");
        }
    }
}
#endif
