using UnityEngine;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class AcquisitionLogLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform materialContent, monsterContent;
        private MaterialDropLogUI materials;
        private MonsterObtainLogUI monsters;
        private void OnEnable()
        {
            materials = FindFirstObjectByType<MaterialDropLogUI>();
            monsters = FindFirstObjectByType<MonsterObtainLogUI>();
            if (materials != null) materials.UseSceneContent(materialContent);
            if (monsters != null) monsters.UseSceneContent(monsterContent);
        }
        private void OnDisable()
        {
            if (materials != null) materials.ReleaseSceneContent(materialContent);
            if (monsters != null) monsters.ReleaseSceneContent(monsterContent);
            materials = null; monsters = null;
        }
    }
}
