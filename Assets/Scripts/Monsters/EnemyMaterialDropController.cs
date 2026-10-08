using UnityEngine;
using MixMaster.Combat;
using MixMaster.Core;

namespace MixMaster.Monsters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class EnemyMaterialDropController : MonoBehaviour
    {
        [SerializeField] private MonsterSO monsterDefinition;

        private EnemyHealth health;
        private DropManager dropManager;

        private void Awake()
        {
            health = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            if (health == null)
                health = GetComponent<EnemyHealth>();

            if (health != null)
                health.Died += HandleDied;
        }

        private void OnDisable()
        {
            if (health != null)
                health.Died -= HandleDied;
        }

        public void Configure(MonsterSO definition)
        {
            monsterDefinition = definition;
        }

        private void HandleDied(EnemyHealth enemy)
        {
            if (monsterDefinition == null ||
                monsterDefinition.uniqueMaterial == null)
            {
                return;
            }

            if (dropManager == null)
            {
                dropManager =
                    FindFirstObjectByType<DropManager>();
            }

            if (dropManager == null)
                return;

            MaterialDropSourceInfo source =
                enemy != null
                    ? enemy.LastHitSource
                    : MaterialDropSourceInfo.Unknown();

            long quantity =
                dropManager.RollMaterialQuantity(
                    monsterDefinition.materialBaseDropRate,
                    source != null
                        ? source.dropRateBonus
                        : 0f);

            if (quantity <= 0L)
                return;

            dropManager.SpawnMaterialDrop(
                monsterDefinition.uniqueMaterial,
                quantity,
                transform.position,
                source);
        }
    }
}
