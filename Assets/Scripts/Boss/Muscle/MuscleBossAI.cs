using UnityEngine;

namespace Week14.Enemy
{
    public sealed partial class MuscleBossAI : GraphBossAI
    {
        private static readonly int IsWalkParameter = Animator.StringToHash("isWalk");
        private static readonly int StunParameter = Animator.StringToHash("Stun");
        private static readonly int EndStunParameter = Animator.StringToHash("EndStun");

        [SerializeField] private Animator walkAnimator;
        [SerializeField, Min(0f)] private float walkVelocityThreshold = 0.01f;
        [SerializeField, Header("Death Shadow")] private Transform deathShadow;
        [SerializeField] private SpriteRenderer deathShadowFrameSource;
        [SerializeField] private Animator deathShadowAnimator;
        [SerializeField] private string deathShadowAnimationStateName = "Anim-Security-Die";
        [SerializeField] private string deathShadowFramePrefix = "Security-Die_";
        [SerializeField] private string deathShadowStageOneFrameName = "Security-Die_3";
        [SerializeField, Min(0f)] private float deathShadowStageOneAnimationTime = 0.2f;
        [SerializeField] private float deathShadowStageOneLocalX = -0.05f;
        [SerializeField, Min(0f)] private float deathShadowStageOneScaleX = 0.55f;
        [SerializeField] private string deathShadowStageTwoFrameName = "Security-Die_5";
        [SerializeField, Min(0f)] private float deathShadowStageTwoAnimationTime = 0.5f;
        [SerializeField] private float deathShadowStageTwoLocalX = -0.19f;
        [SerializeField, Min(0f)] private float deathShadowStageTwoScaleX = 0.69f;
        [SerializeField] private bool mirrorDeathShadowLocalXByFacing = true;

        private bool hasAppliedWalkState;
        private bool lastIsWalking;
        private SpriteRenderer facingSpriteRenderer;
        private Transform[] facingMirrorChildren;
        private Vector3[] facingMirrorBaseLocalPositions;
        private bool facingMirrorChildrenCached;
        private Vector3 deathShadowBaseLocalPosition;
        private Vector3 deathShadowBaseLocalScale;
        private bool deathShadowBaseCached;
        private int deathShadowStage;

        protected override GameObject BossMuzzleFlashVfxPrefab => EffectData != null
            ? EffectData.MuscleMuzzleFlashVfxPrefab
            : null;
        protected override bool RotatesBodyToPlayer => false;

        protected override void OnHpEmptyBegan()
        {
            // 이전 사이클에서 EndStun이 Stun 스테이트 진입 전에 소모되지 못하고 남아있으면,
            // 이번에 Stun 스테이트에 들어가자마자 그 묵은 트리거에 바로 되튕겨 나가버린다.
            ResetAnimatorTrigger(EndStunParameter);
            SetAnimatorTrigger(StunParameter);
        }

        protected override void OnHpEmptyRecovered()
        {
            ResetAnimatorTrigger(StunParameter);
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
            ResetDeathShadow();
            base.OnDisable();
        }

        private void LateUpdate()
        {
            UpdateFacingSprite();
            UpdateWalkState();
            UpdateDeathShadow();
        }

        private void UpdateFacingSprite()
        {
            SpriteRenderer spriteRenderer = ResolveFacingSpriteRenderer();
            if (spriteRenderer == null || Player == null || GraphContext?.IsFacingLocked == true)
            {
                return;
            }

            ApplyFacingSprite(spriteRenderer, Player.position.x > transform.position.x);
        }

        protected override void ApplyExecutionFacing(Vector2 worldPosition)
        {
            SpriteRenderer spriteRenderer = ResolveFacingSpriteRenderer();
            if (spriteRenderer != null)
            {
                ApplyFacingSprite(spriteRenderer, worldPosition.x > transform.position.x);
            }
        }

