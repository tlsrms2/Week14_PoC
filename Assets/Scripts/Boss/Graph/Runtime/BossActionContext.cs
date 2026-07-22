using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Audio;
using Week14.Bootstrap;
using Week14.Combat;

namespace Week14.Enemy
{
    public sealed class BossActionContext
    {
        private const float OvershootGuardSeconds = 0.12f;

        private readonly Action stop;
        private readonly Func<bool> isExecutionPaused;
        private Animator[] animators;
        private BossAnimationEventBridge animationEventBridge;
        private bool hasBodyRootLocalBase;
        private Vector3 bodyRootLocalBase;
        private bool hasPlayerRelativeMoveIntent;
        private float playerRelativeMoveDirectionSign = 1f;
        private float playerRelativeMoveSpeedMultiplier = 1f;
        private float playerRelativeMoveElapsedSeconds;
        private float playerRelativeMoveDurationSeconds;
        private AnimationCurve playerRelativeMoveSpeedCurve;
        private readonly Dictionary<Transform, Vector3> transformBaseScales = new();
        private readonly Dictionary<string, BossChildAimState> bossChildAimStates = new();
        private readonly Dictionary<string, string> bossChildAimStartNodePaths = new();
        private readonly Dictionary<string, EnemyProjectile> projectileHandles = new();
        private readonly List<GameObject> transientVisuals = new();
        private readonly HashSet<object> facingLockOwners = new();
        private string currentNodeId;
        private int activeNodeExecutionCount;
        private int nodeExecutionVersion;
        private int conductorMinionOutlineHoldRequests;
        private int activeSnipingTelegraphCount;
        private int parallelPatternGroupDepth;
        private bool isFacingLocked;
        private bool isMeleeAdvanceSynchronized;
        private bool hasMeleeAttackAdvanceCompleted;
        private bool isPatternTerminationRequested;

        public BossActionContext(
            BossAI boss,
            Action stop,
            Func<bool> isExecutionPaused,
            BossGraphAsset graphAsset = null,
            bool skipApproachMovement = false)
        {
            Boss = boss;
            this.stop = stop;
            this.isExecutionPaused = isExecutionPaused;
            GraphAsset = graphAsset;
            SkipApproachMovement = skipApproachMovement;
        }

        public BossAI Boss { get; }
        public BossGraphAsset GraphAsset { get; }
        public bool SkipApproachMovement { get; }
        public string CurrentNodeId => currentNodeId;
        public bool IsNodeActionExecuting => activeNodeExecutionCount > 0;
        public int NodeExecutionVersion => nodeExecutionVersion;
        public bool IsExecutionPaused => isExecutionPaused?.Invoke() == true;
        public bool IsDashing { get; private set; }
        public bool IsFacingLocked => isFacingLocked || facingLockOwners.Count > 0;
        public bool IsMeleeAdvanceSynchronized => isMeleeAdvanceSynchronized;
        public bool HasMeleeAttackAdvanceCompleted => hasMeleeAttackAdvanceCompleted;
        public bool IsPatternTerminationRequested => isPatternTerminationRequested;
        public bool IsExecutingParallelPatternGroup => parallelPatternGroupDepth > 0;

        internal void BeginParallelPatternGroup()
        {
            parallelPatternGroupDepth++;
        }

        internal void EndParallelPatternGroup()
        {
            parallelPatternGroupDepth = Mathf.Max(0, parallelPatternGroupDepth - 1);
        }

        public void RequestPatternTermination()
        {
            isPatternTerminationRequested = true;
        }

        public void ClearPatternTerminationRequest()
        {
            isPatternTerminationRequested = false;
        }

        public void SetDashing(bool dashing)
        {
            IsDashing = dashing;
            if (!dashing)
            {
                Boss?.SetAutomaticDashContactDamageSuppressed(false);
            }

            Boss?.SetIgnorePlayerCollision(dashing);
        }

        public void SetAutomaticDashContactDamageSuppressed(bool suppressed)
        {
            Boss?.SetAutomaticDashContactDamageSuppressed(suppressed);
        }

        public void SetFacingLocked(bool locked)
        {
            isFacingLocked = locked;
        }

        public void SetFacingLocked(object owner, bool locked)
        {
            if (owner == null)
            {
                return;
            }

            if (locked)
            {
                facingLockOwners.Add(owner);
            }
            else
            {
                facingLockOwners.Remove(owner);
            }
        }

