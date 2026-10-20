using UnityEngine;
using MixMaster.Monsters;

namespace MixMaster.Combat
{
    [RequireComponent(typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public sealed class PlayerAttackRangeIndicator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerAutoAttack playerAutoAttack;
        [SerializeField] private PartyMemberCombat partyMember;
        [SerializeField] private SpriteRenderer circleRenderer;

        [Header("Display")]
        [SerializeField] private bool visible = true;
        [SerializeField, Range(0f, 1f)] private float opacity = 0.18f;
        [SerializeField] private bool applyOpacity = true;
        [SerializeField] private int sortingOrderOffset = -10;
        [SerializeField, Min(0.01f)] private float radiusMultiplier = 1f;

        private float lastRange = -1f;
        private Sprite lastSprite;
        private Vector3 lastParentScale;

        private void Awake()
        {
            if (circleRenderer == null)
                circleRenderer = GetComponent<SpriteRenderer>();

            if (playerAutoAttack == null)
                playerAutoAttack =
                    GetComponentInParent<PlayerAutoAttack>();
            if (playerAutoAttack == null && partyMember == null)
                partyMember = GetComponentInParent<PartyMemberCombat>();

            ConfigureRenderer();
            RefreshScale(true);
        }

        private void LateUpdate()
        {
            if (circleRenderer == null)
                return;

            circleRenderer.enabled =
                visible &&
                (partyMember != null ? partyMember.IsAlive : playerAutoAttack != null) &&
                circleRenderer.sprite != null;

            if (!circleRenderer.enabled)
                return;

            RefreshScale(false);
        }

        public void SetVisible(bool value)
        {
            visible = value;

            if (circleRenderer != null)
                circleRenderer.enabled = value;
        }

        public void SetPartyMember(PartyMemberCombat member)
        {
            partyMember = member;
            playerAutoAttack = null;
            ConfigureRenderer();
            RefreshScale(true);
        }

        public void RefreshScale(bool force = false)
        {
            if ((playerAutoAttack == null && partyMember == null) ||
                circleRenderer == null ||
                circleRenderer.sprite == null)
            {
                return;
            }

            float range =
                Mathf.Max(
                    0.01f,
                    (partyMember != null ? partyMember.AttackRange : playerAutoAttack.AttackRange) *
                    radiusMultiplier);

            Vector3 parentScale = transform.parent != null
                ? transform.parent.lossyScale : Vector3.one;

            if (!force &&
                Mathf.Approximately(range, lastRange) &&
                lastSprite == circleRenderer.sprite &&
                lastParentScale == parentScale)
            {
                return;
            }

            lastRange = range;
            lastSprite = circleRenderer.sprite;
            lastParentScale = parentScale;

            Vector2 spriteSize =
                circleRenderer.sprite.bounds.size;

            if (spriteSize.x <= 0.0001f ||
                spriteSize.y <= 0.0001f)
            {
                return;
            }

            float desiredDiameter = range * 2f;

            float parentScaleX =
                Mathf.Max(0.0001f, Mathf.Abs(parentScale.x));

            float parentScaleY =
                Mathf.Max(0.0001f, Mathf.Abs(parentScale.y));

            transform.localScale =
                new Vector3(
                    desiredDiameter /
                    spriteSize.x /
                    parentScaleX,
                    desiredDiameter /
                    spriteSize.y /
                    parentScaleY,
                    1f);

            transform.localPosition =
                new Vector3(
                    0f,
                    0f,
                    transform.localPosition.z);
        }

        private void ConfigureRenderer()
        {
            if (circleRenderer == null)
                return;

            if (applyOpacity)
            {
                Color color = circleRenderer.color;
                color.a = opacity;
                circleRenderer.color = color;
            }

            SpriteRenderer playerRenderer =
                playerAutoAttack != null
                    ? playerAutoAttack.GetComponentInChildren<SpriteRenderer>()
                    : partyMember != null ? partyMember.GetComponentInChildren<SpriteRenderer>() : null;

            if (playerRenderer != null &&
                playerRenderer != circleRenderer)
            {
                circleRenderer.sortingLayerID =
                    playerRenderer.sortingLayerID;

                circleRenderer.sortingOrder =
                    playerRenderer.sortingOrder +
                    sortingOrderOffset;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            radiusMultiplier =
                Mathf.Max(0.01f, radiusMultiplier);

            if (circleRenderer == null)
                circleRenderer = GetComponent<SpriteRenderer>();

            if (applyOpacity &&
                circleRenderer != null)
            {
                Color color = circleRenderer.color;
                color.a = opacity;
                circleRenderer.color = color;
            }

            if (Application.isPlaying)
                RefreshScale(true);
        }
#endif
    }
}
