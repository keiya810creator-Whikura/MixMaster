using System;
using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    public sealed class EncyclopediaManager : MonoBehaviour
    {
        public static EncyclopediaManager Instance { get; private set; }
        private readonly HashSet<string> defeated =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance() => Instance = null;

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public bool IsUnlocked(string monsterId) =>
            !string.IsNullOrWhiteSpace(monsterId) && defeated.Contains(monsterId);

        public bool RecordDefeat(string monsterId)
        {
            if (string.IsNullOrWhiteSpace(monsterId) || !defeated.Add(monsterId))
                return false;
            Changed?.Invoke();
            return true;
        }

        public List<string> Capture()
        {
            var result = new List<string>(defeated);
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        public void Restore(IReadOnlyList<string> monsterIds)
        {
            defeated.Clear();
            if (monsterIds != null)
                for (int i = 0; i < monsterIds.Count; i++)
                    if (!string.IsNullOrWhiteSpace(monsterIds[i]))
                        defeated.Add(monsterIds[i]);
            Changed?.Invoke();
        }
    }
}
