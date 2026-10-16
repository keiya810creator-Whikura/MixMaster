#if UNITY_EDITOR
using MixMaster.Core;
using UnityEditor;
using UnityEngine;

namespace MixMaster.EditorTools
{
    [CustomEditor(typeof(BreedingManager))]
    public sealed class BreedingManagerInspector : UnityEditor.Editor
    {
        private MonsterSO parentA;
        private MonsterSO parentB;
        private string previewMessage = "";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("配合結果シミュレーター（種族決定のみ）",
                EditorStyles.boldLabel);
            parentA = (MonsterSO)EditorGUILayout.ObjectField(
                "親A (MonsterSO)", parentA, typeof(MonsterSO), false);
            parentB = (MonsterSO)EditorGUILayout.ObjectField(
                "親B (MonsterSO)", parentB, typeof(MonsterSO), false);

            using (new EditorGUI.DisabledScope(parentA == null || parentB == null))
            {
                if (GUILayout.Button("配合結果を確認"))
                {
                    BreedingManager manager = (BreedingManager)target;
                    if (manager.TryResolveChild(parentA, parentB,
                            out MonsterSO child, out bool special))
                    {
                        int rank = manager.CalculateChildBreedingValue(parentA, parentB);
                        previewMessage =
                            (special ? "特殊配合" : "通常・同種配合") +
                            " : " + child.displayName + " (" + child.monsterId + ")" +
                            "\n配合計算値: " + rank +
                            " / 子の配合値: " + child.breedingValue +
                            " / 子のStage: " + child.appearanceStage +
                            "\n通常配合のStage上限: " +
                            (Mathf.Max(parentA.appearanceStage, parentB.appearanceStage) + 1);
                        Debug.Log("[BreedingManager] " +
                            previewMessage.Replace("\n", " / "));
                    }
                    else
                        previewMessage = "結果を取得できません。カタログとレシピをインポートしてください。";
                }
            }

            if (!string.IsNullOrEmpty(previewMessage))
                EditorGUILayout.HelpBox(previewMessage, MessageType.Info);

            EditorGUILayout.HelpBox(
                "このボタンは子の種族を確認するだけで、所持モンスターを消費しません。" +
                "実際の配合はTryBreed(親AのuniqueId, 親BのuniqueId, ...)を使用します。",
                MessageType.None);
        }
    }
}
#endif
