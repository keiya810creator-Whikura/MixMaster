using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using MixMaster.Player;
using MixMaster.World;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class MapBuildingUIRouter : MonoBehaviour
    {
        public static MapBuildingUIRouter Instance { get; private set; }

        [Header("Panels")]
        [SerializeField] private GameObject altarPanel;
        [SerializeField] private GameObject dungeonEntrancePanel;
        [SerializeField] private GameObject breedingPanel;

        [Header("Behavior")]
        [SerializeField] private bool lockPlayerMovementWhileOpen = true;
        [SerializeField] private bool disableJoystickWhileOpen = true;

        public bool IsOpen { get; private set; }
        public MapBuildingType? CurrentBuilding { get; private set; }

        public event Action<MapBuildingType> BuildingOpened;
        public event Action BuildingClosed;

        private PlayerController cachedPlayer;
        private FloatingJoystick cachedJoystick;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Automatically provide a dungeon entrance panel when the scene
            // has not yet assigned one in the router Inspector.
            if (dungeonEntrancePanel == null ||
                dungeonEntrancePanel.GetComponent<DungeonEntrancePanelUI>() == null)
            {
                DungeonEntrancePanelUI existing =
                    FindFirstObjectByType<DungeonEntrancePanelUI>(
                        FindObjectsInactive.Include);
                if (existing == null)
                    existing = DungeonEntrancePanelUI.CreateRuntime(transform);
                dungeonEntrancePanel = existing.gameObject;
            }

            // BreedingShopPanel is now authored as a Prefab and placed in
            // the scene by the user (usually inactive under the scene Canvas).
            // Never instantiate a duplicate at runtime.
            if (breedingPanel == null ||
                breedingPanel.GetComponent<BreedingPanelUI>() == null)
            {
                BreedingPanelUI scenePanel =
                    FindFirstObjectByType<BreedingPanelUI>(FindObjectsInactive.Include);
                breedingPanel = scenePanel != null
                    ? scenePanel.gameObject : null;
            }

            if (breedingPanel == null)
                Debug.LogWarning(
                    "[MapBuildingUIRouter] シーンに配合パネルPrefabがありません。" +
                    " Assets/Prefab/UI/BreedingShopPanel.prefab をCanvas下に配置し、" +
                    " RouterのBreeding Panelへ設定してください。", this);

            CloseAllInternal(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Open(MapBuildingType type)
        {
            // UI itself comes from the scene. Keep standalone town scenes
            // usable when no global EventSystem has been provided.
            if (type == MapBuildingType.BreedingShop &&
                EventSystem.current == null)
            {
                new GameObject("BreedingShopEventSystem",
                    typeof(EventSystem), typeof(InputSystemUIInputModule));
            }

            GameObject targetPanel =
                type == MapBuildingType.Altar ? altarPanel :
                type == MapBuildingType.DungeonEntrance ? dungeonEntrancePanel :
                type == MapBuildingType.BreedingShop ? breedingPanel : null;

            if (targetPanel == null)
            {
                Debug.LogWarning(
                    "[MapBuildingUIRouter] " +
                    type +
                    " のPanelが未設定です。",
                    this);

                BuildingOpened?.Invoke(type);
                return;
            }

            SetPanelActive(
                altarPanel,
                type == MapBuildingType.Altar);

            SetPanelActive(
                dungeonEntrancePanel,
                type == MapBuildingType.DungeonEntrance);
            SetPanelActive(
                breedingPanel,
                type == MapBuildingType.BreedingShop);

            CurrentBuilding = type;
            IsOpen = true;

            SetPlayerMovementLocked(
                lockPlayerMovementWhileOpen);

            SetJoystickInteractionEnabled(false);

            BuildingOpened?.Invoke(type);
        }

        public void CloseAll()
        {
            CloseAllInternal(true);
        }

        public void SetAltarPanel(GameObject panel)
        {
            altarPanel = panel;
        }

        public void SetDungeonEntrancePanel(GameObject panel)
        {
            dungeonEntrancePanel = panel;
        }

        public void SetBreedingPanel(GameObject panel)
        {
            breedingPanel = panel;
        }

        private void CloseAllInternal(bool invokeEvent)
        {
            SetPanelActive(altarPanel, false);
            SetPanelActive(dungeonEntrancePanel, false);
            SetPanelActive(breedingPanel, false);

            bool wasOpen = IsOpen;

            IsOpen = false;
            CurrentBuilding = null;

            SetPlayerMovementLocked(false);
            SetJoystickInteractionEnabled(true);

            if (invokeEvent && wasOpen)
                BuildingClosed?.Invoke();
        }

        private void SetPlayerMovementLocked(bool locked)
        {
            if (!lockPlayerMovementWhileOpen && locked)
                return;

            if (cachedPlayer == null)
                cachedPlayer = FindFirstObjectByType<PlayerController>();

            cachedPlayer?.SetCombatMovementLocked(locked);
        }

        private void SetJoystickInteractionEnabled(
            bool enabled)
        {
            if (!disableJoystickWhileOpen)
                return;

            if (cachedJoystick == null)
            {
                cachedJoystick =
                    FindFirstObjectByType<FloatingJoystick>();
            }

            cachedJoystick?.SetInteractionEnabled(enabled);
        }

        private static void SetPanelActive(
            GameObject panel,
            bool active)
        {
            if (panel != null &&
                panel.activeSelf != active)
            {
                panel.SetActive(active);
            }
        }
    }
}
