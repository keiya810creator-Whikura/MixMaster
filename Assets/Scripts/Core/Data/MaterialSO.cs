using UnityEngine;

namespace MixMaster.Core
{
    public enum MaterialCategory
    {
        Monster,
        Dungeon,
        Other
    }

    [CreateAssetMenu(
        fileName = "Material_",
        menuName = "MixMaster/Data/Material")]
    public sealed class MaterialSO : ScriptableObject
    {
        [Header("Identity")]
        public string materialId;
        public string displayName;
        [TextArea(2, 5)] public string description;
        public Sprite icon;

        [Header("Category")]
        public MaterialCategory category = MaterialCategory.Monster;
    }
}
