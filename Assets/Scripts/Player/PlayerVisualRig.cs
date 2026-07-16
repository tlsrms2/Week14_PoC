using System.Collections;
using UnityEngine;
using Week14.Input;
using Week14.Weapons;

namespace Week14.Combat
{
    [DisallowMultipleComponent]
    public sealed class PlayerVisualRig : MonoBehaviour
    {
        private const int BaseLayerIndex = 0;
        private const string BaseLayerPrefix = "Base Layer.";
        private const string FrontBodyIdleState = "Front_Body_Idle";
        private const string FrontBodyWalkState = "Front_Body_Walk";
        private const string FrontRightArmIdleState = "Front_Arm_R_Idle";
        private const string FrontRightArmWalkState = "Front_Arm_R_Walk";
        private const string FrontRightArmHolsteringState = "Front_Arm_R_Holstering";
        private const string SideBodyIdleState = "Side_Body";
        private const string SideBodyWalkState = "Side_Body_Walk";
        private const string SideRightArmIdleState = "Side_Arm_R";
        private const string SideRightArmWalkState = "Side_Arm_R_Walk";
        private const string SideRightArmHolsteringState = "Side_Arm_R_Holstering";
        private const string BackBodyIdleState = "Back_Body_Idle";
        private const string BackBodyWalkState = "Back_Body_Walk";
        private const string BackRightArmIdleState = "Back_Arm_R_Idle";
        private const string BackRightArmWalkState = "Back_Arm_R_Walk";
        private const string BackRightArmHolsteringState = "Back_Arm_R_Holstering";

        private static readonly int IsWalkParameter = Animator.StringToHash("isWalk");
        private static readonly int DoInterceptParameter = Animator.StringToHash("doIntercept");
        private static readonly int DoShotParameter = Animator.StringToHash("doShot");
        private static readonly int DoReloadParameter = Animator.StringToHash("doReload");
        private static readonly int DoRollParameter = Animator.StringToHash("doRoll");

        private enum VisualFacing
        {
            Front,
            Side,
            Back
        }

