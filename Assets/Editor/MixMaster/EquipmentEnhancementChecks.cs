#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using MixMaster.Core;
using UnityEditor;
using UnityEngine;

namespace MixMaster.EditorTools
{
    /// <summary>
    /// Runs without NUnit/asmdef changes. Also usable with Unity batchmode:
    /// -executeMethod MixMaster.EditorTools.EquipmentEnhancementChecks.Run
    /// </summary>
    public static class EquipmentEnhancementChecks
    {
        [MenuItem("MixMaster/Tests/装備強化を検証")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Playモードを終了してから実行してください。");
            var owned = new List<UnityEngine.Object>();
            FieldInfo mapCache = typeof(MapCatalogSO).GetField("cached",
                BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo equipmentCache = typeof(EquipmentCatalogSO).GetField("cached",
                BindingFlags.NonPublic | BindingFlags.Static);
            object oldMaps = mapCache.GetValue(null);
            object oldEquipment = equipmentCache.GetValue(null);
            GameObject root = new GameObject("EquipmentEnhancementCheckFixture");
            try
            {
                var game = root.AddComponent<GameManager>();
                var inventory = root.AddComponent<MaterialInventoryManager>();
                var manager = root.AddComponent<EquipmentManager>();
                MapSO map = Asset<MapSO>(owned);
                map.mapId = "MAP_TEST";
                for (int i = 0; i < 6; i++)
                {
                    var monster = Asset<MonsterSO>(owned);
                    monster.monsterId = "MON_" + (i + 1).ToString("D3");
                    monster.uniqueMaterial = Asset<MaterialSO>(owned);
                    monster.uniqueMaterial.materialId = "MAT_TEST_" + i;
                    map.monsters.Add(monster);
                    if (i < 5) map.normalMonsters.Add(monster);
                    else map.bossMonster = monster;
                }
                var definition = Asset<EquipmentSO>(owned);
                definition.equipmentId = "EQP_TEST";
                definition.modifiers.maxHp = 0.01f;
                map.dungeonEquipment.Add(definition);
                var catalog = Asset<MapCatalogSO>(owned);
                catalog.SetData(new List<MapSO> { map });
                mapCache.SetValue(null, catalog);
                var definitions = Asset<EquipmentCatalogSO>(owned);
                definitions.SetData(new List<EquipmentSO> { definition });
                equipmentCache.SetValue(null, definitions);

                long[] expected = { 6, 8, 10, 12, 15, 18, 22, 27, 33, 40 };
                long[] expectedMaterials = { 1, 2, 2, 2, 3, 3, 4, 5, 6, 7 };
                for (int i = 0; i < expected.Length; i++)
                {
                    Check(EquipmentEnhancement.GoldCost(6, i) == expected[i], "毎回切り上げ " + i);
                    Check(EquipmentEnhancement.MaterialCost(expected[i]) == expectedMaterials[i],
                        "素材端数切り上げ " + i);
                }
                Check(EquipmentEnhancement.GoldCost(6, 49) == 61212, "50回目の費用");
                Check(EquipmentEnhancement.GoldCost(6, 50) == 0, "51回目の費用を発行しない");
                Check(EquipmentEnhancement.GoldCost(long.MaxValue, 1) == long.MaxValue,
                    "費用のlongオーバーフロー防止");

                EquipmentRecord item = manager.CreateEquipment("EQP_TEST", 20, 2L, false);
                Check(item.sourceMapId == map.mapId, "取得時に出身マップを記録");
                for (int missing = 0; missing < 6; missing++)
                {
                    Fill(inventory, map, 1L, missing);
                    game.RestoreGold(6L);
                    int notifications = 0;
                    Action<long> goldChanged = _ => notifications++;
                    Action<string, long, long> materialChanged = (id, delta, after) => notifications++;
                    game.GoldChanged += goldChanged;
                    inventory.MaterialChanged += materialChanged;
                    Check(!manager.TryEnhanceEquipment(item.uniqueId, out _), "素材不足 " + missing);
                    game.GoldChanged -= goldChanged;
                    inventory.MaterialChanged -= materialChanged;
                    Check(game.Gold == 6 && item.level == 20 && item.enhancementCount == 0 &&
                        notifications == 0, "不足時にGold・Lv・通知を変更しない");
                    for (int i = 0; i < 6; i++)
                        Check(inventory.GetAmount(map.monsters[i].uniqueMaterial) ==
                            (i == missing ? 0L : 1L), "不足時に他素材を消費しない");
                }
                Fill(inventory, map, 1L);
                game.RestoreGold(5L);
                Check(!manager.TryEnhanceEquipment(item.uniqueId, out _) &&
                    game.Gold == 5L && item.level == 20, "Gold不足時に変更しない");
                foreach (MonsterSO monster in map.monsters)
                    Check(inventory.GetAmount(monster.uniqueMaterial) == 1L, "Gold不足時の素材保護");

                game.RestoreGold(6L);
                int committedNotifications = 0;
                Action verifyCommitted = () =>
                {
                    committedNotifications++;
                    Check(game.Gold == 0L && item.level == 21 && item.enhancementCount == 1,
                        "全リソース確定後に通知");
                    foreach (MonsterSO monster in map.monsters)
                        Check(inventory.GetAmount(monster.uniqueMaterial) == 0L, "6種を同時に確定");
                    Check(!manager.TryEnhanceEquipment(item.uniqueId, out _), "再入による二重購入防止");
                };
                Action<long> onGold = _ => verifyCommitted();
                Action<string, long, long> onMaterial = (id, delta, after) => verifyCommitted();
                game.GoldChanged += onGold;
                inventory.MaterialChanged += onMaterial;
                Check(manager.TryEnhanceEquipment(item.uniqueId, out _), "1回目の購入");
                game.GoldChanged -= onGold;
                inventory.MaterialChanged -= onMaterial;
                Check(committedNotifications == 7 && item.originalLevel == 20, "Gold＋素材6種を通知");
                Check(Math.Abs(manager.GetScaledModifiers(item).maxHp - 0.21f) < 0.00001f,
                    "基礎値×強化後Lv");

                EquipmentRecord high = manager.CreateEquipment("EQP_TEST", 100, 2L, false);
                Fill(inventory, map, MaterialInventoryManager.MaxMaterialAmount);
                game.RestoreGold(10000000L);
                for (int i = 0; i < 50; i++)
                    Check(manager.TryEnhanceEquipment(high.uniqueId, out _), "連続購入 " + i);
                Check(high.level == 150 && high.originalLevel == 100 && high.enhancementCount == 50,
                    "取得Lv100＋50＝150");
                Check(game.Gold == 10000000L - 367152L, "50回分の累計Gold");
                foreach (MonsterSO monster in map.monsters)
                    Check(inventory.GetAmount(monster.uniqueMaterial) ==
                        MaterialInventoryManager.MaxMaterialAmount - 61207L, "50回分の累計素材");
                long before = game.Gold;
                Check(!manager.TryEnhanceEquipment(high.uniqueId, out _) && game.Gold == before,
                    "上限後の購入拒否");
                Check(manager.GetScaledValue(high) == 300L &&
                    Math.Abs(manager.GetScaledModifiers(high).maxHp - 1.5f) < 0.00001f,
                    "Lv100を超える値と補正");

                EquipmentRecord restored = JsonUtility.FromJson<EquipmentRecord>(JsonUtility.ToJson(high));
                EquipmentRecord legacy = JsonUtility.FromJson<EquipmentRecord>(
                    "{\"uniqueId\":\"legacy\",\"equipmentId\":\"EQP_TEST\",\"level\":87,\"baseValue\":2}");
                manager.RestoreEquipment(new[] { restored, legacy }, null);
                Check(restored.level == 150 && restored.originalLevel == 100 &&
                    restored.enhancementCount == 50 && restored.sourceMapId == map.mapId,
                    "JSON保存・復元後もLv150を保持");
                Check(legacy.level == 87 && legacy.originalLevel == 87 && legacy.enhancementCount == 0,
                    "旧セーブの取得Lvを引き継ぐ");
                Check(manager.TryEnhanceEquipment(legacy.uniqueId, out _), "旧装備の出身マップを推定");

                MapSO other = Asset<MapSO>(owned);
                other.mapId = "MAP_OTHER";
                other.dungeonEquipment.Add(definition);
                catalog.SetData(new List<MapSO> { map, other });
                game.SetCurrentMap(other.mapId);
                Check(manager.ResolveEnhancementMap(legacy) == map, "現在地が変わっても出身を保持");
                Check(catalog.FindEquipmentMap("EQP_TEST") == null, "旧装備の曖昧なマップを拒否");
                Check(!manager.TryEnhanceEquipment("missing", out _), "消失した装備を拒否");
                map.monsters[0].uniqueMaterial = null;
                Check(!EquipmentEnhancement.TryBuildCost(legacy, map, out _, out _), "欠落素材を拒否");

                Debug.Log("[EquipmentEnhancementChecks] すべての装備強化チェックに成功しました。");
            }
            finally
            {
                mapCache.SetValue(null, oldMaps);
                equipmentCache.SetValue(null, oldEquipment);
                UnityEngine.Object.DestroyImmediate(root);
                foreach (UnityEngine.Object asset in owned) UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        private static T Asset<T>(List<UnityEngine.Object> owned) where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            owned.Add(asset);
            return asset;
        }

        private static void Fill(MaterialInventoryManager inventory, MapSO map, long amount,
            int missing = -1)
        {
            var stock = new List<MaterialInventoryRecord>();
            for (int i = 0; i < map.monsters.Count; i++)
                stock.Add(new MaterialInventoryRecord { materialId = map.monsters[i].uniqueMaterial.materialId,
                    amount = i == missing ? 0L : amount });
            inventory.RestoreMaterials(stock);
        }

        private static void Check(bool passed, string name)
        {
            if (!passed) throw new InvalidOperationException("[EquipmentEnhancementChecks] " + name);
        }
    }
}
#endif