        public IDisposable AcquireFacingLock()
        {
            object owner = new();
            SetFacingLocked(owner, true);
            return new FacingLockLease(this, owner);
        }

        public Vector3 OriginPosition
        {
            get
            {
                if (Boss == null)
                {
                    return Vector3.zero;
                }

                return Boss.BodyRoot != null ? Boss.BodyRoot.position : Boss.transform.position;
            }
        }

        private sealed class FacingLockLease : IDisposable
        {
            private BossActionContext context;
            private object owner;

            internal FacingLockLease(BossActionContext context, object owner)
            {
                this.context = context;
                this.owner = owner;
            }

            public void Dispose()
            {
                context?.SetFacingLocked(owner, false);
                context = null;
                owner = null;
            }
        }

        public void Stop()
        {
            UpdateBossChildAims();
            stop?.Invoke();
        }

        public void SetCurrentNodeId(string nodeId)
        {
            currentNodeId = nodeId;
        }

        public void BeginNodeExecution(string nodeId)
        {
            activeNodeExecutionCount++;
            nodeExecutionVersion++;
            currentNodeId = nodeId;
        }

        public void EndNodeExecution()
        {
            activeNodeExecutionCount = Mathf.Max(0, activeNodeExecutionCount - 1);
            if (activeNodeExecutionCount == 0)
            {
                currentNodeId = null;
            }

        }

        public void BeginMeleeAdvanceSynchronization()
        {
            isMeleeAdvanceSynchronized = true;
            hasMeleeAttackAdvanceCompleted = false;
        }

        public void NotifyMeleeAttackAdvanceCompleted()
        {
            if (isMeleeAdvanceSynchronized)
            {
                hasMeleeAttackAdvanceCompleted = true;
            }
        }

        public void EndMeleeAdvanceSynchronization()
        {
            isMeleeAdvanceSynchronized = false;
            hasMeleeAttackAdvanceCompleted = false;
        }

        public void RegisterConductorMinionOutlineHold()
        {
            conductorMinionOutlineHoldRequests++;
        }

        public int ConsumeConductorMinionOutlineHoldRequests()
        {
            int count = conductorMinionOutlineHoldRequests;
            conductorMinionOutlineHoldRequests = 0;
            return count;
        }

        public void ClearConductorMinionOutlineHoldRequests()
        {
            conductorMinionOutlineHoldRequests = 0;
        }

        public void PlayAnimationTrigger(string triggerName)
        {
            if (string.IsNullOrWhiteSpace(triggerName))
            {
                return;
            }

            Animator[] targetAnimators = GetAnimators();
            for (int i = 0; i < targetAnimators.Length; i++)
            {
                targetAnimators[i].SetTrigger(triggerName);
            }
        }

        public void RestartAnimationTrigger(string triggerName)
        {
            if (string.IsNullOrWhiteSpace(triggerName))
            {
                return;
            }

            Animator[] targetAnimators = GetAnimators();
            for (int i = 0; i < targetAnimators.Length; i++)
            {
                targetAnimators[i].ResetTrigger(triggerName);
                targetAnimators[i].SetTrigger(triggerName);
            }
        }

        public void BeginSnipingTelegraph(string shootTriggerName, string holdParameterName)
        {
            activeSnipingTelegraphCount++;
            SetAnimationBool(holdParameterName, true);
            if (activeSnipingTelegraphCount == 1)
            {
                PlayAnimationTrigger(shootTriggerName);
            }
        }

        public void ReleaseSnipingShot(string holdParameterName, string releaseTriggerName)
        {
            activeSnipingTelegraphCount = Mathf.Max(0, activeSnipingTelegraphCount - 1);
            SetAnimationBool(holdParameterName, activeSnipingTelegraphCount > 0);
            PlayAnimationTrigger(releaseTriggerName);
        }

        public void SetAnimationFloat(string parameterName, float value)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                return;
            }