        [Header("Visual Roots")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform frontVisualRoot;
        [SerializeField] private Transform sideVisualRoot;
        [SerializeField] private Transform backVisualRoot;

        [Header("Death")]
        [Tooltip("사망 시 visualRoot를 끄고 대신 켜줄 사망용 비주얼 루트입니다.")]
        [SerializeField] private GameObject deathVisualRoot;

        [Header("Left Arm Aim")]
        [SerializeField] private Transform leftArm;
        [Tooltip("왼팔 애니메이터입니다. 비워두면 leftArm(또는 \"Arm_L\" 이름의 자식)에서 자동으로 찾습니다.")]
        [SerializeField] private Animator leftArmAnimator;
        [SerializeField] private Vector2 leftArmAimAngleLimits = new Vector2(-105f, 75f);
        [SerializeField] private float leftArmAimAngleOffset;
        [SerializeField, Min(0f)] private float leftArmAimRotateSpeed;

        [Header("Facing")]
        [SerializeField, Range(0f, 180f)] private float frontMaxAngleFromDown = 50f;
        [SerializeField, Range(0f, 180f)] private float backMaxAngleFromUp = 50f;
        [SerializeField, Min(0f)] private float flipDeadZone = 0.05f;

        private PlayerCombatController combat;
        private SpriteRenderer[] frontRenderers;
        private SpriteRenderer[] sideRenderers;
        private SpriteRenderer[] backRenderers;
        private Animator[] partAnimators;
        private RuntimeAnimatorController defaultLeftArmController;
        private Animator frontRightArmAnimator;
        private Animator sideRightArmAnimator;
        private Animator backRightArmAnimator;
        private Animator frontBodyAnimator;
        private Animator sideBodyAnimator;
        private Animator backBodyAnimator;
        private float[] frontVisibleAlphas;
        private float[] sideVisibleAlphas;
        private float[] backVisibleAlphas;
        private VisualFacing currentFacing = VisualFacing.Front;
        private bool hasAppliedFacing;
        private bool hasAppliedWalkState;
        private bool lastIsWalking;
        private bool frontRightArmWasHolstering;
        private bool sideRightArmWasHolstering;
        private bool backRightArmWasHolstering;
        private Coroutine rollSpinRoutine;
        private bool cinematicMovementActive;
        private bool executionVisualActive;
        private VisualFacing facingBeforeExecution;
        private VisualFacing executionFacing;
        private bool leftArmWasActiveBeforeExecution;
        private Animator executionRightArmAnimator;
        private string executionRightArmIdleState;
        private string executionRightArmHolsteringState;
        private bool executionRightArmWasActive;
        private float executionRightArmSpeedBeforeExecution = 1f;

        private void Awake()
        {
            ResolveReferences();
            CacheRenderers();
            CachePartAnimators();

            if (deathVisualRoot != null)
            {
                deathVisualRoot.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (WeaponLoadoutManager.Instance != null)
            {
                WeaponLoadoutManager.Instance.WeaponChanged += HandleWeaponChanged;
            }
        }

        private void OnDisable()
        {
            if (WeaponLoadoutManager.Instance != null)
            {
                WeaponLoadoutManager.Instance.WeaponChanged -= HandleWeaponChanged;
            }
        }

        private void Start()
        {
            ApplyFacing(currentFacing);
            UpdateWalkAnimation(true);
            ApplyLeftArmController(WeaponLoadoutManager.Instance != null ? WeaponLoadoutManager.Instance.CurrentWeapon : null);
        }

        private void LateUpdate()
        {
            UpdateWalkAnimation(false);
            if (!executionVisualActive)
            {
                SyncRightArmAfterHolstering();
            }
        }

        public bool IsFacingLeft => visualRoot != null && visualRoot.localScale.x < 0f;

        public void BeginCinematicMovement(Vector2 direction)
        {
            cinematicMovementActive = direction.sqrMagnitude > 0.0001f;
            if (cinematicMovementActive)
            {
                SetBodyAimDirection(direction.normalized);
            }

            UpdateWalkAnimation(true);
        }

        public void EndCinematicMovement(Vector2 facingDirection)
        {
            cinematicMovementActive = false;
            if (facingDirection.sqrMagnitude > 0.0001f)
            {
                SetBodyAimDirection(facingDirection);
            }

            UpdateWalkAnimation(true);
        }

        public void SetBodyAimDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                ApplyFacing(currentFacing);
                return;
            }

            UpdateVisualRootFlip(direction.x);

            float angleFromDown = Vector2.Angle(Vector2.down, direction);
            ApplyFacing(executionVisualActive ? executionFacing : GetFacingFromAngle(angleFromDown));
        }

        public void SetLeftArmAimDirection(Vector2 direction)
        {
            if (leftArm == null || direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Transform aimSpace = leftArm.parent != null ? leftArm.parent : visualRoot;
            if (aimSpace == null)
            {
                return;
            }

            Vector2 localDirection = aimSpace.InverseTransformVector(direction);
            if (localDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float targetAngle = Mathf.Atan2(localDirection.y, localDirection.x) * Mathf.Rad2Deg;
            float clampedAngle = ClampLeftArmAngle(targetAngle) + leftArmAimAngleOffset;
            Quaternion targetRotation = Quaternion.Euler(0f, 0f, clampedAngle);

            leftArm.localRotation = leftArmAimRotateSpeed > 0f
                ? Quaternion.RotateTowards(leftArm.localRotation, targetRotation, leftArmAimRotateSpeed * Time.deltaTime)
                : targetRotation;
        }

        public void PlayShot()
        {
            SetTriggerIfExists(leftArmAnimator, DoShotParameter);
        }

        public void PlayDeath()
        {
            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(false);
            }

            if (deathVisualRoot != null)
            {
                deathVisualRoot.SetActive(true);

                Animator deathAnimator = deathVisualRoot.GetComponent<Animator>();
                if (deathAnimator != null)
                {
                    // 사망 대기 중 Time.timeScale이 0이 되어도 애니메이션이 끝까지 재생되도록 합니다.
                    deathAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
                }
            }
        }

        public void RestoreAfterDeath()
        {
            if (deathVisualRoot != null)
            {
                deathVisualRoot.SetActive(false);
            }

            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(true);
            }

            ApplyFacing(currentFacing);
            UpdateWalkAnimation(true);
        }

        private void HandleWeaponChanged(BaseWeaponSO weapon)
        {
            ApplyLeftArmController(weapon);
        }

