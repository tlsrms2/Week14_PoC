using UnityEngine;
using Week14.Audio;

namespace Week14.Enemy
{
    public sealed partial class MuscleBossAI : GraphBossAI
    {
        private static readonly int IsWalkParameter = Animator.StringToHash("isWalk");

        [SerializeField] private Animator walkAnimator;
        [SerializeField, Min(0f)] private float walkVelocityThreshold = 0.01f;

        private bool hasAppliedWalkState;
        private bool lastIsWalking;
        private SpriteRenderer facingSpriteRenderer;

        protected override bool RotatesBodyToPlayer => false;

        protected override void OnCombatStarted()
        {
            SoundManager.PlayBgm("MuscleBgm");
        }

        private void LateUpdate()
        {
            UpdateFacingSprite();
            UpdateWalkState();
        }

        private void UpdateFacingSprite()
        {
            SpriteRenderer spriteRenderer = ResolveFacingSpriteRenderer();
            if (spriteRenderer == null || Player == null || GraphContext?.IsDashing == true)
            {
                return;
            }

            spriteRenderer.flipX = Player.position.x > transform.position.x;
        }

        private SpriteRenderer ResolveFacingSpriteRenderer()
        {
            if (facingSpriteRenderer != null)
            {
                return facingSpriteRenderer;
            }

            Animator animator = ResolveWalkAnimator();
            facingSpriteRenderer = animator != null ? animator.GetComponent<SpriteRenderer>() : null;
            return facingSpriteRenderer;
        }

        private void UpdateWalkState()
        {
            if (Body == null)
            {
                ApplyWalkState(false, false);
                return;
            }

            float threshold = Mathf.Max(0f, walkVelocityThreshold);
            bool isWalking = Body.linearVelocity.sqrMagnitude > threshold * threshold;
            ApplyWalkState(isWalking, false);
        }

        private void ApplyWalkState(bool isWalking, bool force)
        {
            Animator targetAnimator = ResolveWalkAnimator();
            if (targetAnimator == null)
            {
                return;
            }

            if (!force && hasAppliedWalkState && lastIsWalking == isWalking)
            {
                return;
            }

            targetAnimator.SetBool(IsWalkParameter, isWalking);
            lastIsWalking = isWalking;
            hasAppliedWalkState = true;
        }

        private Animator ResolveWalkAnimator()
        {
            if (walkAnimator != null)
            {
                return walkAnimator;
            }

            walkAnimator = BodyRoot != null
                ? BodyRoot.GetComponentInChildren<Animator>(true)
                : GetComponentInChildren<Animator>(true);
            return walkAnimator;
        }
    }
}
