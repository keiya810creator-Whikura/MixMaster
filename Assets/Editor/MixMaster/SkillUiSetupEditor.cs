#if UNITY_EDITOR
using System.IO;
using MixMaster.UI;
using UnityEditor;
using UnityEngine;
namespace MixMaster.EditorTools
{
    public static class SkillUiSetupEditor
    {
        [MenuItem("MixMaster/スキル/スキルUIプレファブを作成・配置")]
        public static void Setup()
        {
            const string path="Assets/Resources/Skills/SkillHUD.prefab";
            var canvas=Object.FindFirstObjectByType<Canvas>();
            if(canvas==null){Debug.LogError("先にシーンへCanvasを配置してください。");return;}
            Directory.CreateDirectory("Assets/Resources/Skills");AssetDatabase.Refresh();
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab==null)
            {
                var temp=SkillScreenUI.Build(canvas.transform);
                prefab=PrefabUtility.SaveAsPrefabAsset(temp.gameObject,path);Object.DestroyImmediate(temp.gameObject);
            }
            if(Object.FindFirstObjectByType<SkillScreenUI>()==null){var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,canvas.transform);Undo.RegisterCreatedObjectUndo(instance,"スキルUI配置");}
            Selection.activeObject=prefab;
        }
    }
}
#endif