        private void ApplyLeftArmController(BaseWeaponSO weapon)
        {
            if (leftArmAnimator == null)
            {
                return;
            }

            RuntimeAnimatorController controller = weapon != null && weapon.LeftArmController != null
                ? weapon.LeftArmController
                : defaultLeftArmController;

            if (leftArmAnimator.runtimeAnimatorController != controller)
            {
                leftArmAnimator.runtimeAnimatorController = controller;
            }
        }

        public void PlayIntercept()
        {
            SetTriggerIfExists(frontRightArmAnimator, DoInterceptParameter);
            SetTriggerIfExists(sideRightArmAnimator, DoInterceptParameter);
            SetTriggerIfExists(backRightArmAnimator, DoInterceptParameter);
        }

        public void PlayReload()
        {
            SetTriggerIfExists(frontRightArmAnimator, DoReloadParameter);
            SetTriggerIfExists(sideRightArmAnimator, DoReloadParameter);
            SetTriggerIfExists(backRightArmAnimator, DoReloadParameter);
        }

        public void SetLeftArmVisible(bool visible)
        {
            if (leftArm != null && leftArm.gameObject.activeSelf != visible)
            {
                leftArm.gameObject.SetActive(visible);
            }
        }

        public void BeginExecutionVisual(Vector2 facingDirection)
        {
            if (!executionVisualActive)
            {
                facingBeforeExecution = currentFacing;
                leftArmWasActiveBeforeExecution = leftArm != null && leftArm.gameObject.activeSelf;
                executionFacing = GetFacingFromDirection(facingDirection, currentFacing);
                ResolveExecutionRightArm(executionFacing);
                executionRightArmWasActive = executionRightArmAnimator != null
                    && executionRightArmAnimator.gameObject.activeSelf;
                executionRightArmSpeedBeforeExecution = executionRightArmAnimator != null
                    ? executionRightArmAnimator.speed
                    : 1f;
            }

            executionVisualActive = true;
            if (facingDirection.sqrMagnitude > 0.0001f)
            {
                UpdateVisualRootFlip(facingDirection.x);
            }

            SetLeftArmVisible(false);
            if (executionRightArmAnimator != null)
            {
                executionRightArmAnimator.gameObject.SetActive(true);
                executionRightArmAnimator.speed = 0f;
            }

            hasAppliedFacing = false;
            ApplyFacing(executionFacing);
            SampleExecutionRightArmHolstering(1f);
        }

        public IEnumerator PlayExecutionRightArmHolstering(float duration, bool reverse)
        {
            if (!executionVisualActive || executionRightArmAnimator == null)
            {
                yield break;
            }

            float animationDuration = Mathf.Max(0f, duration);
            if (animationDuration <= 0f)
            {
                SampleExecutionRightArmHolstering(reverse ? 0f : 1f);
                yield break;
            }

            for (float elapsed = 0f; elapsed < animationDuration; elapsed += Time.deltaTime)
            {
                float progress = elapsed / animationDuration;
                SampleExecutionRightArmHolstering(reverse ? 1f - progress : progress);
                yield return null;
            }

            SampleExecutionRightArmHolstering(reverse ? 0f : 1f);
        }

        public void EndExecutionVisual()
        {
            if (!executionVisualActive)
            {
                return;
            }

            executionVisualActive = false;
            if (executionRightArmAnimator != null)
            {
                executionRightArmAnimator.speed = executionRightArmSpeedBeforeExecution;
                executionRightArmAnimator.Play(BaseLayerPrefix + executionRightArmIdleState, BaseLayerIndex, 0f);
                executionRightArmAnimator.Update(0f);
                executionRightArmAnimator.gameObject.SetActive(executionRightArmWasActive);
            }

            SetLeftArmVisible(leftArmWasActiveBeforeExecution);
            executionRightArmAnimator = null;
            executionRightArmIdleState = null;
            executionRightArmHolsteringState = null;
            hasAppliedFacing = false;
            ApplyFacing(facingBeforeExecution);
            UpdateWalkAnimation(true);
        }

        public void PlayRoll(float duration)
        {
            SetTriggerIfExists(frontBodyAnimator, DoRollParameter);
            SetTriggerIfExists(sideBodyAnimator, DoRollParameter);
            SetTriggerIfExists(backBodyAnimator, DoRollParameter);

            if (visualRoot != null && duration > 0f)
            {
                if (rollSpinRoutine != null)
                {
                    StopCoroutine(rollSpinRoutine);
                }

                rollSpinRoutine = StartCoroutine(RollSpinRoutine(duration));
            }
        }

