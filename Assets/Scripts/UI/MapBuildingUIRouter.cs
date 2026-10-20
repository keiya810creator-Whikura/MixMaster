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
        [SerializeField] private EquipmentInventoryUI blacksmithInventory;
        [SerializeField] private ReincarnationPanelUI reincarnationPanel;

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

            // Resolve the editable scene prefab, including inactive panels.
            if (dungeonEntrancePanel == null ||
                dungeonEntrancePanel.GetComponent<DungeonEntrancePanelUI>() == null)
            {
                DungeonEntrancePanelUI existing =
                    FindFirstObjectByType<DungeonEntrancePanelUI>(
                        FindObjectsInactive.Include);
                dungeonEntrancePanel = existing != null ? existing.gameObject : null;
            }
            if (dungeonEntrancePanel == null)
            {
                Debug.LogWarning("[MapBuildingUIRouter] Assets/Prefab/UI/DungeonEntrancePanel.prefab をCanvas下に配置してください。", this);
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

            ResolveReincarnationPanel();
            CloseAllInternal(false);
        }

        private void OnDestroy()
        {
            if (IsOpen) CloseAllInternal(false);
            if (blacksmithInventory != null)
                blacksmithInventory.Closed -= HandleBlacksmithInventoryClosed;
            if (reincarnationPanel != null)
                reincarnationPanel.Closed -= HandleReincarnationPanelClosed;
            if (Instance == this)
                Instance = null;
        }

        public void Open(MapBuildingType type)
        {
            if (IsOpen && CurrentBuilding == type) return;
            // UI itself comes from the scene. Keep standalone town scenes
            // usable when no global EventSystem has been provided.
            if ((type == MapBuildingType.BreedingShop || type == MapBuildingType.Blacksmith ||
                type == MapBuildingType.Reincarnation) &&
                EventSystem.current == null)
            {
                new GameObject("MapBuildingEventSystem",
                    typeof(EventSystem), typeof(InputSystemUIInputModule));
            }

            if (type == MapBuildingType.Blacksmith)
                ResolveBlacksmithInventory();
            if (type == MapBuildingType.Reincarnation)
                ResolveReincarnationPanel();

            GameObject targetPanel =
                type == MapBuildingType.Altar ? altarPanel :
                type == MapBuildingType.DungeonEntrance ? dungeonEntrancePanel :
                type == MapBuildingType.BreedingShop ? breedingPanel :
                type == MapBuildingType.Reincarnation && reincarnationPanel != null ? reincarnationPanel.gameObject :
                type == MapBuildingType.Blacksmith && blacksmithInventory != null
                    ? blacksmithInventory.gameObject : null;

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

            // Clear the previous building BEFORE disabling its inventory;
            // its Closed callback must not close the newly requested panel.
            CloseAllInternal(false);

            if (type == MapBuildingType.Blacksmith)
            {
                blacksmithInventory.OpenForBlacksmith();
                if (!blacksmithInventory.isActiveAndEnabled)
                {
                    blacksmithInventory.Close();
                    Debug.LogWarning("[MapBuildingUIRouter] 装備インベントリの親Canvasを有効にしてください。", this);
                    return;
                }
            }
            else
                SetPanelActive(targetPanel, true);

            if (!targetPanel.activeInHierarchy)
            {
                SetPanelActive(targetPanel, false);
                Debug.LogWarning("[MapBuildingUIRouter] Panelの親Canvasを有効にしてください。", this);
                return;
            }

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

        public void SetBlacksmithInventory(EquipmentInventoryUI inventory)
        {
            if (blacksmithInventory != null)
                blacksmithInventory.Closed -= HandleBlacksmithInventoryClosed;
            blacksmithInventory = inventory;
            if (blacksmithInventory != null)
                blacksmithInventory.Closed += HandleBlacksmithInventoryClosed;
        }

        private void ResolveBlacksmithInventory()
        {
            EquipmentInventoryUI inventory = blacksmithInventory != null
                ? blacksmithInventory
                : FindFirstObjectByType<EquipmentInventoryUI>(FindObjectsInactive.Include);
            SetBlacksmithInventory(inventory);
        }

        public void SetReincarnationPanel(ReincarnationPanelUI panel)
        {
            if (reincarnationPanel != null) reincarnationPanel.Closed -= HandleReincarnationPanelClosed;
            reincarnationPanel = panel;
            if (reincarnationPanel != null) reincarnationPanel.Closed += HandleReincarnationPanelClosed;
        }
        private void ResolveReincarnationPanel()
        {
            ReincarnationPanelUI panel = reincarnationPanel != null ? reincarnationPanel :
                FindFirstObjectByType<ReincarnationPanelUI>(FindObjectsInactive.Include);
            SetReincarnationPanel(panel);
        }
        private void HandleReincarnationPanelClosed()
        {
            if (IsOpen && CurrentBuilding == MapBuildingType.Reincarnation) CloseAll();
        }

        private void HandleBlacksmithInventoryClosed()
        {
            if (IsOpen && CurrentBuilding == MapBuildingType.Blacksmith)
                CloseAll();
        }

        private void CloseAllInternal(bool invokeEvent)
        {
            bool wasOpen = IsOpen;
            bool closeBlacksmith = CurrentBuilding == MapBuildingType.Blacksmith;
            IsOpen = false;
            CurrentBuilding = null;

            SetPanelActive(altarPanel, false);
            SetPanelActive(dungeonEntrancePanel, false);
            SetPanelActive(breedingPanel, false);
            if (reincarnationPanel != null) SetPanelActive(reincarnationPanel.gameObject, false);

            if (closeBlacksmith && blacksmithInventory != null)
                blacksmithInventory.Close();

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
