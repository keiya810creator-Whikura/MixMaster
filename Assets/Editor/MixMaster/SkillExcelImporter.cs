#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MixMaster.Core;
using UnityEditor;
using UnityEngine;

namespace MixMaster.EditorTools
{
    public static class SkillExcelImporter
    {
        public const string DefaultPath="Assets/DataCSV/スキル.xlsx";
        private const string CatalogPath="Assets/Resources/Skills/SkillCatalog.asset";
        [MenuItem("MixMaster/スキル/Excel・CSVを選んで取り込む")]
        public static void SelectFile()
        {
            string path=EditorUtility.OpenFilePanel("スキル定義の取り込み",Application.dataPath,"");
            if(!string.IsNullOrEmpty(path))Import(path,true);
        }
        [MenuItem("MixMaster/スキル/既定のスキル表を取り込む")]
        public static void ImportDefault()
        {
            string path=File.Exists(DefaultPath)?DefaultPath:"Assets/DataCSV/スキル.csv";
            Import(path,true);
        }
        public static void Import(string path,bool dialog)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("再生を停止してから取り込んでください。");
            var temporary=new List<SkillSO>();
            try
            {
                var rows=SkillTableReader.Read(path);var ids=new HashSet<string>();
                for(int i=0;i<rows.Count;i++)
                {
                    try{var s=Parse(rows[i]);temporary.Add(s);if(!ids.Add(s.skillId))throw new InvalidDataException("IDが重複しています: "+s.skillId);}
                    catch(Exception e){throw new InvalidDataException("データ行 "+(i+2)+": "+e.Message,e);}
                }
                if(temporary.Count==0)throw new InvalidDataException("データがないため更新しません。");
                var existing=new Dictionary<string,SkillSO>();
                foreach(string guid in AssetDatabase.FindAssets("t:SkillSO"))
                {
                    var skill=AssetDatabase.LoadAssetAtPath<SkillSO>(AssetDatabase.GUIDToAssetPath(guid));
                    if(skill==null||string.IsNullOrEmpty(skill.skillId))continue;
                    if(existing.ContainsKey(skill.skillId))throw new InvalidDataException("既存アセットのIDが重複: "+skill.skillId);
                    existing[skill.skillId]=skill;
                }
                Directory.CreateDirectory("Assets/Data/Skills");Directory.CreateDirectory("Assets/Resources/Skills");AssetDatabase.Refresh();
                var catalog=AssetDatabase.LoadAssetAtPath<SkillCatalogSO>(CatalogPath);
                if(catalog==null){catalog=ScriptableObject.CreateInstance<SkillCatalogSO>();AssetDatabase.CreateAsset(catalog,CatalogPath);}
                Undo.RecordObject(catalog,"スキル表取り込み");
                foreach(var incoming in temporary)
                {
                    var sourceRow=rows[temporary.IndexOf(incoming)];
                    if(existing.TryGetValue(incoming.skillId,out var target))
                    {
                        Undo.RecordObject(target,"スキル更新");
                        if(string.IsNullOrWhiteSpace(Cell(sourceRow,"画像パス")))incoming.icon=target.icon;
                        if(string.IsNullOrWhiteSpace(Cell(sourceRow,"演出パス")))incoming.effectPrefab=target.effectPrefab;
                        if(string.IsNullOrWhiteSpace(Cell(sourceRow,"発射物プレファブパス")))incoming.projectilePrefab=target.projectilePrefab;
                        EditorUtility.CopySerialized(incoming,target);EditorUtility.SetDirty(target);
                    }
                    else{target=ScriptableObject.CreateInstance<SkillSO>();EditorUtility.CopySerialized(incoming,target);AssetDatabase.CreateAsset(target,"Assets/Data/Skills/Skill_"+incoming.skillId+".asset");}
                    if(!catalog.skills.Contains(target))catalog.skills.Add(target);
                    // A new catalog makes the imported list available to the human book.
                    // Later imports preserve the user's existing human-only selection.
                }
                if(catalog.humanSkills.Count==0)catalog.humanSkills.AddRange(catalog.skills.Where(s=>s!=null&&s.executable));
                EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();AssetDatabase.Refresh();
                Debug.Log("[SkillExcelImporter] "+temporary.Count+"件を更新しました。数式のキャッシュではなく入力欄と発動形式から弾設定を確定します。");
                if(dialog)EditorUtility.DisplayDialog("スキル取り込み",temporary.Count+"件を取り込みました。\n既存IDの参照を維持して更新しました。","OK");
            }
            catch(Exception e){Debug.LogError("[SkillExcelImporter] "+e.Message);if(dialog)EditorUtility.DisplayDialog("スキル取り込み失敗",e.Message,"OK");else throw;}
            finally{foreach(var s in temporary)UnityEngine.Object.DestroyImmediate(s);}
        }
        private static string Cell(Dictionary<string,string> r,string key,bool required=false)
        {
            if(r.TryGetValue(key,out string v)&&!string.IsNullOrWhiteSpace(v))return v.Trim();
            if(required)throw new InvalidDataException("必須列・値がありません: "+key);return "";
        }
        private static double Number(Dictionary<string,string> r,string key,double fallback=0)
        {
            string value=Cell(r,key);if(value=="")return fallback;
            if(!double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out double n)||double.IsNaN(n)||double.IsInfinity(n)||n<0||n>float.MaxValue)throw new InvalidDataException(key+"は0以上の有限数で指定してください。");return n;
        }
        private static long Integer(Dictionary<string,string> r,string key,long fallback=0)
        {
            string v=Cell(r,key);if(v=="")return fallback;
            if(!decimal.TryParse(v,NumberStyles.Float,CultureInfo.InvariantCulture,out decimal n)||n<0||n>long.MaxValue||n!=decimal.Truncate(n))throw new InvalidDataException(key+"は0以上の整数で指定してください。");return (long)n;
        }
        private static int Int(Dictionary<string,string> r,string key,int fallback=0){long n=Integer(r,key,fallback);if(n>int.MaxValue)throw new InvalidDataException(key+"が大きすぎます。");return (int)n;}
        private static T Pick<T>(Dictionary<string,string> r,string key,string[] names,bool optional=false)where T:struct
        {
            string v=Cell(r,key,!optional);if(v==""&&optional)return default;
            int i=Array.IndexOf(names,v);if(i<0)throw new InvalidDataException(key+"の選択肢が不正: "+v);return (T)Enum.ToObject(typeof(T),i);
        }
        private static bool On(Dictionary<string,string> r,string key){string v=Cell(r,key);if(v=="有効"||v=="true"||v=="TRUE"||v=="1")return true;if(v=="無効"||v=="false"||v=="FALSE"||v=="0"||v=="")return false;throw new InvalidDataException(key+"は有効／無効です。");}
        public static SkillSO Parse(Dictionary<string,string> r)
        {
            var s=ScriptableObject.CreateInstance<SkillSO>();
            try
            {
                string id=Cell(r,"ID",true);
                if(!decimal.TryParse(id,NumberStyles.Float,CultureInfo.InvariantCulture,out decimal idNumber)||idNumber<1||idNumber!=decimal.Truncate(idNumber))throw new InvalidDataException("IDは正の整数にしてください。");
                s.skillId=idNumber.ToString("0",CultureInfo.InvariantCulture);s.name="Skill_"+s.skillId;
                s.displayName=Cell(r,"名前",true);s.description=Cell(r,"説明");
                s.requiredLevel=Int(r,"必要レベル",1);if(s.requiredLevel<1)throw new InvalidDataException("必要レベルは1以上です。");
                s.skillPointCost=Int(r,"習得ポイント",1);s.mpCost=Integer(r,r.ContainsKey("消費MP")?"消費MP":"消費魔力");
                s.cooldown=(float)Number(r,"再使用時間秒");s.castTime=(float)Number(r,"詠唱時間秒");s.globalCooldown=(float)Number(r,"共通待機時間秒",0.2);
                s.range=(float)Number(r,"射程");s.radius=(float)Number(r,"範囲半径");s.hitLimit=Int(r,"命中上限",1);
                string target=Cell(r,"対象",true);
                var targetMap=new Dictionary<string,SkillTargetType>{{"自身",SkillTargetType.Self},{"敵一体",SkillTargetType.SingleEnemy},{"味方一体",SkillTargetType.SingleAlly},{"自身中心",SkillTargetType.AreaAroundSelf},{"敵中心",SkillTargetType.AreaAroundTarget}};
                if(!targetMap.TryGetValue(target,out s.targetType))throw new InvalidDataException("対象の選択肢が不正です。");
                s.effect=Pick<SkillBaseEffect>(r,"基本効果",new[]{"ダメージ","回復","なし"});
                s.powerSource=Pick<SkillPowerSource>(r,"参照能力",new[]{"なし","攻撃力","魔法攻撃力","最大体力"});
                s.application=Pick<SkillApplication>(r,"適用先",new[]{"選択対象","自身","範囲内の敵","範囲内の味方"});
                s.powerMultiplier=(float)Number(r,"倍率");s.flatPower=Integer(r,"固定値");s.canCritical=On(r,"会心有効");
                string element=Cell(r,"属性",true);s.useOwnerElement=element=="使用者と同じ";
                if(!s.useOwnerElement)s.overrideElement=Pick<ElementType>(r,"属性",new[]{"無属性","火","水","風","雷","光","闇"});
                string delivery=Cell(r,"発動形式",true);if(delivery!="即時"&&delivery!="発射物")throw new InvalidDataException("発動形式は即時／発射物です。");s.projectile=delivery=="発射物";
                s.projectileSpeed=s.projectile?(float)Number(r,r.ContainsKey("発射物速度入力")?"発射物速度入力":"発射物速度"):0;
                s.projectileLifetime=s.projectile?(float)Number(r,r.ContainsKey("発射物寿命入力秒")?"発射物寿命入力秒":"発射物寿命秒"):0;
                s.homing=s.projectile&&On(r,r.ContainsKey("追尾入力")?"追尾入力":"追尾");
                s.useProjectileMotionOverrides=true;
                var motion=new List<ProjectileMotionOverride>();
                string homingKey=r.ContainsKey("追尾入力")?"追尾入力":"追尾";
                if(!string.IsNullOrWhiteSpace(Cell(r,homingKey)))motion.Add(new ProjectileMotionOverride{option=ProjectileMotionOption.Homing,value=s.homing?1:0});
                string[] motionColumns={"発射物_貫通","発射物_回転","発射物_回転速度","発射物_蛇行","発射物_蛇行幅","発射物_蛇行回数","発射物_加速","発射物_加速度","発射物_最高速度","発射物_命中半径"};
                ProjectileMotionOption[] motionOptions={ProjectileMotionOption.Piercing,ProjectileMotionOption.Spin,ProjectileMotionOption.SpinSpeed,ProjectileMotionOption.Wave,ProjectileMotionOption.WaveWidth,ProjectileMotionOption.WaveFrequency,ProjectileMotionOption.Accelerate,ProjectileMotionOption.Acceleration,ProjectileMotionOption.MaximumSpeed,ProjectileMotionOption.HitRadius};
                for(int i=0;i<motionColumns.Length;i++)
                {
                    string value=Cell(r,motionColumns[i]);if(value=="")continue;
                    float number;
                    if(i==0||i==1||i==3||i==6)number=On(r,motionColumns[i])?1:0;
                    else if(!float.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out number)||float.IsNaN(number)||float.IsInfinity(number)||Math.Abs(number)>1000000)throw new InvalidDataException(motionColumns[i]+"は有限の数値を指定してください。");
                    if((i==4||i==5)&&number<0)throw new InvalidDataException(motionColumns[i]+"は0以上です。");
                    if((i==8||i==9)&&number<=0)throw new InvalidDataException(motionColumns[i]+"は0より大きい値です。");
                    motion.Add(new ProjectileMotionOverride{option=motionOptions[i],value=number});
                }
                s.projectileMotionOverrides=motion.ToArray();
                ParseAdvancedMotion(r,motion);s.projectileMotionOverrides=motion.ToArray();
                if(s.projectile&&(s.projectileSpeed<=0||s.projectileLifetime<=0))throw new InvalidDataException("発射物の速度・寿命は0より大きい値です。");
                if(s.effect==SkillBaseEffect.Damage&&(s.powerSource!=SkillPowerSource.Attack&&s.powerSource!=SkillPowerSource.Magic))throw new InvalidDataException("ダメージの参照能力は攻撃力／魔法攻撃力です。");
                if(s.effect==SkillBaseEffect.Heal&&s.powerSource==SkillPowerSource.Attack)throw new InvalidDataException("回復には攻撃力を使えません。");
                bool allies=s.targetType==SkillTargetType.SingleAlly||s.targetType==SkillTargetType.Self;
                if(s.application==SkillApplication.SelectedTarget && ((s.effect==SkillBaseEffect.Damage&&allies)||(s.effect==SkillBaseEffect.Heal&&!allies)))throw new InvalidDataException("基本効果と対象の敵味方が一致しません。");
                if((s.application==SkillApplication.EnemiesInArea||s.application==SkillApplication.AlliesInArea)&&s.radius<=0)throw new InvalidDataException("範囲効果には正の範囲半径が必要です。");
                var effects=new List<SkillAffix>();
                for(int n=1;n<=3;n++)
                {
                    string p="追加効果"+n+"_";var e=new SkillAffix();
                    e.type=Pick<SkillAffixType>(r,p+"種別",new[]{"なし","バフ","デバフ","状態異常"},true);if(e.type==SkillAffixType.None)continue;
                    e.stat=Pick<SkillStat>(r,p+"対象能力",new[]{"なし","攻撃力","魔法攻撃力","防御力","魔法防御力","移動速度","攻撃速度","攻撃範囲"});
                    e.change=Pick<SkillChange>(r,p+"変化方式",new[]{"なし","割合変化","固定値変化","固定ダメージ","最大体力割合"});
                    e.amount=Number(r,p+"効果量");e.duration=(float)Number(r,p+"持続秒");e.chance=(float)Number(r,p+"付与確率％");
                    e.ailment=Pick<SkillAilment>(r,p+"状態異常",new[]{"なし","毒","火傷","凍結","麻痺","睡眠","沈黙","暗闇","混乱"});
                    e.interval=(float)Number(r,p+"継続間隔秒");e.stacking=Pick<SkillStacking>(r,p+"重複方式",new[]{"時間更新","重ね掛け","無視"});e.stackLimit=Int(r,p+"重複上限",1);
                    if(e.duration<=0||e.chance>100||e.stackLimit<1)throw new InvalidDataException(p+"持続秒・確率・重複上限を確認してください。");
                    if(e.type!=SkillAffixType.Status)
                    {if(e.stat==SkillStat.None||e.ailment!=SkillAilment.None||(e.change!=SkillChange.Percent&&e.change!=SkillChange.Flat)||e.interval!=0)throw new InvalidDataException(p+"能力変化の設定が不正です。");}
                    else
                    {
                        if(e.ailment==SkillAilment.None||e.stat!=SkillStat.None)throw new InvalidDataException(p+"状態異常を選んでください。");
                        bool dot=e.ailment==SkillAilment.Poison||e.ailment==SkillAilment.Burn;
                        if(dot&&(e.interval<=0||(e.change!=SkillChange.FixedDamage&&e.change!=SkillChange.MaxHpPercent)))throw new InvalidDataException(p+"継続ダメージには方式と正の間隔が必要です。");
                        if(!dot&&(e.change!=SkillChange.None||e.amount!=0||e.interval!=0||e.stacking==SkillStacking.Stack))throw new InvalidDataException(p+"行動系状態異常は量・間隔0、時間更新または無視です。");
                    }
                    if(e.stacking!=SkillStacking.Stack&&e.stackLimit!=1)throw new InvalidDataException(p+"重ね掛け以外の上限は1です。");effects.Add(e);
                }
                s.affixes=effects.ToArray();if(s.effect==SkillBaseEffect.None&&effects.Count==0)throw new InvalidDataException("効果がありません。");
                string icon=Cell(r,"画像パス"),vfx=Cell(r,"演出パス");
                if(icon!=""){s.icon=AssetDatabase.LoadAssetAtPath<Sprite>(icon);if(s.icon==null)throw new InvalidDataException("画像パスが不正: "+icon);}
                if(vfx!=""){s.effectPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(vfx);if(s.effectPrefab==null)throw new InvalidDataException("演出パスが不正: "+vfx);}
                string projectilePath=Cell(r,"発射物プレファブパス");
                if(projectilePath!="")
                {
                    s.projectilePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(projectilePath);
                    if(s.projectilePrefab==null||s.projectilePrefab.GetComponent<MixMaster.Combat.SkillProjectile>()==null||s.projectilePrefab.GetComponentInChildren<SpriteRenderer>()==null)
                        throw new InvalidDataException("発射物プレファブにはSkillProjectileとSpriteRendererが必要です: "+projectilePath);
                }
                s.executable=true;return s;
            }
            catch{UnityEngine.Object.DestroyImmediate(s);throw;}
        }
        public static readonly string[] MotionTypes={"直進","追尾","拡大直進","その場","ブーメラン","周回後に射出","蛇行","加速","減速停止","遅延追尾","螺旋","往復","感応地雷","収束成長"};
        public static readonly (ProjectileMotionOption option,string column)[] AdvancedMotionColumns={
            (ProjectileMotionOption.MoveType,"移動タイプ"),(ProjectileMotionOption.TurnSpeed,"追尾旋回速度"),
            (ProjectileMotionOption.GrowSpeed,"拡大速度"),(ProjectileMotionOption.MaximumScale,"最大拡大倍率"),
            (ProjectileMotionOption.OutboundTime,"直進秒"),(ProjectileMotionOption.ReturnSpeed,"帰還速度"),
            (ProjectileMotionOption.OrbitTime,"周回秒"),(ProjectileMotionOption.OrbitRadius,"周回半径"),(ProjectileMotionOption.OrbitAngularSpeed,"周回角速度"),
            (ProjectileMotionOption.DelayedHomingTime,"追尾開始秒"),(ProjectileMotionOption.SpiralAngularSpeed,"螺旋角速度"),(ProjectileMotionOption.SpiralGrowth,"螺旋拡大速度"),
            (ProjectileMotionOption.PingPongDistance,"往復距離"),(ProjectileMotionOption.Reversals,"反転回数"),(ProjectileMotionOption.DestroyAfterReversals,"反転終了で消滅"),
            (ProjectileMotionOption.FollowOwner,"使用者に追従"),(ProjectileMotionOption.RepeatHits,"再命中"),(ProjectileMotionOption.RepeatInterval,"再命中間隔秒"),
            (ProjectileMotionOption.ChargeTime,"収束秒"),(ProjectileMotionOption.ChargeScale,"収束最大拡大倍率"),(ProjectileMotionOption.ChargePower,"収束最大威力倍率"),
            (ProjectileMotionOption.TimedPhase,"時間切替"),(ProjectileMotionOption.PhaseTime,"切替秒"),(ProjectileMotionOption.PhaseType,"切替後移動タイプ"),(ProjectileMotionOption.PhaseSpeed,"切替後速度"),
            (ProjectileMotionOption.Chain,"連鎖"),(ProjectileMotionOption.ChainCount,"連鎖回数"),(ProjectileMotionOption.ChainRadius,"連鎖半径"),(ProjectileMotionOption.ChainPower,"連鎖威力倍率"),
            (ProjectileMotionOption.Split,"分裂"),(ProjectileMotionOption.SplitCount,"子弾数"),(ProjectileMotionOption.SplitAngle,"分裂角度"),(ProjectileMotionOption.SplitPower,"子弾威力倍率"),(ProjectileMotionOption.SplitDepth,"分裂世代数"),
            (ProjectileMotionOption.ExplodeOnHit,"命中時爆発"),(ProjectileMotionOption.ExplodeOnStop,"停止時爆発"),(ProjectileMotionOption.ExplosionRadius,"爆発半径"),(ProjectileMotionOption.ExplosionDuration,"爆発表示秒")};
        private static void ParseAdvancedMotion(Dictionary<string,string> row,List<ProjectileMotionOverride> result)
        {
            var flags=new HashSet<ProjectileMotionOption>{ProjectileMotionOption.DestroyAfterReversals,ProjectileMotionOption.FollowOwner,ProjectileMotionOption.RepeatHits,ProjectileMotionOption.TimedPhase,ProjectileMotionOption.Chain,ProjectileMotionOption.Split,ProjectileMotionOption.ExplodeOnHit,ProjectileMotionOption.ExplodeOnStop};
            foreach(var item in AdvancedMotionColumns)
            {
                string key="発射物_"+item.column,raw=Cell(row,key);if(raw=="")continue;
                float value;
                if(item.option==ProjectileMotionOption.MoveType||item.option==ProjectileMotionOption.PhaseType)
                {int index=Array.IndexOf(MotionTypes,raw);if(index<0)throw new InvalidDataException(key+"の移動タイプが不正です。");value=index;}
                else if(flags.Contains(item.option))value=On(row,key)?1:0;
                else
                {
                    if(!float.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out value)||float.IsNaN(value)||float.IsInfinity(value)||Math.Abs(value)>1000000)throw new InvalidDataException(key+"は有限の数値で指定してください。");
                    bool signed=item.option==ProjectileMotionOption.OrbitAngularSpeed||item.option==ProjectileMotionOption.SpiralAngularSpeed;
                    if(!signed&&value<0)throw new InvalidDataException(key+"は0以上です。");
                    bool integer=item.option==ProjectileMotionOption.Reversals||item.option==ProjectileMotionOption.ChainCount||item.option==ProjectileMotionOption.SplitCount||item.option==ProjectileMotionOption.SplitDepth;
                    if(integer&&(value!=(int)value||value>(item.option==ProjectileMotionOption.SplitDepth?3:16)))throw new InvalidDataException(key+"の整数範囲が不正です。");
                    if((item.option==ProjectileMotionOption.SplitCount||item.option==ProjectileMotionOption.Reversals||item.option==ProjectileMotionOption.PingPongDistance||item.option==ProjectileMotionOption.ExplosionRadius)&&value<=0)throw new InvalidDataException(key+"は0より大きい値です。");
                    if(item.option==ProjectileMotionOption.RepeatInterval&&value<.02f)throw new InvalidDataException(key+"は0.02秒以上です。");
                    if((item.option==ProjectileMotionOption.ChargeScale||item.option==ProjectileMotionOption.MaximumScale)&&value<1)throw new InvalidDataException(key+"は1以上です。");
                    if(item.option==ProjectileMotionOption.SplitAngle&&value>360)throw new InvalidDataException(key+"は360度以下です。");
                }
                result.Add(new ProjectileMotionOverride{option=item.option,value=value});
            }
        }
    }
}
#endif
