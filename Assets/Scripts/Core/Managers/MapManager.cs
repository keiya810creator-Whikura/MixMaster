using System;
using UnityEngine;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    public sealed class MapManager : MonoBehaviour
    {
        public string CurrentMapId { get; private set; } = string.Empty;

        public event Action<string> MapChanged;

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

            MapChanged?.Invoke(CurrentMapId);
        }
    }
}