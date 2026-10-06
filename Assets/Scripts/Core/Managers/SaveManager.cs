using UnityEngine;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    public sealed class SaveManager : MonoBehaviour
    {
        private const string SaveKey = "MixMaster.Save";

        public bool HasSaveData => PlayerPrefs.HasKey(SaveKey);

        public void SaveJson(string json)
        {
            PlayerPrefs.SetString(SaveKey, json ?? string.Empty);
            PlayerPrefs.Save();
        }

        public string LoadJson()
        {
            return PlayerPrefs.GetString(SaveKey, string.Empty);
        }

        public void DeleteSave()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }
    }
}