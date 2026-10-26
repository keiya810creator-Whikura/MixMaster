#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MixMaster.Combat;
using MixMaster.Core;
using UnityEditor;
using UnityEngine;

namespace MixMaster.EditorTools
{
    // Generated assets are owned by the table. Existing GUIDs are kept by saving in place.
    public static class SkillProjectilePrefabBuilder
    {
        public const string OutputFolder = "Assets/Resources/Skills/Projectile";
        public static readonly string[] SpriteFolders = { "Assets/素材/Profectile", "Assets/素材/Projectile" };
        public sealed class Plan
        {
            public SkillSO skill;
            public Sprite sprite;
            public GameObject template;
            public ProjectileMotion motion;
            public float scale, angle;
            public int sortingOrder;
            public Color color;
            public string Path => OutputFolder + "/" + skill.skillId + ".prefab";
        }

        public static List<Plan> Prepare(IReadOnlyList<SkillSO> skills, IReadOnlyList<Dictionary<string,string>> rows)
        {
            var result = new List<Plan>();
            var sprites = IndexSprites();
            for (int i = 0; i < skills.Count; i++)
            {
                if (!skills[i].projectile) { skills[i].projectilePrefab = null; continue; }
                try
                {
                    var p = CreatePlan(skills[i], rows[i], sprites);
                    var existing = AssetDatabase.LoadMainAssetAtPath(p.Path);
                    if (existing != null && !(existing is GameObject))
                        throw new InvalidDataException("出力先にPrefab以外のアセットがあります: " + p.Path);
                    result.Add(p);
                }
                catch (Exception e) { throw new InvalidDataException("データ行 " + (i + 2) + " / ID " + skills[i].skillId + ": " + e.Message, e); }
            }
            return result;
        }

        private static Dictionary<string,List<Sprite>> IndexSprites()
        {
            var result = new Dictionary<string,List<Sprite>>();
            var folders = SpriteFolders.Where(AssetDatabase.IsValidFolder).ToArray();
            if (folders.Length == 0) return result;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", folders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                string fileId = NormalizeId(System.IO.Path.GetFileNameWithoutExtension(path));
                foreach (var sprite in sprites)
                {
                    string id = NormalizeId(sprite.name) ?? (sprites.Length == 1 ? fileId : null);
                    if (id == null) continue;
                    if (!result.TryGetValue(id, out var list)) result[id] = list = new List<Sprite>();
                    if (!list.Contains(sprite)) list.Add(sprite);
                }
            }
            return result;
        }

