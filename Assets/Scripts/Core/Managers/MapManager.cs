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
            CurrentMapId = mapId ?? string.Empty;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetCurrentMap(CurrentMapId);
                GameManager.Instance.SetMode(GameMode.Exploration);
            }

            MapChanged?.Invoke(CurrentMapId);
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