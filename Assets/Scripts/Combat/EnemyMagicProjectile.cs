using MixMaster.Core;
using MixMaster.Monsters;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MixMaster.Combat
{
    [DisallowMultipleComponent]
    public sealed class EnemyMagicProjectile : MonoBehaviour
    {
        [SerializeField, Min(.1f)] private float defaultSpeed = 8f;
        [SerializeField, Min(.01f)] private float hitDistance = .12f;
        [SerializeField] private bool rotateToDirection = true;

        private readonly PiercingMagicFlight flight = new PiercingMagicFlight();
        private PlayerAutoAttack player;
        private Component aim;
        private PhysicsScene2D scene;
        private long magicPower;
        private ElementType element;
        private bool critical;
        private bool initialized;

        public void Initialize(PlayerAutoAttack activePlayer, Component target,
            long power, float speed, bool homing, ElementType attackElement, bool isCritical)
        {
            scene = gameObject.scene.GetPhysicsScene2D();
            player = activePlayer;
            aim = target;
            magicPower = System.Math.Max(1L, power);
            element = attackElement;
            critical = isCritical;
            flight.Begin(transform, aim, speed, homing);
            initialized = true;
        }

        private void Start()
        {
            if (!initialized) Initialize(null, null, 1L, defaultSpeed, false, ElementType.None, false);
        }

        private void Update()
        {
            if (Time.deltaTime <= 0f) return;
            bool aimAlive = aim != null && (aim is PartyMemberCombat memberTarget
                ? memberTarget.IsAlive : aim is PlayerAutoAttack playerTarget && playerTarget.IsAlive);
            Vector2 start = flight.Step(transform, aimAlive, rotateToDirection);
            Vector2 end = transform.position;
            if (player != null && player.isActiveAndEnabled && player.IsAlive && flight.TryHit(player, scene, start, end, hitDistance))
                player.TakeMagicHit(magicPower, element, critical);

            var members = PartyMemberCombat.ActiveMembers;
            for (int i = members.Count - 1; i >= 0; i--)
            {
                if (i >= members.Count) continue;
                var member = members[i];
                if (member != null && member.IsAlive && flight.TryHit(member, scene, start, end, hitDistance))
                    member.TakeMagicHit(magicPower, element, critical);
            }

            if (flight.Expired) Destroy(gameObject);
        }
    }
}
