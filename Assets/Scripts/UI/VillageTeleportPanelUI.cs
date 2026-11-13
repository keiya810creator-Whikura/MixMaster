using System;
using System.Collections.Generic;
using MixMaster.Core;
using MixMaster.Player;
using MixMaster.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    public sealed class VillageTeleportPanelUI : MonoBehaviour
    {
        private RectTransform content;
        private ScrollRect scroll;
        private TextMeshProUGUI status;
        private MapBuildingUIRouter router;
        private PlayerController player;
        private TilemapMapRoot village;

        internal static VillageTeleportPanelUI Create(Transform parent)
        {
            var root = new GameObject("VillageTeleportPanel", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 180;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            root.SetActive(false);
            var panel = root.AddComponent<VillageTeleportPanelUI>();
            panel.Build();
            return panel;
        }

        internal void Prepare(MapBuildingUIRouter owner, PlayerController traveler, TilemapMapRoot origin)
        {
            router = owner;
            player = traveler;
            village = origin;
            foreach (Transform child in content)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            var destinations = new List<TilemapMapRoot>();
            var mapManager = FindFirstObjectByType<MapManager>();
            if (mapManager != null)
                foreach (var map in FindObjectsByType<TilemapMapRoot>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var definition = map.MapDefinition;
                    if (map != village && map.gameObject.scene == village.gameObject.scene &&
                        definition != null && !definition.isTown && mapManager.HasVisitedMap(definition.mapId) &&
                        TryGetArrival(map, out _))
                        destinations.Add(map);
                }

            destinations.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(
                a.MapDefinition.mapId, b.MapDefinition.mapId));
            foreach (var map in destinations)
                AddDestination(map);

            status.text = destinations.Count == 0 ? "訪問済みの転移先がありません" : "";
        }

        private void OnEnable()
        {
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1f;
        }

        private void AddDestination(TilemapMapRoot destination)
        {
            MapSO map = destination.MapDefinition;
            var row = AddButton(content, "Destination", new Color(0.16f, 0.23f, 0.3f));
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 116f;

            var icon = AddRect(row.transform, "MapSprite", new Vector2(0, 0.5f), new Vector2(0, 0.5f));
            icon.sizeDelta = new Vector2(90f, 90f);
            icon.anchoredPosition = new Vector2(62f, 0f);
            var image = icon.gameObject.AddComponent<Image>();
            image.sprite = map.thumbnail;
            image.preserveAspect = true;
            image.enabled = map.thumbnail != null;
            image.raycastTarget = false;

            var label = AddText(row.transform, "MapName",
                string.IsNullOrWhiteSpace(map.displayName) ? map.mapId : map.displayName, 40f);
            label.rectTransform.offsetMin = new Vector2(126f, 6f);
            label.rectTransform.offsetMax = new Vector2(-16f, -6f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            row.onClick.AddListener(() => Travel(destination));
        }

        private void Travel(TilemapMapRoot destination)
        {
            var manager = FindFirstObjectByType<MapManager>();
            var definition = destination != null ? destination.MapDefinition : null;
            if (manager == null || definition == null || definition.isTown ||
                !manager.HasVisitedMap(definition.mapId) ||
                !TryGetArrival(destination, out Vector3 position) ||
                !MapExitTransition.MovePlayer(player, village, destination, position))
            {
                status.text = "このマップへ転移できません";
                return;
            }

            router.CloseAll();
        }

        private static bool TryGetArrival(TilemapMapRoot map, out Vector3 position)
        {
            position = default;
            var grid = map.GetComponent<Grid>();
            if (grid == null || map.Ground == null) return false;

            Transform spawn = map.transform.Find("_MapObjects/PlayerSpawnPoint");
            BoundsInt bounds = map.Ground.cellBounds;
            Vector3Int center = spawn != null ? grid.WorldToCell(spawn.position) :
                new Vector3Int(bounds.xMin + bounds.size.x / 2,
                    bounds.yMin + bounds.size.y / 2, bounds.zMin);
            int bestDistance = int.MaxValue;
            for (int y = -4; y <= 4; y++)
                for (int x = -4; x <= 4; x++)
                {
                    int distance = x * x + y * y;
                    if (distance >= bestDistance) continue;
                    Vector3Int cell = center + new Vector3Int(x, y, 0);
                    if (!map.Ground.HasTile(cell) ||
                        (map.Collision != null && map.Collision.HasTile(cell))) continue;

                    bestDistance = distance;
                    position = grid.GetCellCenterWorld(cell);
                }

            return bestDistance != int.MaxValue;
        }

        private void Build()
        {
            var backdrop = AddRect(transform, "Backdrop", Vector2.zero, Vector2.one);
            backdrop.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.66f);
            var card = AddRect(backdrop, "Card", new Vector2(0.07f, 0.20f), new Vector2(0.93f, 0.80f));
            card.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.13f, 0.18f);

            var heading = AddText(card, "Title", "転移魔方陣", 54f);
            heading.rectTransform.anchorMin = new Vector2(0f, 1f);
            heading.rectTransform.anchorMax = new Vector2(1f, 1f);
            heading.rectTransform.sizeDelta = new Vector2(0f, 100f);
            heading.rectTransform.anchoredPosition = new Vector2(0f, -57f);

            var view = AddRect(card, "Destinations", Vector2.zero, Vector2.one);
            view.offsetMin = new Vector2(24f, 112f);
            view.offsetMax = new Vector2(-24f, -115f);
            view.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.08f, 0.12f);
            view.gameObject.AddComponent<RectMask2D>();
            scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = view;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            content = AddRect(view, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f));
            content.pivot = new Vector2(0.5f, 1f);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;

            status = AddText(card, "Status", "", 34f);
            status.rectTransform.anchorMin = new Vector2(0.03f, 0.5f);
            status.rectTransform.anchorMax = new Vector2(0.97f, 0.5f);
            status.rectTransform.sizeDelta = new Vector2(0f, 100f);

            var close = AddButton(card, "Close", new Color(0.22f, 0.33f, 0.42f));
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.sizeDelta = new Vector2(270f, 76f);
            closeRect.anchoredPosition = new Vector2(0f, 54f);
            AddText(closeRect, "Label", "閉じる", 36f);
            close.onClick.AddListener(() => router.CloseAll());
        }

        private static RectTransform AddRect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Button AddButton(Transform parent, string name, Color color)
        {
            var rect = AddRect(parent, name, Vector2.zero, Vector2.one);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        private static TextMeshProUGUI AddText(Transform parent, string name, string value, float size)
        {
            var rect = AddRect(parent, name, Vector2.zero, Vector2.one);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = size;
            label.fontSizeMax = size;
            label.fontSizeMin = size * 0.65f;
            label.enableAutoSizing = true;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            label.richText = false;
            label.text = value;
            return label;
        }
    }
}
