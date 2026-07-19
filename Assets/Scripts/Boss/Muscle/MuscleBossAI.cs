using UnityEngine;
using Week14.Audio;

namespace Week14.Enemy
{
    public sealed partial class MuscleBossAI : GraphBossAI
    {
        private static readonly int IsWalkParameter = Animator.StringToHash("isWalk");
        private static readonly int StunParameter = Animator.StringToHash("Stun");
        private static readonly int EndStunParameter = Animator.StringToHash("EndStun");

        [SerializeField] private Animator walkAnimator;
        [SerializeField, Min(0f)] private float walkVelocityThreshold = 0.01f;

        [Header("BGM")]
        [Tooltip("전투 시작 시 재생할 BGM의 SoundLibrary ID입니다. 비워두면 재생하지 않습니다.")]
        [BossGraphBgmId]
        [SerializeField] private string bgmId = "MuscleBgm";

        private bool hasAppliedWalkState;
        private bool lastIsWalking;
        private SpriteRenderer facingSpriteRenderer;
        private Transform[] facingMirrorChildren;
        private Vector3[] facingMirrorBaseLocalPositions;
        private bool facingMirrorChildrenCached;

        protected override GameObject BossMuzzleFlashVfxPrefab => EffectData != null
            ? EffectData.MuscleMuzzleFlashVfxPrefab
            : null;
        protected override bool RotatesBodyToPlayer => false;

        protected override void OnCombatStarted()
        {
            if (!string.IsNullOrWhiteSpace(bgmId))
            {
                SoundManager.PlayBgm(bgmId);
            }
        }

        protected override void OnHpEmptyBegan()
        {
            SetAnimatorTrigger(StunParameter);
        }

        protected override void OnHpEmptyRecovered()
        {
            SetAnimatorTrigger(EndStunParameter);
        }

        protected override void OnBossDied()
        {
            ApplyWalkState(false, true);
            base.OnBossDied();
        }

        protected override void OnDisable()
        {
            ApplyWalkState(false, true);
            base.OnDisable();
        }

        private void LateUpdate()
        {
            UpdateFacingSprite();
            UpdateWalkState();
        }

        private void UpdateFacingSprite()
        {
            SpriteRenderer spriteRenderer = ResolveFacingSpriteRenderer();
            if (spriteRenderer == null || Player == null || GraphContext?.IsFacingLocked == true)
            {
                return;
            }

            bool flip = Player.position.x > transform.position.x;
            spriteRenderer.flipX = flip;
            ApplyFacingMirrorToChildren(spriteRenderer.transform, flip);
        }

        private void ApplyFacingMirrorToChildren(Transform facingRoot, bool flip)
        {
            CacheFacingMirrorChildren(facingRoot);
            for (int i = 0; i < facingMirrorChildren.Length; i++)
            {
                Transform child = facingMirrorChildren[i];
                if (child == null)
                {
                    continue;
                }

                Vector3 basePosition = facingMirrorBaseLocalPositions[i];
                child.localPosition = new Vector3(flip ? -basePosition.x : basePosition.x, basePosition.y, basePosition.z);
            }
        }

        private void CacheFacingMirrorChildren(Transform facingRoot)
        {
            if (facingMirrorChildrenCached)
            {
                return;
            }

            facingMirrorChildrenCached = true;
            int childCount = facingRoot.childCount;
            facingMirrorChildren = new Transform[childCount];
            facingMirrorBaseLocalPositions = new Vector3[childCount];
            for (int i = 0; i < childCount; i++)
            {
                Transform child = facingRoot.GetChild(i);
                facingMirrorChildren[i] = child;
                facingMirrorBaseLocalPositions[i] = child.localPosition;
            }
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

        private void SetAnimatorTrigger(int parameter)
        {
            Animator targetAnimator = ResolveWalkAnimator();
            if (targetAnimator == null)
            {
                return;
            }

            targetAnimator.SetTrigger(parameter);
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
