using System;
using UnityEngine;
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
            CloseAllInternal(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Open(MapBuildingType type)
        {
            GameObject targetPanel =
                type == MapBuildingType.Altar
                    ? altarPanel
                    : dungeonEntrancePanel;

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

        private void CloseAllInternal(bool invokeEvent)
        {
            SetPanelActive(altarPanel, false);
            SetPanelActive(dungeonEntrancePanel, false);

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
