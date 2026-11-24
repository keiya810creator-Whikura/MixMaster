#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using MixMaster.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MixMaster.EditorTools
{
    /// <summary>Places missing views in edit mode once, keeping subsequent scene adjustments.</summary>
    [InitializeOnLoad]
    internal static class EditableSceneUiInstaller
    {
        private const string MainScenePath = "Assets/Scenes/MainScene.unity";
        private const string PanelsPath = "Assets/Resources/UI/ScenePanels/";
        private static bool applying;

        static EditableSceneUiInstaller()
        {
            EditorSceneManager.sceneOpened += (_, __) => QueuePlacement();
            QueuePlacement();
        }

        private static void QueuePlacement() => EditorApplication.delayCall += PlaceInOpenMainScene;

        private static void PlaceInOpenMainScene()
        {
            if (applying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { QueuePlacement(); return; }
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.path == MainScenePath) Place(scene);
            }
        }

        [MenuItem("MixMaster/UI/不足している画面をシーンに配置")]
        private static void PlaceInActiveScene()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) Place(SceneManager.GetActiveScene());
        }

        private static IEnumerable<T> InScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<T>(true)) yield return component;
        }

        private static T First<T>(Scene scene, Func<T, bool> matches = null) where T : Component
        {
            foreach (var component in InScene<T>(scene))
                if (matches == null || matches(component)) return component;
            return null;
        }

        private static T PlacePanel<T>(Scene scene, Transform parent, string assetName,
            Func<T, bool> matches, ref bool changed) where T : Component
        {
            var existing = First(scene, matches);
            if (existing != null) return existing;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelsPath + assetName + ".prefab");
            if (prefab == null) return null;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(instance, "編集用UIを配置");
            instance.name = assetName;
            instance.SetActive(false);
            changed = true;
            return instance.GetComponent<T>();
        }

        private static void Assign(Component owner, string field, Object value, ref bool changed)
        {
            if (owner == null || value == null) return;
            var data = new SerializedObject(owner);
            var property = data.FindProperty(field);
            if (property == null || property.objectReferenceValue != null) return;
            property.objectReferenceValue = value;
            data.ApplyModifiedProperties();
            changed = true;
        }

        private static void Place(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || applying) return;
            var canvas = First<Canvas>(scene, c => c.isRootCanvas && c.renderMode != RenderMode.WorldSpace &&
                (c.name == "Canvas" || c.name == "Cacvas"));
            if (canvas == null) return;
            var safeArea = canvas.transform.Find("SafeArea") ?? canvas.transform.Find("SafeAreaHUD") ?? canvas.transform;
            var body = safeArea.Find("Body") ?? safeArea;
            bool wasDirty = scene.isDirty;
            bool changed = false;
            applying = true;
            try
            {
                var raid = PlacePanel<RaidPanelUI>(scene, body, "RaidPanel",
                    p => !new SerializedObject(p).FindProperty("trainingView").boolValue, ref changed);
                PlacePanel<RaidPanelUI>(scene, body, "TrainingPanel",
                    p => new SerializedObject(p).FindProperty("trainingView").boolValue, ref changed);
                var teleport = PlacePanel<VillageTeleportPanelUI>(scene, body, "VillageTeleportPanel", null, ref changed);
                var router = First<MapBuildingUIRouter>(scene);
                Assign(router, "raidPanel", raid, ref changed);
                Assign(router, "teleportPanel", teleport, ref changed);

                var farm = First<MonsterFarmPanelUI>(scene);
                foreach (var button in InScene<MonsterFarmButton>(scene))
                {
                    Assign(button, "panel", farm, ref changed);
                    PlaceFarmBadge(button, ref changed);
                }
                foreach (var skills in InScene<SkillScreenUI>(scene)) PlaceSkillControls(skills, ref changed);
                if (!changed) return;
                EditorSceneManager.MarkSceneDirty(scene);
                // Never save unrelated unsaved edits. A clean scene can safely persist this placement.
                if (!wasDirty) EditorSceneManager.SaveScene(scene);
                Debug.Log("[EditableUI] 編集用UIをシーンへ配置しました。非表示のパネルを一時的にActiveにすると調整できます。", canvas);
            }
            finally { applying = false; }
        }

        private static void PlaceSkillControls(SkillScreenUI skills, ref bool changed)
        {
            var data = new SerializedObject(skills);
            var panel = data.FindProperty("panel").objectReferenceValue as GameObject;
            if (panel == null) return;
            if (data.FindProperty("autoSetupButton").objectReferenceValue == null)
            {
                var existing = panel.transform.Find("AutoSetupSkills")?.GetComponent<Button>();
                if (existing == null)
                {
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Skills/SkillHUD.prefab");
                    var sourceData = source != null ? new SerializedObject(source.GetComponent<SkillScreenUI>()) : null;
                    var sourceButton = sourceData?.FindProperty("autoSetupButton").objectReferenceValue as Button;
                    if (sourceButton != null)
                    {
                        existing = Object.Instantiate(sourceButton, panel.transform, false);
                        existing.name = "AutoSetupSkills";
                        Undo.RegisterCreatedObjectUndo(existing.gameObject, "スキル操作ボタンを配置");
                    }
                }
                Assign(skills, "autoSetupButton", existing, ref changed);
            }
            bool found = false;
            foreach (var overlay in panel.GetComponentsInChildren<ProgressionPanelUI>(true))
                if (overlay.name == "AutoSetupSkills") found = true;
            if (!found)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(PanelsPath + "ProgressionPanel.prefab");
                if (source != null)
                {
                    var overlay = (GameObject)PrefabUtility.InstantiatePrefab(source, panel.transform);
                    Undo.RegisterCreatedObjectUndo(overlay, "スキル設定画面を配置");
                    overlay.name = "AutoSetupSkills";
                    overlay.SetActive(false);
                    changed = true;
                }
            }
            data.Update();
            if (data.FindProperty("detailsScroll").objectReferenceValue != null) return;
            var details = data.FindProperty("details").objectReferenceValue as TMP_Text;
            if (details == null) return;
            var old = details.rectTransform;
            var view = new GameObject("DetailsScroll", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            Undo.RegisterCreatedObjectUndo(view, "スキル説明欄を配置");
            view.layer = old.gameObject.layer;
            view.transform.SetParent(old.parent, false);
            var rect = (RectTransform)view.transform;
            rect.SetSiblingIndex(old.GetSiblingIndex());
            rect.anchorMin = old.anchorMin; rect.anchorMax = old.anchorMax; rect.pivot = old.pivot;
            rect.sizeDelta = old.sizeDelta; rect.anchoredPosition3D = old.anchoredPosition3D;
            rect.localScale = old.localScale; rect.localRotation = old.localRotation;
            view.GetComponent<Image>().color = new Color(.035f, .06f, .092f);
            Undo.RecordObject(old, "スキル説明の親を変更");
            Undo.SetTransformParent(old, rect, "スキル説明の親を変更");
            old.localRotation = Quaternion.identity; old.localScale = Vector3.one;
            old.anchorMin = new Vector2(0, 1); old.anchorMax = Vector2.one; old.pivot = new Vector2(0, 1);
            old.anchoredPosition3D = Vector3.zero; old.sizeDelta = Vector2.zero;
            Undo.RecordObject(details, "スキル説明をスクロール表示");
            details.enableAutoSizing = false;
            details.alignment = TextAlignmentOptions.TopLeft;
            details.richText = true; details.textWrappingMode = TextWrappingModes.Normal;
            details.overflowMode = TextOverflowModes.Overflow; details.raycastTarget = false;
            details.margin = new Vector4(14, 9, 14, 9);
            var fitter = details.GetComponent<ContentSizeFitter>() ?? Undo.AddComponent<ContentSizeFitter>(details.gameObject);
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = view.GetComponent<ScrollRect>();
            scroll.viewport = rect; scroll.content = old; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 36;
            Assign(skills, "detailsScroll", scroll, ref changed);
        }

        private static void PlaceFarmBadge(MonsterFarmButton button, ref bool changed)
        {
            var data = new SerializedObject(button);
            if (data.FindProperty("readyBadge").objectReferenceValue != null) return;
            var existing = button.transform.Find("FarmReadyBadge")?.GetComponent<TMP_Text>();
            if (existing == null)
            {
                var root = new GameObject("FarmReadyBadge", typeof(RectTransform), typeof(TextMeshProUGUI));
                Undo.RegisterCreatedObjectUndo(root, "ファーム通知を配置");
                root.layer = button.gameObject.layer;
                root.transform.SetParent(button.transform, false);
                var rect = (RectTransform)root.transform;
                rect.anchorMin = new Vector2(0, .7f); rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                existing = root.GetComponent<TMP_Text>();
                var source = button.GetComponentInChildren<TMP_Text>(true);
                existing.font = source != null ? source.font : TMP_Settings.defaultFontAsset;
                existing.fontSize = 22; existing.alignment = TextAlignmentOptions.Center;
                existing.color = new Color(1f, .85f, .2f); existing.raycastTarget = false;
                existing.text = "育成完了 0";
                root.SetActive(false);
            }
            Assign(button, "readyBadge", existing, ref changed);
        }
    }
}
#endif