        private IEnumerator RollSpinRoutine(float duration)
        {
            // visualRoot의 localScale.x 부호로 좌우 반전을 표현하므로, 그 부호에 맞춰 회전 방향을 정해야
            // 구르는 방향과 반대로 도는 것처럼 보이지 않는다.
            float spinDirection = visualRoot.localScale.x < 0f ? 1f : -1f;
            Quaternion startRotation = visualRoot.localRotation;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float angle = spinDirection * 360f * Mathf.Clamp01(elapsed / duration);
                visualRoot.localRotation = startRotation * Quaternion.Euler(0f, 0f, angle);
                yield return null;
            }

            visualRoot.localRotation = startRotation;
            rollSpinRoutine = null;
        }

        private static void SetTriggerIfExists(Animator animator, int parameterHash)
        {
            if (animator == null || !HasParameter(animator, parameterHash, AnimatorControllerParameterType.Trigger))
            {
                return;
            }

            animator.SetTrigger(parameterHash);
        }

        private void SampleExecutionRightArmHolstering(float normalizedTime)
        {
            if (!executionVisualActive || executionRightArmAnimator == null)
            {
                return;
            }

            executionRightArmAnimator.speed = 0f;
            executionRightArmAnimator.Play(
                BaseLayerPrefix + executionRightArmHolsteringState,
                BaseLayerIndex,
                Mathf.Clamp01(normalizedTime));
            executionRightArmAnimator.Update(0f);
        }