        public static string NormalizeId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Any(c => c < '0' || c > '9')) return null;
            string id = value.TrimStart('0');
            return id.Length == 0 ? null : id;
        }

        public static Plan CreatePlan(SkillSO skill, Dictionary<string,string> row, Dictionary<string,List<Sprite>> sprites)
        {
            string imageNumber=Cell(row,"発射物_画像番号");
            if(imageNumber=="")imageNumber=skill.skillId;
            imageNumber=NormalizeId(imageNumber);
            if(imageNumber==null)throw new InvalidDataException("発射物_画像番号は正の整数で指定してください。");
            if (!sprites.TryGetValue(imageNumber, out var matches) || matches.Count == 0)
                throw new InvalidDataException("スキル " + skill.skillId + " の画像番号 " + imageNumber + " のSpriteがありません。素材/Profectile または 素材/Projectile に " + imageNumber + ".png を配置し、Texture TypeをSprite (2D and UI)にしてください。");
            if (matches.Count != 1)
                throw new InvalidDataException("画像番号 " + imageNumber + " のSpriteが複数あります。両素材フォルダ・サブスプライトの番号を一意にしてください。");
            // Always rebuild defaults from the template, never from a previous generated prefab.
            var template = skill.projectilePrefab;
            if (template != null && AssetDatabase.GetAssetPath(template) == OutputFolder + "/" + skill.skillId + ".prefab") template = null;
            var motion = template != null ? template.GetComponent<SkillProjectile>().Defaults.Copy() : new ProjectileMotion();
            if (skill.projectileMotionOverrides != null)
                foreach (var item in skill.projectileMotionOverrides) motion.Apply(item);
            var plan = new Plan { skill = skill, sprite = matches[0], template = template, motion = motion,
                scale = Number(row, "発射物_表示倍率", .3f), angle = Number(row, "発射物_画像角度", 0), sortingOrder = 60, color = Color.white };
            if (plan.scale <= 0 || plan.scale > 1000) throw new InvalidDataException("発射物_表示倍率は0より大きく1000以下です。");
            string order = Cell(row, "発射物_描画順");
            if (order != "" && (!int.TryParse(order, NumberStyles.Integer, CultureInfo.InvariantCulture, out plan.sortingOrder) || plan.sortingOrder < -32768 || plan.sortingOrder > 32767))
                throw new InvalidDataException("発射物_描画順は-32768～32767の整数です。");
            string color = Cell(row, "発射物_色");
            if (color != "" && (!(color.Length == 7 || color.Length == 9) || color[0] != '#' || !ColorUtility.TryParseHtmlString(color, out plan.color)))
                throw new InvalidDataException("発射物_色は #RRGGBB または #RRGGBBAA です。");
            return plan;
        }

        private static string Cell(Dictionary<string,string> row, string name) => row.TryGetValue(name, out var value) ? value.Trim() : "";
        private static float Number(Dictionary<string,string> row, string name, float fallback)
        {
            string raw = Cell(row, name);
            if (raw == "") return fallback;
            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || float.IsNaN(value) || float.IsInfinity(value) || Math.Abs(value) > 1000000)
                throw new InvalidDataException(name + "は有限の数値にしてください。");
            return value;
        }

        public static void SaveAll(List<Plan> plans)
        {
            if (plans.Count == 0) return;
            Directory.CreateDirectory(OutputFolder);
            AssetDatabase.Refresh();
            foreach (var p in plans)
            {
                GameObject root = null;
                bool loaded = AssetDatabase.LoadAssetAtPath<GameObject>(p.Path) != null;
                try
                {
                    root = loaded ? PrefabUtility.LoadPrefabContents(p.Path) :
                        p.template != null ? UnityEngine.Object.Instantiate(p.template) : new GameObject(p.skill.skillId);
                    if (PrefabUtility.IsPartOfPrefabInstance(root))
                        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    root.name = p.skill.skillId;
                    root.SetActive(true);
                    root.transform.localPosition = Vector3.zero;
                    root.transform.localRotation = Quaternion.identity;
                    root.transform.localScale = Vector3.one * p.scale;
                    var projectile = root.GetComponent<SkillProjectile>() ?? root.AddComponent<SkillProjectile>();
                    projectile.enabled = true;
                    projectile.ConfigureDefaults(p.motion);
                    var renderer = root.GetComponentInChildren<SpriteRenderer>(true);
                    if (renderer == null || renderer.transform == root.transform)
                    {
                        var child = new GameObject("Visual");
                        child.transform.SetParent(root.transform, false);
                        var replacement = child.AddComponent<SpriteRenderer>();
                        if (renderer != null) { EditorUtility.CopySerialized(renderer, replacement); UnityEngine.Object.DestroyImmediate(renderer); }
                        renderer = replacement;
                    }
                    renderer.gameObject.SetActive(true);
                    renderer.enabled = true;
                    renderer.transform.localRotation = Quaternion.Euler(0, 0, p.angle);
                    renderer.sprite = p.sprite;
                    renderer.color = p.color;
                    renderer.sortingOrder = p.sortingOrder;
                    if (PrefabUtility.SaveAsPrefabAsset(root, p.Path) == null) throw new IOException("Prefabを保存できません: " + p.Path);
                    p.skill.projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(p.Path);
                    if (p.skill.projectilePrefab == null) throw new IOException("生成Prefabを読み込めません: " + p.Path);
                }
                finally
                {
                    if (root != null) { if (loaded) PrefabUtility.UnloadPrefabContents(root); else UnityEngine.Object.DestroyImmediate(root); }
                }
            }
        }
    }
}
#endif