        private void ApplyFacingSprite(SpriteRenderer spriteRenderer, bool flip)
        {
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

        private void UpdateDeathShadow()
        {
            Transform targetShadow = ResolveDeathShadow();
            if (targetShadow == null)
            {
                return;
            }

            CacheDeathShadowBase(targetShadow);

            SpriteRenderer frameSource = ResolveDeathShadowFrameSource();
            Sprite currentSprite = frameSource != null ? frameSource.sprite : null;
            string currentFrameName = currentSprite != null ? currentSprite.name : null;
            bool isDeathAnimationPlaying = IsDeathShadowAnimationPlaying(out float deathAnimationTime);
            int detectedStage = Mathf.Max(
                GetDeathShadowFrameStage(currentFrameName),
                GetDeathShadowAnimationStage(deathAnimationTime, isDeathAnimationPlaying));
            if (detectedStage > deathShadowStage)
            {
                deathShadowStage = detectedStage;
            }

            bool isDeathFrame = IsDeathShadowFrame(currentFrameName);
            if (deathShadowStage > 0 && (isDeathFrame || isDeathAnimationPlaying))
            {
                ApplyDeathShadowStage(targetShadow, deathShadowStage);
                return;
            }

            if (!isDeathFrame && !isDeathAnimationPlaying)
            {
                ResetDeathShadow(targetShadow);
            }
        }

        private Transform ResolveDeathShadow()
        {
            if (deathShadow != null)
            {
                return deathShadow;
            }

            Transform visualRoot = BodyRoot != null ? BodyRoot : transform.Find("Boss-Muscle Visual");
            if (visualRoot == null)
            {
                return null;
            }

            deathShadow = visualRoot.Find("Shadow");
            return deathShadow;
        }

        private SpriteRenderer ResolveDeathShadowFrameSource()
        {
            if (deathShadowFrameSource != null)
            {
                return deathShadowFrameSource;
            }

            deathShadowFrameSource = ResolveFacingSpriteRenderer();
            return deathShadowFrameSource;
        }

        private Animator ResolveDeathShadowAnimator()
        {
            if (deathShadowAnimator != null)
            {
                return deathShadowAnimator;
            }

            if (deathShadowFrameSource != null)
            {
                deathShadowAnimator = deathShadowFrameSource.GetComponent<Animator>();
                if (deathShadowAnimator == null)
                {
                    deathShadowAnimator = deathShadowFrameSource.GetComponentInParent<Animator>();
                }
            }

            if (deathShadowAnimator == null)
            {
                deathShadowAnimator = ResolveWalkAnimator();
            }

            return deathShadowAnimator;
        }

        private void CacheDeathShadowBase(Transform targetShadow)
        {
            if (deathShadowBaseCached)
            {
                return;
            }

            deathShadowBaseLocalPosition = targetShadow.localPosition;
            deathShadowBaseLocalScale = targetShadow.localScale;
            deathShadowBaseCached = true;
        }

        private int GetDeathShadowFrameStage(string frameName)
        {
            if (string.IsNullOrEmpty(frameName))
            {
                return 0;
            }

            if (!string.IsNullOrEmpty(deathShadowStageTwoFrameName) && frameName == deathShadowStageTwoFrameName)
            {
                return 2;
            }

            if (!string.IsNullOrEmpty(deathShadowStageOneFrameName) && frameName == deathShadowStageOneFrameName)
            {
                return 1;
            }

            return 0;
        }

        private bool IsDeathShadowFrame(string frameName)
        {
            return !string.IsNullOrEmpty(frameName)
                && !string.IsNullOrEmpty(deathShadowFramePrefix)
                && frameName.StartsWith(deathShadowFramePrefix);
        }

        private bool IsDeathShadowAnimationPlaying(out float animationTime)
        {
            animationTime = 0f;

            Animator animator = ResolveDeathShadowAnimator();
            if (animator == null || !animator.isActiveAndEnabled)
            {
                return false;
            }

            if (animator.IsInTransition(0))
            {
                AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(0);
                if (IsDeathShadowAnimationState(nextState))
                {
                    animationTime = GetDeathShadowAnimationTime(nextState);
                    return true;
                }
            }

            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
            if (!IsDeathShadowAnimationState(currentState))
            {
                return false;
            }

            animationTime = GetDeathShadowAnimationTime(currentState);
            return true;
        }

        private bool IsDeathShadowAnimationState(AnimatorStateInfo state)
        {
            return !string.IsNullOrEmpty(deathShadowAnimationStateName)
                && (state.shortNameHash == Animator.StringToHash(deathShadowAnimationStateName)
                    || state.IsName(deathShadowAnimationStateName));
        }

        private static float GetDeathShadowAnimationTime(AnimatorStateInfo state)
        {
            float normalizedTime = state.loop
                ? Mathf.Repeat(state.normalizedTime, 1f)
                : Mathf.Clamp01(state.normalizedTime);
            return Mathf.Max(0f, state.length) * normalizedTime;
        }

        private int GetDeathShadowAnimationStage(float animationTime, bool isDeathAnimationPlaying)
        {
            if (!isDeathAnimationPlaying)
            {
                return 0;
            }

            if (animationTime >= deathShadowStageTwoAnimationTime)
            {
                return 2;
            }

            if (animationTime >= deathShadowStageOneAnimationTime)
            {
                return 1;
            }

            return 0;
        }

        private void ApplyDeathShadowStage(Transform targetShadow, int stage)
        {
            float targetLocalX = stage >= 2
                ? deathShadowStageTwoLocalX
                : deathShadowStageOneLocalX;
            SpriteRenderer facingSource = ResolveFacingSpriteRenderer();
            if (mirrorDeathShadowLocalXByFacing && facingSource != null && facingSource.flipX)
            {
                targetLocalX = -targetLocalX;
            }

            float targetScaleX = stage >= 2
                ? deathShadowStageTwoScaleX
                : deathShadowStageOneScaleX;

            Vector3 nextPosition = deathShadowBaseLocalPosition;
            nextPosition.x = targetLocalX;
            targetShadow.localPosition = nextPosition;

            Vector3 nextScale = deathShadowBaseLocalScale;
            nextScale.x = targetScaleX;
            targetShadow.localScale = nextScale;
        }

        private void ResetDeathShadow()
        {
            Transform targetShadow = ResolveDeathShadow();
            if (targetShadow != null)
            {
                ResetDeathShadow(targetShadow);
            }
        }

        private void ResetDeathShadow(Transform targetShadow)
        {
            if (deathShadowBaseCached)
            {
                targetShadow.localPosition = deathShadowBaseLocalPosition;
                targetShadow.localScale = deathShadowBaseLocalScale;
            }

            deathShadowStage = 0;
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

        private void ResetAnimatorTrigger(int parameter)
        {
            Animator targetAnimator = ResolveWalkAnimator();
            if (targetAnimator == null)
            {
                return;
            }

            targetAnimator.ResetTrigger(parameter);
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
