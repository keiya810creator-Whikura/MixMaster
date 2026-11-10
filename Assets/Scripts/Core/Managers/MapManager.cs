using System;
using UnityEngine;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    public sealed class MapManager : MonoBehaviour
    {
        public string CurrentMapId { get; private set; } = string.Empty;

        public event Action<string> MapChanged;

        public void RestoreMapId(string mapId)
        {
            CurrentMapId = mapId ?? string.Empty;
            MapChanged?.Invoke(CurrentMapId);
        }

        public void EnterMap(string mapId)
        {
            if (MapCatalogSO.Load()?.GetMap(mapId)?.isTown == true)
            {
                EnterTown(mapId);
                return;
            }
            CurrentMapId = mapId ?? string.Empty;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetCurrentMap(CurrentMapId);
                GameManager.Instance.SetMode(GameMode.Exploration);
            }

            MapChanged?.Invoke(CurrentMapId);
            ShowStageName();
        }

        public void EnterTown(string townMapId)
        {
            CurrentMapId = townMapId ?? string.Empty;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetCurrentMap(CurrentMapId);
                GameManager.Instance.SetMode(GameMode.Town);
            }

            RestoreTownResources();
            MapChanged?.Invoke(CurrentMapId);
            ShowStageName();
        }

        private void ShowStageName()
        {
            MapSO map = MapCatalogSO.Load()?.GetMap(CurrentMapId);
            string title = map != null && !string.IsNullOrWhiteSpace(map.displayName)
                ? map.displayName : CurrentMapId.Replace("TilemapMap_", "");
            MixMaster.UI.StageNameBannerUI.Show(title);
        }

        private void RestoreTownResources()
        {
            var player=GetComponent<PlayerManager>()??FindFirstObjectByType<PlayerManager>();
            player?.RestoreFullResources();
            foreach(var member in FindObjectsByType<MixMaster.Monsters.PartyMemberCombat>(FindObjectsSortMode.None))
                if(member!=null)member.RestoreFullResources();
        }
    }
}