        private static bool HasParameter(
            Animator animator,
            int parameterHash,
            AnimatorControllerParameterType parameterType)
        {
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter.nameHash == parameterHash && parameter.type == parameterType)
                {
                    return true;
                }
            }

            return false;
        }

        private void ResolveReferences()
        {
            if (combat == null)
            {
                combat = GetComponentInParent<PlayerCombatController>();
            }

            if (visualRoot == null)
            {
                visualRoot = FindDescendant(transform, "VisualRoot");
            }

            Transform root = visualRoot != null ? visualRoot : transform;

            if (frontVisualRoot == null)
            {
                frontVisualRoot = FindDescendant(root, "Front VisualRoot");
            }

            if (sideVisualRoot == null)
            {
                sideVisualRoot = FindDescendant(root, "Side VisualRoot");
            }

            if (backVisualRoot == null)
            {
                backVisualRoot = FindDescendant(root, "Back VisualRoot");
            }

            if (leftArm == null)
            {
                leftArm = FindDescendant(root, "Arm_L");
            }
        }

        private void CacheRenderers()
        {
            frontRenderers = GetChildRenderers(frontVisualRoot);
            sideRenderers = GetChildRenderers(sideVisualRoot);
            backRenderers = GetChildRenderers(backVisualRoot);
            frontVisibleAlphas = CacheVisibleAlphas(frontRenderers);
            sideVisibleAlphas = CacheVisibleAlphas(sideRenderers);
            backVisibleAlphas = CacheVisibleAlphas(backRenderers);
        }

        private void CachePartAnimators()
        {
            Animator[] frontAnimators = GetChildAnimators(frontVisualRoot);
            Animator[] sideAnimators = GetChildAnimators(sideVisualRoot);
            Animator[] backAnimators = GetChildAnimators(backVisualRoot);

            partAnimators = new Animator[frontAnimators.Length + sideAnimators.Length + backAnimators.Length];
            System.Array.Copy(frontAnimators, 0, partAnimators, 0, frontAnimators.Length);
            System.Array.Copy(sideAnimators, 0, partAnimators, frontAnimators.Length, sideAnimators.Length);
            System.Array.Copy(backAnimators, 0, partAnimators, frontAnimators.Length + sideAnimators.Length, backAnimators.Length);

            if (leftArmAnimator == null)
            {
                leftArmAnimator = leftArm != null ? leftArm.GetComponent<Animator>() : null;
            }

            defaultLeftArmController = leftArmAnimator != null ? leftArmAnimator.runtimeAnimatorController : null;
            frontBodyAnimator = GetNamedChildAnimator(frontVisualRoot, "Front_Body");
            sideBodyAnimator = GetNamedChildAnimator(sideVisualRoot, "Side_Body");
            backBodyAnimator = GetNamedChildAnimator(backVisualRoot, "Back_Body");
            frontRightArmAnimator = GetNamedChildAnimator(frontVisualRoot, "Front_Arm_R");
            sideRightArmAnimator = GetNamedChildAnimator(sideVisualRoot, "Side_Arm_R");
            backRightArmAnimator = GetNamedChildAnimator(backVisualRoot, "Back_Arm_R");
        }

        private void UpdateVisualRootFlip(float aimDirectionX)
        {
            if (visualRoot == null || Mathf.Abs(aimDirectionX) <= flipDeadZone)
            {
                return;
            }

            Vector3 scale = visualRoot.localScale;
            float xMagnitude = Mathf.Abs(scale.x);
            scale.x = aimDirectionX < 0f ? -xMagnitude : xMagnitude;
            visualRoot.localScale = scale;
        }

        private VisualFacing GetFacingFromAngle(float angleFromDown)
        {
            if (angleFromDown <= frontMaxAngleFromDown)
            {
                return VisualFacing.Front;
            }

            if (angleFromDown >= 180f - backMaxAngleFromUp)
            {
                return VisualFacing.Back;
            }

            return VisualFacing.Side;
        }

        private VisualFacing GetFacingFromDirection(Vector2 direction, VisualFacing fallback)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return fallback;
            }

            float angleFromDown = Vector2.Angle(Vector2.down, direction);
            return GetFacingFromAngle(angleFromDown);
        }

        private void ResolveExecutionRightArm(VisualFacing facing)
        {
            switch (facing)
            {
                case VisualFacing.Front:
                    executionRightArmAnimator = frontRightArmAnimator;
                    executionRightArmIdleState = FrontRightArmIdleState;
                    executionRightArmHolsteringState = FrontRightArmHolsteringState;
                    break;

                case VisualFacing.Back:
                    executionRightArmAnimator = backRightArmAnimator;
                    executionRightArmIdleState = BackRightArmIdleState;
                    executionRightArmHolsteringState = BackRightArmHolsteringState;
                    break;

                default:
                    executionRightArmAnimator = sideRightArmAnimator;
                    executionRightArmIdleState = SideRightArmIdleState;
                    executionRightArmHolsteringState = SideRightArmHolsteringState;
                    break;
            }
        }

        private void UpdateWalkAnimation(bool force)
        {
            bool canWalk = combat == null || combat.CanMove;
            bool isWalking = cinematicMovementActive
                || (canWalk && GameInput.Move.sqrMagnitude > 0.0001f);
            if (!force && hasAppliedWalkState && lastIsWalking == isWalking)
            {
                return;
            }

            for (int i = 0; i < partAnimators.Length; i++)
            {
                partAnimators[i].SetBool(IsWalkParameter, isWalking);
            }

            lastIsWalking = isWalking;
            hasAppliedWalkState = true;
        }

        private void SyncRightArmAfterHolstering()
        {
            SyncRightArmAfterHolstering(
                frontBodyAnimator,
                frontRightArmAnimator,
                ref frontRightArmWasHolstering,
                FrontBodyIdleState,
                FrontBodyWalkState,
                FrontRightArmIdleState,
                FrontRightArmWalkState,
                FrontRightArmHolsteringState);

            SyncRightArmAfterHolstering(
                sideBodyAnimator,
                sideRightArmAnimator,
                ref sideRightArmWasHolstering,
                SideBodyIdleState,
                SideBodyWalkState,
                SideRightArmIdleState,
                SideRightArmWalkState,
                SideRightArmHolsteringState);

            SyncRightArmAfterHolstering(
                backBodyAnimator,
                backRightArmAnimator,
                ref backRightArmWasHolstering,
                BackBodyIdleState,
                BackBodyWalkState,
                BackRightArmIdleState,
                BackRightArmWalkState,
                BackRightArmHolsteringState);
        }

        private static void SyncRightArmAfterHolstering(
            Animator bodyAnimator,
            Animator armAnimator,
            ref bool wasHolstering,
            string bodyIdleState,
            string bodyWalkState,
            string armIdleState,
            string armWalkState,
            string armHolsteringState)
        {
            if (bodyAnimator == null || armAnimator == null)
            {
                wasHolstering = false;
                return;
            }

            AnimatorStateInfo armState = armAnimator.GetCurrentAnimatorStateInfo(BaseLayerIndex);
            bool armIsHolstering = IsState(armState, armHolsteringState);
            if (armAnimator.IsInTransition(BaseLayerIndex))
            {
                AnimatorStateInfo nextArmState = armAnimator.GetNextAnimatorStateInfo(BaseLayerIndex);
                armIsHolstering |= IsState(nextArmState, armHolsteringState);
            }

            if (armIsHolstering)
            {
                wasHolstering = true;
                return;
            }

            if (!wasHolstering)
            {
                return;
            }

            if (IsState(armState, armWalkState))
            {
                PlayArmStateAtBodyTime(bodyAnimator, armAnimator, bodyWalkState, armWalkState);
                wasHolstering = false;
                return;
            }

            if (IsState(armState, armIdleState))
            {
                PlayArmStateAtBodyTime(bodyAnimator, armAnimator, bodyIdleState, armIdleState);
                wasHolstering = false;
            }
        }

        private static void PlayArmStateAtBodyTime(
            Animator bodyAnimator,
            Animator armAnimator,
            string bodyState,
            string armState)
        {
            float bodyNormalizedTime = GetAnimatorStateTime(bodyAnimator, bodyState);
            armAnimator.Play(BaseLayerPrefix + armState, BaseLayerIndex, bodyNormalizedTime);
            armAnimator.Update(0f);
        }

        private static float GetAnimatorStateTime(Animator animator, string stateName)
        {
            if (animator.IsInTransition(BaseLayerIndex))
            {
                AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(BaseLayerIndex);
                if (IsState(nextState, stateName))
                {
                    return Mathf.Repeat(nextState.normalizedTime, 1f);
                }
            }

            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(BaseLayerIndex);
            return Mathf.Repeat(currentState.normalizedTime, 1f);
        }

        private static bool IsState(AnimatorStateInfo stateInfo, string stateName)
        {
            return stateInfo.shortNameHash == Animator.StringToHash(stateName);
        }

        private float ClampLeftArmAngle(float angle)
        {
            float minAngle = Mathf.Min(leftArmAimAngleLimits.x, leftArmAimAngleLimits.y);
            float maxAngle = Mathf.Max(leftArmAimAngleLimits.x, leftArmAimAngleLimits.y);
            return Mathf.Clamp(Mathf.DeltaAngle(0f, angle), minAngle, maxAngle);
        }

        private void ApplyFacing(VisualFacing nextFacing)
        {
            if (hasAppliedFacing && currentFacing == nextFacing)
            {
                return;
            }

            SetRendererAlphas(frontRenderers, frontVisibleAlphas, nextFacing == VisualFacing.Front);
            SetRendererAlphas(sideRenderers, sideVisibleAlphas, nextFacing == VisualFacing.Side);
            SetRendererAlphas(backRenderers, backVisibleAlphas, nextFacing == VisualFacing.Back);

            currentFacing = nextFacing;
            hasAppliedFacing = true;
        }

        private static SpriteRenderer[] GetChildRenderers(Transform root)
        {
            return root != null
                ? root.GetComponentsInChildren<SpriteRenderer>(true)
                : System.Array.Empty<SpriteRenderer>();
        }

        private static Animator[] GetChildAnimators(Transform root)
        {
            return root != null
                ? root.GetComponentsInChildren<Animator>(true)
                : System.Array.Empty<Animator>();
        }

        private static Animator GetNamedChildAnimator(Transform root, string childName)
        {
            Transform child = root != null ? FindDescendant(root, childName) : null;
            return child != null ? child.GetComponent<Animator>() : null;
        }

        private static float[] CacheVisibleAlphas(SpriteRenderer[] renderers)
        {
            float[] visibleAlphas = new float[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                visibleAlphas[i] = renderers[i].color.a;
            }

            return visibleAlphas;
        }

        private static void SetRendererAlphas(SpriteRenderer[] renderers, float[] visibleAlphas, bool isVisible)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer spriteRenderer = renderers[i];
                Color color = spriteRenderer.color;
                color.a = isVisible ? visibleAlphas[i] : 0f;
                spriteRenderer.color = color;
            }
        }

        private static Transform FindDescendant(Transform root, string childName)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