            Animator[] targetAnimators = GetAnimators();
            for (int i = 0; i < targetAnimators.Length; i++)
            {
                targetAnimators[i].SetFloat(parameterName, value);
            }
        }

        public void SetAnimationInt(string parameterName, int value)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                return;
            }

            Animator[] targetAnimators = GetAnimators();
            for (int i = 0; i < targetAnimators.Length; i++)
            {
                targetAnimators[i].SetInteger(parameterName, value);
            }
        }

        public void SetAnimationBool(string parameterName, bool value)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                return;
            }

            Animator[] targetAnimators = GetAnimators();
            for (int i = 0; i < targetAnimators.Length; i++)
            {
                targetAnimators[i].SetBool(parameterName, value);
            }
        }

        public IEnumerator WaitForAnimationEvent(string eventId, float timeoutSeconds)
        {
            BossAnimationEventBridge bridge = GetAnimationEventBridge();
            if (bridge == null || string.IsNullOrWhiteSpace(eventId))
            {
                yield break;
            }

            int startVersion = bridge.GetVersion(eventId);
            float remaining = timeoutSeconds;
            while (bridge.GetVersion(eventId) <= startVersion)
            {
                if (IsExecutionPaused)
                {
                    Stop();
                    yield return null;
                    continue;
                }

                if (timeoutSeconds > 0f)
                {
                    remaining -= EnemyTimeScale.DeltaTime;
                    if (remaining <= 0f)
                    {
                        yield break;
                    }
                }

                UpdateContinuousActions();
                yield return null;
            }
        }

        public void MoveTowardPlayer(float speedMultiplier)
        {
            UpdateBossChildAims();
            ApplyPlayerRelativeMove(1f, speedMultiplier);
        }

        public void MoveTowardPlayer(AnimationCurve speedCurve, float elapsedSeconds)
        {
            MoveTowardPlayer(1f, speedCurve, elapsedSeconds, 0f);
        }

        public void MoveTowardPlayer(
            float speedMultiplier,
            AnimationCurve speedCurve,
            float elapsedSeconds,
            float durationSeconds)
        {
            UpdateBossChildAims();
            ApplyPlayerRelativeMove(1f, speedMultiplier, speedCurve, elapsedSeconds, durationSeconds);
        }

        public void MaintainPlayerDistance(
            float targetDistance,
            float tolerance,
            float speedMultiplier,
            AnimationCurve speedCurve,
            float elapsedSeconds,
            float durationSeconds)
        {
            UpdateBossChildAims();
            ApplyMaintainPlayerDistance(
                targetDistance,
                tolerance,
                Mathf.Max(0f, speedMultiplier) * EvaluateMoveSpeedCurve(speedCurve, elapsedSeconds, durationSeconds));
        }

        public void StartMoveTowardPlayer(float speedMultiplier)
        {
            StartPlayerRelativeMove(1f, speedMultiplier, BossMoveSpeedCurve.CreateConstant(), 0f);
        }

        public void StartMoveTowardPlayer(AnimationCurve speedCurve)
        {
            StartMoveTowardPlayer(1f, speedCurve, 0f);
        }

        public void StartMoveTowardPlayer(float speedMultiplier, AnimationCurve speedCurve, float durationSeconds)
        {
            StartPlayerRelativeMove(1f, speedMultiplier, speedCurve, durationSeconds);
        }

        public void StartMoveAwayFromPlayer(AnimationCurve speedCurve)
        {
            StartMoveAwayFromPlayer(1f, speedCurve, 0f);
        }

        public void StartMoveAwayFromPlayer(float speedMultiplier, AnimationCurve speedCurve, float durationSeconds)
        {
            StartPlayerRelativeMove(-1f, speedMultiplier, speedCurve, durationSeconds);
        }

        public void StopMoveTowardPlayer()
        {
            ClearPlayerRelativeMove();
            Stop();
        }

        private void UpdateContinuousActions()
        {
            UpdateBossChildAims();
            if (hasPlayerRelativeMoveIntent)
            {
                ApplyPlayerRelativeMove(
                    playerRelativeMoveDirectionSign,
                    playerRelativeMoveSpeedMultiplier,
                    playerRelativeMoveSpeedCurve,
                    playerRelativeMoveElapsedSeconds,
                    playerRelativeMoveDurationSeconds);
                playerRelativeMoveElapsedSeconds += EnemyTimeScale.DeltaTime;
                if (playerRelativeMoveDurationSeconds > 0f
                    && playerRelativeMoveElapsedSeconds >= playerRelativeMoveDurationSeconds)
                {
                    ClearPlayerRelativeMove();
                    Stop();
                }
            }
        }

        private void StartPlayerRelativeMove(
            float directionSign,
            float speedMultiplier,
            AnimationCurve speedCurve,
            float durationSeconds)
        {
            hasPlayerRelativeMoveIntent = true;
            playerRelativeMoveDirectionSign = directionSign < 0f ? -1f : 1f;
            playerRelativeMoveSpeedMultiplier = Mathf.Max(0f, speedMultiplier);
            playerRelativeMoveElapsedSeconds = 0f;
            playerRelativeMoveDurationSeconds = Mathf.Max(0f, durationSeconds);
            playerRelativeMoveSpeedCurve = speedCurve;
            ApplyPlayerRelativeMove(
                playerRelativeMoveDirectionSign,
                playerRelativeMoveSpeedMultiplier,
                speedCurve,
                0f,
                playerRelativeMoveDurationSeconds);
        }

        private void ClearPlayerRelativeMove()
        {
            hasPlayerRelativeMoveIntent = false;
            playerRelativeMoveDirectionSign = 1f;
            playerRelativeMoveSpeedMultiplier = 1f;
            playerRelativeMoveElapsedSeconds = 0f;
            playerRelativeMoveDurationSeconds = 0f;
            playerRelativeMoveSpeedCurve = null;
        }

        private void ApplyPlayerRelativeMove(
            float directionSign,
            float speedMultiplier,
            AnimationCurve speedCurve,
            float elapsedSeconds,
            float durationSeconds)
        {
            ApplyPlayerRelativeMove(
                directionSign,
                Mathf.Max(0f, speedMultiplier) * EvaluateMoveSpeedCurve(speedCurve, elapsedSeconds, durationSeconds));
        }

        private void ApplyPlayerRelativeMove(float directionSign, float speedMultiplier)
        {
            if (Boss == null || Boss.Body == null || Boss.Player == null)
            {
                return;
            }

            Vector2 direction = (Vector2)Boss.Player.position - (Vector2)Boss.transform.position;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                Stop();
                return;
            }

            Boss.SetMovementVelocity(direction.normalized
                * (directionSign < 0f ? -1f : 1f)
                * (Boss.MoveSpeed * Mathf.Max(0f, speedMultiplier)));
        }

        private void ApplyMaintainPlayerDistance(float targetDistance, float tolerance, float speedMultiplier)
        {
            if (Boss == null || Boss.Body == null || Boss.Player == null)
            {
                return;
            }

            Vector2 toPlayer = (Vector2)Boss.Player.position - (Vector2)Boss.transform.position;
            float currentDistance = toPlayer.magnitude;
            if (currentDistance <= 0.0001f)
            {
                Stop();
                return;
            }

            float safeDistance = Mathf.Max(0.1f, targetDistance);
            float safeTolerance = Mathf.Max(0f, tolerance);
            float distanceDelta = currentDistance - safeDistance;
            float absoluteDelta = Mathf.Abs(distanceDelta);
            if (absoluteDelta <= 0.01f)
            {
                Stop();
                return;
            }

            float directionSign = distanceDelta > 0f ? 1f : -1f;
            float smoothingRange = Mathf.Max(0.01f, safeTolerance);
            float speedScale = Mathf.Clamp01(absoluteDelta / smoothingRange);
            float desiredSpeed = Boss.MoveSpeed * Mathf.Max(0f, speedMultiplier) * speedScale;

            // 보스 속도가 플레이어보다 빠르면 한 프레임에 목표 거리를 지나쳐버려 다음 프레임 Stop()이
            // 걸리고, 그 다음 프레임에 다시 전속력으로 움직이는 식으로 멈췄다 움직였다를 반복하게 된다.
            // 남은 오차를 한 프레임이 아니라 최소 정착 시간(OvershootGuardSeconds)에 걸쳐 줄이도록
            // 속도를 캡 씌워, 오버슈트도 막고 프레임 타이밍에 예민하지 않은 부드러운 감속을 만든다.
            float settleSeconds = Mathf.Max(EnemyTimeScale.DeltaTime, OvershootGuardSeconds);
            float appliedSpeed = Mathf.Min(desiredSpeed, absoluteDelta / settleSeconds);

            Boss.SetMovementVelocity(toPlayer.normalized * directionSign * appliedSpeed);
        }

        private static float EvaluateMoveSpeedCurve(
            AnimationCurve speedCurve,
            float elapsedSeconds,
            float durationSeconds)
        {
            if (speedCurve == null || speedCurve.length == 0)
            {
                return 1f;
            }

            float normalizedTime = durationSeconds > 0f
                ? Mathf.Clamp01(Mathf.Max(0f, elapsedSeconds) / durationSeconds)
                : Mathf.Max(0f, elapsedSeconds);
            return Mathf.Max(0f, speedCurve.Evaluate(normalizedTime));
        }

        public IEnumerator MoveBodyRootToPosition(Vector3 targetLocalOffset, float seconds, bool stopWhenFinished)
        {
            Transform target = Boss != null ? Boss.BodyRoot : null;
            if (target == null || Boss == null || target == Boss.transform)
            {
                yield break;
            }

            if (!hasBodyRootLocalBase)
            {
                bodyRootLocalBase = target.localPosition;
                hasBodyRootLocalBase = true;
            }

            Vector3 from = target.localPosition;
            Vector3 to = bodyRootLocalBase + targetLocalOffset;
            float duration = Mathf.Max(0.01f, seconds);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (target == null)
                {
                    yield break;
                }

                if (IsExecutionPaused)
                {
                    Stop();
                    yield return null;
                    continue;
                }

                Stop();
                elapsed += Time.deltaTime;
                target.localPosition = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            if (target != null)
            {
                target.localPosition = to;
            }

            if (stopWhenFinished)
            {
                ResetBodyRootLocalOffset();
            }
        }

        public void StopBodyRootMovement()
        {
            Stop();
        }

        public void ResetBodyRootLocalOffset()
        {
            if (!hasBodyRootLocalBase)
            {
                return;
            }

            Transform target = Boss != null ? Boss.BodyRoot : null;
            if (target != null)
            {
                target.localPosition = bodyRootLocalBase;
            }

            hasBodyRootLocalBase = false;
        }

        public Vector3 GetBossChildPosition(string childPath)
        {
            UpdateBossChildAims();
            Transform target = FindBossChild(childPath);
            return target != null ? target.position : OriginPosition;
        }

        public Transform GetBossChildTransform(string childPath)
        {
            UpdateBossChildAims();
            return FindBossChild(childPath);
        }

        public void SetBossChildActive(string childPath, bool active)
        {
            Transform target = FindBossChild(childPath);
            if (target != null)
            {
                target.gameObject.SetActive(active);
            }
        }

        public void RotateBossChildRight(string childPath, Vector2 direction, bool flipYByFacing)
        {
            Transform target = FindBossChild(childPath);
            if (target == null || direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            if (!transformBaseScales.TryGetValue(target, out Vector3 baseLocalScale))
            {
                baseLocalScale = target.localScale;
                transformBaseScales[target] = baseLocalScale;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            target.rotation = Quaternion.Euler(0f, 0f, angle);

            if (!flipYByFacing)
            {
                return;
            }

            bool flipY = angle < -90f || angle > 90f;
            Vector3 nextScale = baseLocalScale;
            float authoredSign = baseLocalScale.y < 0f ? -1f : 1f;
            nextScale.y = Mathf.Abs(baseLocalScale.y) * authoredSign * (flipY ? -1f : 1f);
            target.localScale = nextScale;
        }

        public void StartBossChildAimAtPlayer(
            string childPath,
            bool activateOnStart,
            bool flipYByFacing,
            bool deactivateOnPatternEnd)
        {
            if (string.IsNullOrWhiteSpace(childPath))
            {
                return;
            }

            if (activateOnStart)
            {
                SetBossChildActive(childPath, true);
            }

            bossChildAimStates[childPath] = new BossChildAimState(childPath, flipYByFacing, deactivateOnPatternEnd);
            if (!string.IsNullOrWhiteSpace(currentNodeId))
            {
                bossChildAimStartNodePaths[currentNodeId] = childPath;
            }

            UpdateBossChildAim(childPath, flipYByFacing);
        }

        public bool StopBossChildAimAtPlayerStartedByNode(string startNodeId, bool deactivate)
        {
            if (string.IsNullOrWhiteSpace(startNodeId)
                || !bossChildAimStartNodePaths.TryGetValue(startNodeId, out string childPath))
            {
                return false;
            }

            StopBossChildAimAtPlayer(childPath, deactivate);
            return true;
        }

        public void StopBossChildAimAtPlayer(string childPath, bool deactivate)
        {
            if (string.IsNullOrWhiteSpace(childPath))
            {
                return;
            }

            bossChildAimStates.Remove(childPath);
            RemoveBossChildAimStartNodePaths(childPath);
            if (deactivate)
            {
                SetBossChildActive(childPath, false);
            }
        }

        public void RegisterTransientVisual(GameObject visual)
        {
            if (visual != null)
            {
                transientVisuals.Add(visual);
            }
        }

        public void UnregisterTransientVisual(GameObject visual)
        {
            if (visual != null)
            {
                transientVisuals.Remove(visual);
            }
        }

        public void ClearPatternScopedBossChildAims()
        {
            ClearPlayerRelativeMove();
            projectileHandles.Clear();
            DestroyTransientVisuals();
            if (bossChildAimStates.Count == 0)
            {
                bossChildAimStartNodePaths.Clear();
                return;
            }

            List<BossChildAimState> states = new(bossChildAimStates.Values);
            bossChildAimStates.Clear();
            bossChildAimStartNodePaths.Clear();
            for (int i = 0; i < states.Count; i++)
            {
                BossChildAimState state = states[i];
                if (state.DeactivateOnPatternEnd)
                {
                    SetBossChildActive(state.ChildPath, false);
                }
            }
        }

        public void UpdateBossChildAims()
        {
            if (bossChildAimStates.Count == 0)
            {
                return;
            }

            foreach (BossChildAimState state in bossChildAimStates.Values)
            {
                UpdateBossChildAim(state.ChildPath, state.FlipYByFacing);
            }
        }

        private void DestroyTransientVisuals()
        {
            if (transientVisuals.Count == 0)
            {
                return;
            }

            for (int i = 0; i < transientVisuals.Count; i++)
            {
                if (transientVisuals[i] != null)
                {
                    UnityEngine.Object.Destroy(transientVisuals[i]);
                }
            }

            transientVisuals.Clear();
        }

        private void RemoveBossChildAimStartNodePaths(string childPath)
        {
            if (bossChildAimStartNodePaths.Count == 0)
            {
                return;
            }

            List<string> startNodeIds = new();
            foreach (KeyValuePair<string, string> pair in bossChildAimStartNodePaths)
            {
                if (pair.Value == childPath)
                {
                    startNodeIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < startNodeIds.Count; i++)
            {
                bossChildAimStartNodePaths.Remove(startNodeIds[i]);
            }
        }

        public Vector2 GetDirectionToPlayer(Vector3 origin)
        {
            if (Boss is HackerHologramBoss hologram
                && hologram.TryGetRecordedPlayerPosition(out Vector2 recordedPlayerPosition))
            {
                Vector2 recordedDirection = recordedPlayerPosition - (Vector2)origin;
                return recordedDirection.sqrMagnitude > 0.0001f
                    ? recordedDirection.normalized
                    : Vector2.left;
            }

            if (Boss == null || Boss.Player == null)
            {
                return Vector2.left;
            }

            Vector2 direction = (Vector2)Boss.Player.position - (Vector2)origin;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.left;
        }

        public Vector2 GetPlayerPosition()
        {
            if (Boss is HackerHologramBoss hologram
                && hologram.TryGetRecordedPlayerPosition(out Vector2 recordedPlayerPosition))
            {
                return recordedPlayerPosition;
            }

            if (Boss?.Player != null)
            {
                return Boss.Player.position;
            }

            return Boss != null ? (Vector2)Boss.transform.position : Vector2.zero;
        }

        public static Vector2 AngleToDirection(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        public EnemyProjectile FireProjectile(
            BossProjectileSettings projectileSettings,
            Vector3 origin,
            Vector2 direction,
            float muzzleFlashScale,
            bool? aimAtPlayerWhileChargingOverride = null,
            bool? aimAtPlayerOnLaunchOverride = null,
            float chargeSecondsOverride = -1f,
            float radiusOverride = -1f,
            bool suppressHoming = false,
            string projectileName = null,
            bool useExactSettings = false)
        {
            UpdateBossChildAims();
            BossProjectileSettings resolvedSettings = useExactSettings
                ? projectileSettings
                : !string.IsNullOrWhiteSpace(projectileName)
                    ? ResolveGraphProjectileSettings(projectileName)
                    : ResolveGraphProjectileSettings(null) ?? projectileSettings;
            if (Boss == null || resolvedSettings == null || direction.sqrMagnitude <= 0.0001f)
            {
                return null;
            }

            // 홀로그램은 본체와 겹친 위치에서 발사할 수 있다. 자체 소유 탄환으로 만들면
            // 본체와 충돌해 즉시 파괴되므로, 처음부터 본체 소유자로 생성한다.
            if (Boss is HackerHologramBoss hologram)
            {
                return hologram.FireReplayProjectile(
                    resolvedSettings,
                    origin,
                    direction.normalized,
                    muzzleFlashScale,
                    aimAtPlayerWhileChargingOverride,
                    aimAtPlayerOnLaunchOverride,
                    chargeSecondsOverride,
                    radiusOverride,
                    suppressHoming);
            }

            return Boss.FireGraphProjectile(
                resolvedSettings,
                origin,
                direction.normalized,
                muzzleFlashScale,
                aimAtPlayerWhileChargingOverride,
                aimAtPlayerOnLaunchOverride,
                chargeSecondsOverride,
                radiusOverride,
                suppressHoming);
        }

        public BossProjectileSettings ResolveGraphProjectileSettings(string projectileName)
        {
            return Boss != null ? Boss.ResolveGraphProjectileSettingsForActions(projectileName) : null;
        }

        public void SetProjectileHandle(string handleKey, EnemyProjectile projectile)
        {
            if (string.IsNullOrWhiteSpace(handleKey) || projectile == null)
            {
                return;
            }

            projectileHandles[handleKey] = projectile;
        }

        public EnemyProjectile GetProjectileHandle(string handleKey)
        {
            if (string.IsNullOrWhiteSpace(handleKey)
                || !projectileHandles.TryGetValue(handleKey, out EnemyProjectile projectile))
            {
                return null;
            }

            if (projectile != null)
            {
                return projectile;
            }

            projectileHandles.Remove(handleKey);
            return null;
        }

        public void PlaySfx(string sfxId)
        {
            if (string.IsNullOrWhiteSpace(sfxId))
            {
                return;
            }

            SoundManager.PlaySfx(sfxId);
        }

        public void PlaySfxOnLaunch(EnemyProjectile projectile, string sfxId)
        {
            if (projectile == null || string.IsNullOrWhiteSpace(sfxId))
            {
                return;
            }

            void HandleLaunched(EnemyProjectile launchedProjectile)
            {
                launchedProjectile.Launched -= HandleLaunched;
                PlaySfx(sfxId);
            }

            projectile.Launched += HandleLaunched;
        }

        public void PlaySfxOnRadialSplitImminent(EnemyProjectile projectile, string sfxId)
        {
            if (projectile == null || string.IsNullOrWhiteSpace(sfxId))
            {
                return;
            }

            void HandleRadialSplitImminent(EnemyProjectile splitProjectile)
            {
                splitProjectile.RadialSplitImminent -= HandleRadialSplitImminent;
                PlaySfx(sfxId);
            }

            projectile.RadialSplitImminent += HandleRadialSplitImminent;
        }

        public void PlayOriginBurst(BossGraphEffectSettings effects, Vector3 position)
        {
            PlaySmokeIfEnabled(effects, position);
        }

        public void PlaySmokeIfDue(ref float nextSmokeAt, BossGraphEffectSettings effects, Vector3 position)
        {
            if (effects == null || Time.time < nextSmokeAt)
            {
                return;
            }

            PlaySmokeIfEnabled(effects, position);
            nextSmokeAt = Time.time + Mathf.Max(0.01f, effects.SmokeInterval);
        }

        public void PlayMuzzleFlashIfEnabled(
            BossGraphEffectSettings effects,
            EnemyProjectile projectile,
            Vector2 direction,
            Transform followTarget = null)
        {
            if (projectile == null)
            {
                return;
            }

            PlayMuzzleFlashIfEnabled(effects, projectile.transform.position, direction, followTarget);
        }

        public void PlayMuzzleFlashIfEnabled(
            BossGraphEffectSettings effects,
            Vector3 origin,
            Vector2 direction,
            Transform followTarget = null)
        {
            BossGraphPrefabEffectSettings muzzleFlash = effects?.MuzzleFlash;
            if (muzzleFlash == null || !muzzleFlash.Enabled)
            {
                return;
            }

            Transform resolvedFollowTarget = followTarget;
            if (resolvedFollowTarget == null && Boss != null)
            {
                resolvedFollowTarget = Boss.BodyRoot != null ? Boss.BodyRoot : Boss.transform;
            }

            ProjectileVfx.PlayPrefab(muzzleFlash.Prefab, origin, direction, resolvedFollowTarget, muzzleFlash.Scale);
        }

        public void PlayCameraShakeIfEnabled(BossGraphEffectSettings effects, Vector2 direction)
        {
            BossGraphCameraShakeSettings shake = effects?.CameraShake;
            if (shake == null || !shake.Enabled)
            {
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            CameraFollow2D cameraFollow = mainCamera.GetComponent<CameraFollow2D>();
            cameraFollow?.PlayImpact(direction, shake.Seconds, shake.Distance, shake.Frequency);
        }

        public void SendCustomEvent(string methodName, bool broadcastToChildren)
        {
            if (Boss == null || string.IsNullOrWhiteSpace(methodName))
            {
                return;
            }

            if (broadcastToChildren)
            {
                Boss.gameObject.BroadcastMessage(methodName, SendMessageOptions.DontRequireReceiver);
                return;
            }

            Boss.gameObject.SendMessage(methodName, SendMessageOptions.DontRequireReceiver);
        }

        public GameObject SpawnPrefab(GameObject prefab, Vector3 offset, Vector3 rotationEuler, bool parentToBoss)
        {
            if (prefab == null)
            {
                return null;
            }

            Transform parent = parentToBoss && Boss != null ? Boss.transform : null;
            GameObject instance = UnityEngine.Object.Instantiate(
                prefab,
                OriginPosition + offset,
                Quaternion.Euler(rotationEuler),
                parent);
            BossSorting.ApplyToChildren(instance);
            return instance;
        }

        public IEnumerator WaitSeconds(float seconds)
        {
            float remaining = Mathf.Max(0f, seconds);
            while (remaining > 0f)
            {
                if (IsExecutionPaused)
                {
                    Stop();
                    yield return null;
                    continue;
                }

                UpdateContinuousActions();
                remaining -= EnemyTimeScale.DeltaTime;
                yield return null;
            }
        }

        // BodyRoot 밑에 Animator가 여러 개 있으면(예: Assassin처럼 애니메이터 두 개를 나눠 쓰는 보스)
        // 전부 찾아서 캐싱해두고, 트리거/파라미터 명령을 전부 동일하게 받는다.
        private Animator[] GetAnimators()
        {
            if (animators != null)
            {
                return animators;
            }

            if (Boss == null)
            {
                animators = Array.Empty<Animator>();
                return animators;
            }

            Animator[] discoveredAnimators = Boss.BodyRoot != null
                ? Boss.BodyRoot.GetComponentsInChildren<Animator>(true)
                : Boss.GetComponentsInChildren<Animator>(true);
            List<Animator> activeAnimators = new(discoveredAnimators.Length);
            for (int i = 0; i < discoveredAnimators.Length; i++)
            {
                Animator animator = discoveredAnimators[i];
                if (animator != null
                    && (Boss is not HackerBossAI || animator.gameObject.activeInHierarchy))
                {
                    activeAnimators.Add(animator);
                }
            }

            animators = activeAnimators.ToArray();
            return animators;
        }

        private BossAnimationEventBridge GetAnimationEventBridge()
        {
            if (animationEventBridge != null)
            {
                return animationEventBridge;
            }

            Animator[] targetAnimators = GetAnimators();
            GameObject targetObject = targetAnimators.Length > 0
                ? targetAnimators[0].gameObject
                : Boss != null ? Boss.gameObject : null;
            if (targetObject == null)
            {
                return null;
            }

            animationEventBridge = targetObject.GetComponent<BossAnimationEventBridge>();
            if (animationEventBridge == null)
            {
                animationEventBridge = targetObject.AddComponent<BossAnimationEventBridge>();
            }

            return animationEventBridge;
        }

        private static void PlaySmokeIfEnabled(BossGraphEffectSettings effects, Vector3 position)
        {
            BossGraphParticleEffectSettings smoke = effects?.Smoke;
            if (smoke == null || !smoke.Enabled)
            {
                return;
            }

            ProjectileVfx.PlayHogSmokeBurst(position, smoke.Color, smoke.Scale, smoke.Count);
        }

        private Transform FindBossChild(string childPath)
        {
            if (Boss == null || string.IsNullOrWhiteSpace(childPath))
            {
                return null;
            }

            Transform found = Boss.transform.Find(childPath);
            return found != null ? found : FindChildRecursive(Boss.transform, childPath);
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                Transform nested = FindChildRecursive(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private void UpdateBossChildAim(string childPath, bool flipYByFacing)
        {
            Transform target = FindBossChild(childPath);
            if (target == null)
            {
                return;
            }

            RotateBossChildRight(childPath, GetDirectionToPlayer(target.position), flipYByFacing);
        }

        private readonly struct BossChildAimState
        {
            public BossChildAimState(string childPath, bool flipYByFacing, bool deactivateOnPatternEnd)
            {
                ChildPath = childPath;
                FlipYByFacing = flipYByFacing;
                DeactivateOnPatternEnd = deactivateOnPatternEnd;
            }

            public string ChildPath { get; }
            public bool FlipYByFacing { get; }
            public bool DeactivateOnPatternEnd { get; }
        }
    }
}
