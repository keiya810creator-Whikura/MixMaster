using MixMaster.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MixMaster.Combat
{
    [DisallowMultipleComponent]
    public sealed class PartyMagicProjectile : MonoBehaviour
    {
        [SerializeField, Min(.1f)] private float defaultSpeed = 8f;
        [SerializeField, Min(.01f)] private float hitDistance = .12f;
        [SerializeField] private bool rotateToDirection = true;

        private readonly PiercingMagicFlight flight = new PiercingMagicFlight();
        private EnemyHealth target;
        private PhysicsScene2D scene;
        private long magicPower;
        private MaterialDropSourceInfo source;
        private bool initialized;

        public void Initialize(EnemyHealth newTarget, long power, float speed, bool homing,
            MaterialDropSourceInfo attackSource)
        {
            target = newTarget;
            magicPower = System.Math.Max(1L, power);
            source = attackSource?.Clone() ?? MaterialDropSourceInfo.Unknown();
            scene = gameObject.scene.GetPhysicsScene2D();
            flight.Begin(transform, target, speed, homing);
            initialized = true;
        }

        private void Start()
        {
            if (!initialized) Initialize(null, 1L, defaultSpeed, false, null);
        }

        private void Update()
        {
            if (Time.deltaTime <= 0f) return;
            Vector2 start = flight.Step(transform, target != null && target.IsAlive, rotateToDirection);
            Vector2 end = transform.position;
            var enemies = EnemyHealth.ActiveEnemies;
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                if (i >= enemies.Count) continue;
                var enemy = enemies[i];
                if (enemy != null && enemy.IsAlive && flight.TryHit(enemy, scene, start, end, hitDistance))
                    enemy.TakeMagicHit(magicPower, source);
            }

            if (flight.Expired) Destroy(gameObject);
        }
    }
}
