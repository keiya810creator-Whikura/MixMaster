#if UNITY_EDITOR
using MixMaster.Monsters;
using MixMaster.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.EditorTools
{
    public static class PartyFormationPrefabCreator
    {
        private const string UiFolder = "Assets/Prefab/UI";
        private const string PartyBarPath =
            "Assets/Prefab/UI/PartyFormationBar.prefab";
        private const string MemberFolder = "Assets/Resources/Prefabs";
        private const string MemberPrefabPath =
            "Assets/Resources/Prefabs/PartyMember.prefab";
        private const string DetailPath =
            "Assets/Prefab/UI/OwnedMonsterDetailPanel.prefab";

        [MenuItem("MixMaster/UI/パーティ編成UIを作成・一覧に接続")]
        public static void CreateAndAttach()
        {
            EnsureFolder(UiFolder);
            EnsureFolder(MemberFolder);
            EnsurePartyMemberPrefab();
            EnsureFormationBarPrefab();

            if (AssetDatabase.LoadAssetAtPath<GameObject>(
                    OwnedMonsterUiPrefabCreator.PanelPrefabPath) == null)
            {
                OwnedMonsterUiPrefabCreator.CreateOrUpdate();
            }

            // This operation creates the detail prefab if it is missing,
            // without replacing an existing list layout.
            OwnedMonsterDetailPrefabCreator.AttachFromListCreation();
            UpgradeDetailPrefab();
            AttachPartyBarToList();

            AssetDatabase.SaveAssets();
            Debug.Log("[PartyFormationPrefabCreator] パーティUIと共通仲間Prefabの準備が完了しました。");
        }

        private static void EnsurePartyMemberPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(
                    MemberPrefabPath) != null)
                return;

            GameObject root = new GameObject(
                "PartyMember",
                typeof(SpriteRenderer),
                typeof(Rigidbody2D),
                typeof(MonsterTrailFollower),
                typeof(PartyMemberCombat));

            SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
            renderer.sortingOrder = 10;

            Rigidbody2D body = root.GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;

            PrefabUtility.SaveAsPrefabAsset(root, MemberPrefabPath);
            Object.DestroyImmediate(root);
        }

        private static GameObject EnsureFormationBarPrefab()
        {
            GameObject existing =
                AssetDatabase.LoadAssetAtPath<GameObject>(PartyBarPath);

            if (existing != null)
                return existing;

            GameObject root = new GameObject(
                "PartyFormationBar",
                typeof(RectTransform),
                typeof(Image),
                typeof(PartyFormationBarUI));

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(960f, 165f);

            Image bg = root.GetComponent<Image>();
            bg.color = new Color(0.085f, 0.11f, 0.17f, 0.95f);
            bg.raycastTarget = false;

            TMP_Text count = CreateText(
                root.transform,
                "PartyCount",
                "パーティ 0/3　（仲間をタップして編成）",
                new Vector2(16f, -5f),
                new Vector2(840f, 36f),
                25f);

            Button[] buttons = new Button[3];
            Image[] icons = new Image[3];
            TMP_Text[] names = new TMP_Text[3];

            for (int i = 0; i < 3; i++)
            {
                GameObject slot = new GameObject(
                    "PartySlot_" + (i + 1),
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Button));

                slot.transform.SetParent(root.transform, false);
                RectTransform slotRect = slot.GetComponent<RectTransform>();

                TopLeft(slotRect,
                    new Vector2(12f + i * 312f, -50f),
                    new Vector2(300f, 103f));

                Image slotBackground = slot.GetComponent<Image>();
                slotBackground.color =
                    new Color(0.18f, 0.23f, 0.3f, 1f);

                buttons[i] = slot.GetComponent<Button>();

                GameObject iconObject = new GameObject(
                    "MonsterIcon",
                    typeof(RectTransform),
                    typeof(Image));

                iconObject.transform.SetParent(slot.transform, false);

                TopLeft(iconObject.GetComponent<RectTransform>(),
                    new Vector2(7f, -7f),
                    new Vector2(85f, 85f));

                icons[i] = iconObject.GetComponent<Image>();
                icons[i].preserveAspect = true;
                icons[i].raycastTarget = false;
                icons[i].enabled = false;

                names[i] = CreateText(
                    slot.transform,
                    "MonsterName",
                    (i + 1) + ". 空き枠",
                    new Vector2(98f, -17f),
                    new Vector2(193f, 80f),
                    23f);

                names[i].enableAutoSizing = true;
                names[i].fontSizeMin = 15f;
                names[i].fontSizeMax = 23f;
                names[i].raycastTarget = false;
            }

            root.GetComponent<PartyFormationBarUI>()
                .SetReferences(count, buttons, icons, names);

            GameObject saved =
                PrefabUtility.SaveAsPrefabAsset(root, PartyBarPath);

            Object.DestroyImmediate(root);
            return saved;
        }

        private static void UpgradeDetailPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(
                    DetailPath) == null)
                return;

            GameObject root = PrefabUtility.LoadPrefabContents(DetailPath);

            try
            {
                OwnedMonsterDetailUI detail =
                    root.GetComponent<OwnedMonsterDetailUI>();

                if (detail == null)
                    return;

                Transform buttonTransform =
                    root.transform.Find("PartyActionButton");

                Button partyButton = buttonTransform != null
                    ? buttonTransform.GetComponent<Button>()
                    : null;

                TMP_Text partyButtonText = null;

                if (partyButton == null)
                {
                    partyButton = CreateTopRightButton(
                        root.transform,
                        "PartyActionButton",
                        "パーティに編成",
                        new Vector2(-186f, -20f),
                        new Vector2(178f, 65f));
                }

                partyButtonText = partyButton.GetComponentInChildren<TMP_Text>(true);

                Transform statusTransform =
                    root.transform.Find("PartyStatusText");

                TMP_Text partyStatus = statusTransform != null
                    ? statusTransform.GetComponent<TMP_Text>()
                    : null;

                if (partyStatus == null)
                {
                    partyStatus = CreateText(
                        root.transform,
                        "PartyStatusText",
                        "パーティ 0/3",
                        new Vector2(380f, -64f),
                        new Vector2(300f, 34f),
                        22f);
                }

                detail.SetPartyButton(partyButton);
                detail.SetPartyButtonText(partyButtonText);
                detail.SetPartyStatusText(partyStatus);

                EditorUtility.SetDirty(detail);
                PrefabUtility.SaveAsPrefabAsset(root, DetailPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AttachPartyBarToList()
        {
            string path = OwnedMonsterUiPrefabCreator.PanelPrefabPath;

            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return;

            GameObject root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                if (root.GetComponent<OwnedMonsterListUI>() == null)
                    return;

                PartyFormationBarUI bar =
                    root.GetComponentInChildren<PartyFormationBarUI>(true);

                if (bar == null)
                {
                    GameObject prefab =
                        AssetDatabase.LoadAssetAtPath<GameObject>(PartyBarPath);

                    if (prefab == null)
                        return;

                    GameObject instance = PrefabUtility.InstantiatePrefab(
                        prefab, root.transform) as GameObject;

                    if (instance == null)
                        return;

                    instance.name = "PartyFormationBar";
                    instance.transform.SetSiblingIndex(
                        Mathf.Min(4, instance.transform.parent.childCount - 1));

                    RectTransform barRect =
                        instance.GetComponent<RectTransform>();

                    barRect.anchorMin = new Vector2(0f, 1f);
                    barRect.anchorMax = new Vector2(1f, 1f);
                    barRect.pivot = new Vector2(0.5f, 1f);
                    barRect.offsetMin = new Vector2(30f, -278f);
                    barRect.offsetMax = new Vector2(-30f, -108f);
                }

                // Leave room below the party bar; preserve other layout
                // settings and only move the scroll top if necessary.
                Transform scrollTransform = root.transform.Find("ScrollView");

                if (scrollTransform != null)
                {
                    RectTransform scroll =
                        scrollTransform.GetComponent<RectTransform>();

                    if (scroll != null && scroll.offsetMax.y > -292f)
                    {
                        Vector2 offset = scroll.offsetMax;
                        offset.y = -292f;
                        scroll.offsetMax = offset;
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Button CreateTopRightButton(
            Transform parent,
            string name,
            string caption,
            Vector2 position,
            Vector2 size)
        {
            GameObject root = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));

            root.transform.SetParent(parent, false);

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = root.GetComponent<Image>();
            image.color = new Color(0.19f, 0.4f, 0.46f, 1f);

            TMP_Text label = CreateText(root.transform, "Label",
                caption, Vector2.zero, size, 23f);

            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            label.alignment = TextAlignmentOptions.Center;

            return root.GetComponent<Button>();
        }

        private static TMP_Text CreateText(
            Transform parent,
            string name,
            string content,
            Vector2 position,
            Vector2 size,
            float fontSize)
        {
            GameObject obj = new GameObject(
                name,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));

            obj.transform.SetParent(parent, false);
            TopLeft(obj.GetComponent<RectTransform>(), position, size);

            TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Left;
            return text;
        }

        private static void TopLeft(
            RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void EnsureFolder(string folder)
        {
            string[] parts = folder.Replace('\\', '/').Split('/');
            string current = "Assets";

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
