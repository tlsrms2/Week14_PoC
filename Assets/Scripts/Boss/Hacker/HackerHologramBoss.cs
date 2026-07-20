using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Week14.Combat;
using Week14.UI;

namespace Week14.Enemy
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Week14/Boss/Hacker Hologram")]
    public sealed class HackerHologramBoss : HackerBossAI
    {
        private const float ReturnSeconds = 0.2f;
        private const float SummonSideDistance = 0.45f;
        private const float SummonOutSeconds = 0.45f;
        private const float SummonSideHoldSeconds = 0.35f;
        private const float SummonReturnSeconds = 0.55f;
        private const float ReplayWallProbeRadius = 0.22f;
        private static readonly Color HologramTint = new(0.3f, 0.85f, 1f, 0.58f);

        private HackerBossAI sourceBoss;
        private bool isHologramReplayRunning;
        private bool isRecordingReplay;
        private bool isReplayPlaybackComplete;
        private bool isReturningToOwner;
        private bool isPlayingSummonEntrance;
        private bool hasRecordedPlayerPosition;
        private bool hasReplayPositionArcPivot;
        private float replayDelaySeconds;
        private float replayClock;
        private float replayCaptureTime;
        private float replayPositionFollowPauseSeconds;
        private float replayPositionLockSeconds;
        private float replayPositionArcOffsetDegrees;
        private float replayPositionArcDirectionSign = 1f;
        private Coroutine summonEntranceCoroutine;
        private Vector2 recordedPlayerPosition;
        private Vector2 replayPositionArcPivot;
        private Collider2D[] replayMovementColliders = new Collider2D[0];
        private Rigidbody2D replayMovementBody;
        private readonly Queue<RecordedReplayFrame> replayFrames = new();
        private readonly Queue<RecordedReplayActionGroup> replayActionGroups = new();
        private readonly List<TransformReplayBinding> transformReplayBindings = new();
        private readonly List<SpriteReplayBinding> spriteReplayBindings = new();
        private readonly HashSet<SpriteRenderer> excludedReplayRenderers = new();

        protected override bool UsesHackerPresentationUpdates => false;

        protected override void Awake()
        {
            base.Awake();

            // 홀로그램 프리팹/씬 인스턴스는 초기화 전 첫 프레임부터 숨긴다.
            // Instantiate 직후 Initialize가 호출될 때만 다시 활성화된다.
            if (sourceBoss == null)
            {
                gameObject.SetActive(false);
            }
        }

        protected override void OnEnable()
        {
            // 홀로그램은 전투 개체가 아니므로 체력 이벤트와 보스 UI를 연결하지 않는다.
        }

        internal void Initialize(HackerBossAI source)
        {
            sourceBoss = source;
            StripGameplayComponents();
            ResolvePlayerForState();
            HpGauge?.Configure(Mathf.Max(1, sourceBoss?.HpGauge?.MaxBullets ?? 1), true);
            BuildReplayBindings();
            ApplyHologramTint();
            DisableHologramAnimators();
            gameObject.SetActive(true);
        }

        internal void BeginRecordedReplay(float delaySeconds)
        {
            StopSummonEntrance();
            CancelRecordedReplay();
            if (sourceBoss == null)
            {
                return;
            }

            Stop();
            SetReplayRootPosition(GetRestPosition());
            replayDelaySeconds = Mathf.Max(0.01f, delaySeconds);
            replayClock = 0f;
            replayCaptureTime = 0f;
            replayPositionFollowPauseSeconds = 0f;
            replayPositionLockSeconds = 0f;
            replayPositionArcOffsetDegrees = 0f;
            replayPositionArcDirectionSign = 1f;
            hasReplayPositionArcPivot = false;
            isHologramReplayRunning = true;
            isRecordingReplay = true;
            isReplayPlaybackComplete = false;
            replayActionGroups.Clear();
            RecordedReplayFrame initialFrame = CaptureReplayFrame(0f);
            replayFrames.Enqueue(initialFrame);
            ApplyRecordedPlayerPosition(initialFrame);
        }

        internal void PlaySummonEntrance()
        {
            if (sourceBoss == null || isPlayingSummonEntrance)
            {
                return;
            }

            SetReplayRootPosition(GetRestPosition());
            ApplyCurrentSourcePose();
            isPlayingSummonEntrance = true;
            summonEntranceCoroutine = StartCoroutine(PlaySummonEntranceRoutine());
        }

        internal IEnumerator WaitForSummonEntrance()
        {
            while (isPlayingSummonEntrance && sourceBoss != null)
            {
                yield return null;
            }
        }

        internal void EndRecordedReplayCapture()
        {
            if (!isHologramReplayRunning || !isRecordingReplay)
            {
                return;
            }

            isRecordingReplay = false;
            replayFrames.Enqueue(CaptureReplayFrame(replayCaptureTime));
        }

        internal IEnumerator WaitForRecordedReplayCompletion()
        {
            while (!isReplayPlaybackComplete)
            {
                if (sourceBoss == null)
                {
                    CancelRecordedReplay();
                    yield break;
                }

                yield return null;
            }
        }

        internal void CancelRecordedReplay()
        {
            ClearPatternSpawnedWeapons();
            isHologramReplayRunning = false;
            isRecordingReplay = false;
            isReplayPlaybackComplete = true;
            hasRecordedPlayerPosition = false;
            replayDelaySeconds = 0f;
            replayClock = 0f;
            replayCaptureTime = 0f;
            replayPositionFollowPauseSeconds = 0f;
            replayPositionLockSeconds = 0f;
            replayPositionArcOffsetDegrees = 0f;
            replayPositionArcDirectionSign = 1f;
            hasReplayPositionArcPivot = false;
            replayFrames.Clear();
            replayActionGroups.Clear();
        }

        internal bool IsRecordingReplayActions => isRecordingReplay;

        internal bool HasPendingReplayActionGroups => replayActionGroups.Count > 0;

        internal bool IsPlayingSummonEntrance => isPlayingSummonEntrance;

        internal void RecordReplayActionGroup(IReadOnlyList<BossStateNode> nodes)
        {
            if (!isHologramReplayRunning || !isRecordingReplay || nodes == null)
            {
                return;
            }

            List<string> nodeIds = new();
            for (int i = 0; i < nodes.Count; i++)
            {
                string nodeId = nodes[i]?.NodeId;
                if (!string.IsNullOrWhiteSpace(nodeId))
                {
                    nodeIds.Add(nodeId);
                }
            }

            if (nodeIds.Count > 0)
            {
                replayActionGroups.Enqueue(new RecordedReplayActionGroup(replayCaptureTime, nodeIds));
            }
        }

        internal bool TryDequeueReplayActionGroup(out IReadOnlyList<string> nodeIds)
        {
            nodeIds = null;
            if (replayActionGroups.Count == 0
                || replayActionGroups.Peek().Time > replayClock - replayDelaySeconds + 0.0001f)
            {
                return false;
            }

            nodeIds = replayActionGroups.Dequeue().NodeIds;
            return true;
        }

        internal IEnumerator ReturnToOwner()
        {
            if (sourceBoss == null)
            {
                yield break;
            }

            isReturningToOwner = true;
            Vector3 startPosition = transform.position;
            float elapsed = 0f;
            try
            {
                while (elapsed < ReturnSeconds && sourceBoss != null)
                {
                    if (BossAI.IsExecutionPausedForState)
                    {
                        yield return null;
                        continue;
                    }

                    float progress = Mathf.Clamp01(elapsed / ReturnSeconds);
                    SetReplayRootPosition(ClampReplayPosition(
                        Vector3.Lerp(startPosition, GetRestPosition(), progress)));
                    elapsed += EnemyTimeScale.DeltaTime;
                    yield return null;
                }

                if (sourceBoss != null)
                {
                    SetReplayRootPosition(GetRestPosition());
                }
            }
            finally
            {
                isReturningToOwner = false;
            }
        }

        internal bool TryGetRecordedPlayerPosition(out Vector2 playerPosition)
        {
            playerPosition = recordedPlayerPosition;
            return hasRecordedPlayerPosition;
        }

        internal bool TryCreateOrbitReplayWeapon(
            HackerRecallWeaponSelection selection,
            out HackerThrownWeapon replayWeapon)
        {
            replayWeapon = null;
            if (!TryGetSourceWeapon(selection, out HackerThrownWeapon sourceWeapon)
                || sourceWeapon == null)
            {
                return false;
            }

            GameObject replayWeaponObject = Instantiate(
                sourceWeapon.gameObject,
                sourceWeapon.transform.position,
                sourceWeapon.transform.rotation);
            replayWeaponObject.name = "HackerHologramOrbitWeapon";
            DisableReplayWeaponPhysics(replayWeaponObject);
            ApplyHologramTint(replayWeaponObject);
            replayWeapon = replayWeaponObject.GetComponent<HackerThrownWeapon>();
            return replayWeapon != null;
        }

        internal void ApplyHologramStyle(GameObject target)
        {
            ApplyHologramTint(target);
        }

        public override float DistanceToPlayer()
        {
            return hasRecordedPlayerPosition
                ? Vector2.Distance(transform.position, recordedPlayerPosition)
                : base.DistanceToPlayer();
        }

        internal override void SetMovementVelocity(Vector2 velocity)
        {
            // 홀로그램의 위치는 본체 기록으로만 재생한다.
        }

        internal void PauseRecordedPositionFollowing(float seconds)
        {
            replayPositionFollowPauseSeconds = Mathf.Max(
                replayPositionFollowPauseSeconds,
                Mathf.Max(0f, seconds));
        }

        internal void LockRecordedPosition(float seconds)
        {
            replayPositionLockSeconds = Mathf.Max(
                replayPositionLockSeconds,
                Mathf.Max(0f, seconds));
        }

        internal void SetReplayPositionArcOffset(float degrees)
        {
            bool startsNewArc = Mathf.Approximately(degrees, 0f)
                && !Mathf.Approximately(replayPositionArcOffsetDegrees, 0f);
            if (startsNewArc)
            {
                hasReplayPositionArcPivot = false;
            }

            if (!hasReplayPositionArcPivot && hasRecordedPlayerPosition)
            {
                replayPositionArcPivot = recordedPlayerPosition;
                hasReplayPositionArcPivot = true;
                Vector2 fromPivot = (Vector2)transform.position - replayPositionArcPivot;
                // 좌우 어느 쪽에서 시작해도 두 회전 후보 중 월드 아래쪽 궤적을 선택한다.
                replayPositionArcDirectionSign = fromPivot.x >= 0f ? -1f : 1f;
            }

            replayPositionArcOffsetDegrees = Mathf.Abs(degrees) * replayPositionArcDirectionSign;
        }

        internal EnemyProjectile FireReplayProjectile(
            BossProjectileSettings settings,
            Vector3 origin,
            Vector2 direction,
            float muzzleFlashScale,
            bool? aimAtPlayerWhileChargingOverride,
            bool? aimAtPlayerOnLaunchOverride,
            float chargeSecondsOverride,
            float radiusOverride,
            bool suppressHoming)
        {
            if (sourceBoss == null)
            {
                return null;
            }

            return sourceBoss.FireGraphProjectile(
                    settings,
                    origin,
                    direction,
                    muzzleFlashScale,
                    aimAtPlayerWhileChargingOverride,
                    aimAtPlayerOnLaunchOverride,
                    chargeSecondsOverride,
                    radiusOverride,
                    suppressHoming);
        }

        internal override HackerWireSettings WireSettings => sourceBoss != null
            ? sourceBoss.WireSettings
            : base.WireSettings;

        internal override BossProjectileSettings ParryProjectileSettings => sourceBoss != null
            ? sourceBoss.ParryProjectileSettings
            : base.ParryProjectileSettings;

        internal override bool ShowsAttackRangeIndicators => sourceBoss != null
            ? sourceBoss.ShowsAttackRangeIndicators
            : base.ShowsAttackRangeIndicators;

        internal override void ApplyWireLifetimePenalty(PlayerCombatController player)
        {
            if (sourceBoss != null)
            {
                sourceBoss.ApplyWireLifetimePenalty(player);
                return;
            }

            base.ApplyWireLifetimePenalty(player);
        }

        public override bool ReceivePlayerHit(
            int bulletDamage,
            bool strongHit,
            Vector3 hitPosition,
            Vector2 hitDirection,
            Color hitColor)
        {
            return true;
        }

        protected override BossProjectileSettings ResolveGraphProjectileSettings(string projectileName)
        {
            return sourceBoss != null
                ? sourceBoss.ResolveGraphProjectileSettingsForActions(projectileName)
                : base.ResolveGraphProjectileSettings(projectileName);
        }

        protected override void Start()
        {
            // Initialize되지 않은 씬 잔존 인스턴스는 비활성 상태를 유지한다.
            if (sourceBoss == null)
            {
                gameObject.SetActive(false);
            }
        }

        protected override void Update()
        {
            // 홀로그램의 위치와 포즈는 OnIdleHackerLateUpdate에서 기록 프레임으로만 갱신한다.
        }

        protected override void OnIdleHackerLateUpdate()
        {
            if (isHologramReplayRunning)
            {
                UpdateRecordedReplay();
                return;
            }

            if (isPlayingSummonEntrance)
            {
                ApplyCurrentSourcePose();
                return;
            }

            if (!isReturningToOwner && sourceBoss != null)
            {
                SetReplayRootPosition(GetRestPosition());
                ApplyCurrentSourcePose();
            }
        }

        protected override void OnDisable()
        {
            StopSummonEntrance();
            CancelRecordedReplay();
        }

        private IEnumerator PlaySummonEntranceRoutine()
        {
            try
            {
                // 페이즈 변경 콜백보다 PhaseTransitionWait 설정이 늦으므로 한 프레임 뒤부터 판정한다.
                yield return null;
                yield return WaitForSummonEntranceStart();
                yield return MoveAlongSummonOffset(0f, 1f, SummonOutSeconds);
                yield return WaitSummonSeconds(SummonSideHoldSeconds);
                yield return MoveAlongSummonOffset(1f, 0f, SummonReturnSeconds);
                SetReplayRootPosition(GetRestPosition());
            }
            finally
            {
                isPlayingSummonEntrance = false;
                summonEntranceCoroutine = null;
            }
        }

        private IEnumerator MoveAlongSummonOffset(float from, float to, float seconds)
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, seconds);
            while (elapsed < duration && sourceBoss != null)
            {
                if (ShouldPauseSummonEntrance())
                {
                    yield return null;
                    continue;
                }

                float progress = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                SetReplayRootPosition(ClampReplayPosition(
                    GetRestPosition() + GetSummonSideOffset() * Mathf.Lerp(from, to, progress)));
                ApplyCurrentSourcePose();
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }
        }

        private IEnumerator WaitForSummonEntranceStart()
        {
            while (sourceBoss != null && ShouldPauseSummonEntrance())
            {
                yield return null;
            }
        }

        private IEnumerator WaitSummonSeconds(float seconds)
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0f, seconds);
            while (elapsed < duration && sourceBoss != null)
            {
                if (ShouldPauseSummonEntrance())
                {
                    yield return null;
                    continue;
                }

                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }
        }

        private bool ShouldPauseSummonEntrance()
        {
            return BossAI.IsExecutionPausedForState
                || (sourceBoss != null && sourceBoss.IsPhaseTransitionWaitingForState);
        }

        private Vector3 GetSummonSideOffset()
        {
            float direction = sourceBoss != null && sourceBoss.IsFacingLeft ? 1f : -1f;
            return Vector3.right * (SummonSideDistance * direction);
        }

        private void StopSummonEntrance()
        {
            if (summonEntranceCoroutine != null)
            {
                StopCoroutine(summonEntranceCoroutine);
                summonEntranceCoroutine = null;
            }

            isPlayingSummonEntrance = false;
        }

        private void UpdateRecordedReplay()
        {
            if (sourceBoss == null)
            {
                CancelRecordedReplay();
                return;
            }

            if (BossAI.IsExecutionPausedForState)
            {
                return;
            }

            float deltaTime = EnemyTimeScale.DeltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            if (isRecordingReplay)
            {
                replayCaptureTime += deltaTime;
                replayFrames.Enqueue(CaptureReplayFrame(replayCaptureTime));
            }

            if (replayPositionFollowPauseSeconds > 0f)
            {
                replayPositionFollowPauseSeconds = Mathf.Max(
                    0f,
                    replayPositionFollowPauseSeconds - deltaTime);
                return;
            }

            bool lockRootPosition = replayPositionLockSeconds > 0f;
            if (lockRootPosition)
            {
                replayPositionLockSeconds = Mathf.Max(
                    0f,
                    replayPositionLockSeconds - deltaTime);
            }

            replayClock += deltaTime;

            float playbackTime = replayClock - replayDelaySeconds;
            while (replayFrames.Count > 0 && replayFrames.Peek().Time <= playbackTime + 0.0001f)
            {
                RecordedReplayFrame frame = replayFrames.Dequeue();
                ApplyRecordedReplayFrame(frame, lockRootPosition);
            }

            if (!isRecordingReplay && replayFrames.Count == 0)
            {
                isReplayPlaybackComplete = true;
            }
        }

        private void BuildReplayBindings()
        {
            transformReplayBindings.Clear();
            spriteReplayBindings.Clear();
            if (sourceBoss == null)
            {
                return;
            }

            AddSpriteBindings(sourceBoss.transform, transform);
            Transform[] sourceTransforms = sourceBoss.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < sourceTransforms.Length; i++)
            {
                Transform sourceTransform = sourceTransforms[i];
                if (sourceTransform == null || sourceTransform == sourceBoss.transform)
                {
                    continue;
                }

                string relativePath = GetRelativePath(sourceBoss.transform, sourceTransform);
                Transform targetTransform = transform.Find(relativePath);
                if (targetTransform == null)
                {
                    continue;
                }

                transformReplayBindings.Add(new TransformReplayBinding(sourceTransform, targetTransform));
                AddSpriteBindings(sourceTransform, targetTransform);
            }
        }

        private void AddSpriteBindings(Transform sourceTransform, Transform targetTransform)
        {
            SpriteRenderer[] sourceRenderers = sourceTransform.GetComponents<SpriteRenderer>();
            SpriteRenderer[] targetRenderers = targetTransform.GetComponents<SpriteRenderer>();
            int bindingCount = Mathf.Min(sourceRenderers.Length, targetRenderers.Length);
            for (int i = 0; i < bindingCount; i++)
            {
                if (!excludedReplayRenderers.Contains(targetRenderers[i]))
                {
                    spriteReplayBindings.Add(new SpriteReplayBinding(sourceRenderers[i], targetRenderers[i]));
                }
            }
        }

        private RecordedReplayFrame CaptureReplayFrame(float time)
        {
            TransformReplayState[] transformStates = new TransformReplayState[transformReplayBindings.Count];
            for (int i = 0; i < transformReplayBindings.Count; i++)
            {
                Transform sourceTransform = transformReplayBindings[i].Source;
                transformStates[i] = sourceTransform != null
                    ? new TransformReplayState(
                        true,
                        sourceTransform.localPosition,
                        sourceTransform.localRotation,
                        sourceTransform.localScale,
                        sourceTransform.gameObject.activeSelf)
                    : default;
            }

            SpriteReplayState[] spriteStates = new SpriteReplayState[spriteReplayBindings.Count];
            for (int i = 0; i < spriteReplayBindings.Count; i++)
            {
                SpriteRenderer sourceRenderer = spriteReplayBindings[i].Source;
                spriteStates[i] = sourceRenderer != null
                    ? new SpriteReplayState(
                        true,
                        sourceRenderer.sprite,
                        sourceRenderer.color,
                        sourceRenderer.flipX,
                        sourceRenderer.flipY,
                        sourceRenderer.enabled,
                        sourceRenderer.sortingLayerID,
                        sourceRenderer.sortingOrder)
                    : default;
            }

            return new RecordedReplayFrame(
                time,
                sourceBoss.transform.position,
                sourceBoss.transform.rotation,
                sourceBoss.transform.localScale,
                sourceBoss.Player != null,
                sourceBoss.Player != null ? (Vector2)sourceBoss.Player.position : Vector2.zero,
                transformStates,
                spriteStates);
        }

        private void ApplyRecordedReplayFrame(RecordedReplayFrame frame, bool lockRootPosition)
        {
            if (!lockRootPosition)
            {
                SetReplayRootPosition(ClampReplayPosition(GetArcOffsetPosition(frame)));
            }

            transform.rotation = frame.RootRotation;
            transform.localScale = frame.RootLocalScale;
            ApplyRecordedPlayerPosition(frame);
            ApplyTransformStates(frame.TransformStates);
            ApplySpriteStates(frame.SpriteStates);
        }

        private Vector3 GetArcOffsetPosition(RecordedReplayFrame frame)
        {
            if (!frame.HasPlayerPosition || Mathf.Approximately(replayPositionArcOffsetDegrees, 0f))
            {
                return frame.RootPosition;
            }

            Vector2 arcPivot = hasReplayPositionArcPivot
                ? replayPositionArcPivot
                : frame.PlayerPosition;
            Vector2 fromPlayer = (Vector2)frame.RootPosition - arcPivot;
            if (fromPlayer.sqrMagnitude <= 0.0001f)
            {
                return frame.RootPosition;
            }

            float radians = replayPositionArcOffsetDegrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            Vector2 rotatedOffset = new(
                fromPlayer.x * cosine - fromPlayer.y * sine,
                fromPlayer.x * sine + fromPlayer.y * cosine);
            Vector2 position = arcPivot + rotatedOffset;
            return new Vector3(position.x, position.y, frame.RootPosition.z);
        }

        private Vector3 ClampReplayPosition(Vector3 targetPosition)
        {
            if (sourceBoss == null)
            {
                return targetPosition;
            }

            Vector2 currentPosition = transform.position;
            Vector2 clampedPosition = GroundMovementConstraint.ClampPointMovement(
                currentPosition,
                targetPosition,
                ReplayWallProbeRadius,
                sourceBoss.ObstacleMask.value,
                replayMovementColliders);
            return new Vector3(clampedPosition.x, clampedPosition.y, targetPosition.z);
        }

        private void SetReplayRootPosition(Vector3 position)
        {
            if (replayMovementBody != null)
            {
                replayMovementBody.position = position;
            }

            transform.position = position;
        }

        private void ApplyRecordedPlayerPosition(RecordedReplayFrame frame)
        {
            hasRecordedPlayerPosition = frame.HasPlayerPosition;
            recordedPlayerPosition = frame.PlayerPosition;
        }

        private void ApplyCurrentSourcePose()
        {
            if (sourceBoss == null)
            {
                return;
            }

            transform.rotation = sourceBoss.transform.rotation;
            transform.localScale = sourceBoss.transform.localScale;
            for (int i = 0; i < transformReplayBindings.Count; i++)
            {
                Transform sourceTransform = transformReplayBindings[i].Source;
                Transform targetTransform = transformReplayBindings[i].Target;
                if (sourceTransform == null || targetTransform == null)
                {
                    continue;
                }

                targetTransform.localPosition = sourceTransform.localPosition;
                targetTransform.localRotation = sourceTransform.localRotation;
                targetTransform.localScale = sourceTransform.localScale;
                targetTransform.gameObject.SetActive(sourceTransform.gameObject.activeSelf);
            }

            for (int i = 0; i < spriteReplayBindings.Count; i++)
            {
                SpriteRenderer sourceRenderer = spriteReplayBindings[i].Source;
                SpriteRenderer targetRenderer = spriteReplayBindings[i].Target;
                if (sourceRenderer != null && targetRenderer != null)
                {
                    ApplySpriteState(targetRenderer, new SpriteReplayState(
                        true,
                        sourceRenderer.sprite,
                        sourceRenderer.color,
                        sourceRenderer.flipX,
                        sourceRenderer.flipY,
                        sourceRenderer.enabled,
                        sourceRenderer.sortingLayerID,
                        sourceRenderer.sortingOrder));
                }
            }
        }

        private void ApplyTransformStates(IReadOnlyList<TransformReplayState> states)
        {
            int stateCount = Mathf.Min(transformReplayBindings.Count, states?.Count ?? 0);
            for (int i = 0; i < stateCount; i++)
            {
                Transform targetTransform = transformReplayBindings[i].Target;
                TransformReplayState state = states[i];
                if (!state.IsValid || targetTransform == null)
                {
                    continue;
                }

                targetTransform.localPosition = state.LocalPosition;
                targetTransform.localRotation = state.LocalRotation;
                targetTransform.localScale = state.LocalScale;
                targetTransform.gameObject.SetActive(state.ActiveSelf);
            }
        }

        private void ApplySpriteStates(IReadOnlyList<SpriteReplayState> states)
        {
            int stateCount = Mathf.Min(spriteReplayBindings.Count, states?.Count ?? 0);
            for (int i = 0; i < stateCount; i++)
            {
                ApplySpriteState(spriteReplayBindings[i].Target, states[i]);
            }
        }

        private static void ApplySpriteState(SpriteRenderer targetRenderer, SpriteReplayState state)
        {
            if (!state.IsValid || targetRenderer == null)
            {
                return;
            }

            targetRenderer.sprite = state.Sprite;
            targetRenderer.color = new Color(
                state.Color.r * HologramTint.r,
                state.Color.g * HologramTint.g,
                state.Color.b * HologramTint.b,
                state.Color.a * HologramTint.a);
            targetRenderer.flipX = state.FlipX;
            targetRenderer.flipY = state.FlipY;
            targetRenderer.enabled = state.Enabled;
            targetRenderer.sortingLayerID = state.SortingLayerId;
            targetRenderer.sortingOrder = state.SortingOrder;
        }

        private static string GetRelativePath(Transform root, Transform child)
        {
            List<string> names = new();
            Transform current = child;
            while (current != null && current != root)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private void DisableHologramAnimators()
        {
            Animator[] animators = GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null)
                {
                    animators[i].enabled = false;
                }
            }
        }

        private bool TryGetSourceWeapon(
            HackerRecallWeaponSelection selection,
            out HackerThrownWeapon weapon)
        {
            if (sourceBoss == null)
            {
                weapon = null;
                return false;
            }

            if (selection != HackerRecallWeaponSelection.RandomAvailable)
            {
                return sourceBoss.TryGetGroundedWeapon((HackerThrownWeaponType)selection, out weapon)
                    && weapon != null;
            }

            HackerThrownWeaponType[] types =
            {
                HackerThrownWeaponType.ThrowingWeapon,
                HackerThrownWeaponType.Bayonet,
                HackerThrownWeaponType.Gun,
                HackerThrownWeaponType.Sword
            };
            for (int i = 0; i < types.Length; i++)
            {
                if (sourceBoss.TryGetGroundedWeapon(types[i], out weapon) && weapon != null)
                {
                    return true;
                }
            }

            weapon = null;
            return false;
        }

        private static void DisableReplayWeaponPhysics(GameObject replayWeaponObject)
        {
            Collider2D[] colliders = replayWeaponObject.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }

            Rigidbody2D[] bodies = replayWeaponObject.GetComponentsInChildren<Rigidbody2D>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] != null)
                {
                    bodies[i].simulated = false;
                }
            }
        }

        private void ApplyHologramTint()
        {
            ApplyHologramTint(gameObject);
        }

        private static void ApplyHologramTint(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            SpriteRenderer[] renderers = target.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].color = HologramTint;
                }
            }
        }

        private void StripGameplayComponents()
        {
            EnemyStatusView[] statusViews = GetComponentsInChildren<EnemyStatusView>(true);
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int viewIndex = 0; viewIndex < statusViews.Length; viewIndex++)
            {
                EnemyStatusView statusView = statusViews[viewIndex];
                if (statusView == null)
                {
                    continue;
                }

                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    SpriteRenderer renderer = renderers[rendererIndex];
                    if (renderer != null && statusView.OwnsRenderer(renderer))
                    {
                        renderer.enabled = false;
                        excludedReplayRenderers.Add(renderer);
                    }
                }
            }

            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null)
                {
                    texts[i].enabled = false;
                    Destroy(texts[i]);
                }
            }

            Canvas[] canvases = GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null)
                {
                    canvases[i].enabled = false;
                    Destroy(canvases[i]);
                }
            }

            DisableAndDestroyComponents<EnemyStatusView>();
            DisableAndDestroyComponents<FloatingDamageView>();
            DisableAndDestroyComponents<ExecutionTarget>();

            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
            List<Collider2D> movementColliders = new();
            int environmentMask = LayerMask.GetMask("Wall") | (sourceBoss?.ObstacleMask.value ?? 0);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null)
                {
                    continue;
                }

                if (collider.transform == transform)
                {
                    collider.enabled = true;
                    collider.isTrigger = false;
                    collider.includeLayers = environmentMask;
                    collider.excludeLayers = ~environmentMask;
                    movementColliders.Add(collider);

                    continue;
                }

                collider.enabled = false;
                Destroy(collider);
            }

            replayMovementColliders = movementColliders.ToArray();
            replayMovementBody = GetComponent<Rigidbody2D>();

            Rigidbody2D[] bodies = GetComponentsInChildren<Rigidbody2D>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody2D targetBody = bodies[i];
                if (targetBody == null)
                {
                    continue;
                }

                if (targetBody == replayMovementBody && replayMovementColliders.Length > 0)
                {
                    targetBody.linearVelocity = Vector2.zero;
                    targetBody.angularVelocity = 0f;
                    targetBody.gravityScale = 0f;
                    targetBody.bodyType = RigidbodyType2D.Kinematic;
                    targetBody.constraints = RigidbodyConstraints2D.FreezeRotation;
                    targetBody.useFullKinematicContacts = true;
                    targetBody.simulated = true;
                    continue;
                }

                targetBody.simulated = false;
            }
        }

        private void DisableAndDestroyComponents<T>() where T : Component
        {
            T[] components = GetComponentsInChildren<T>(true);
            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                if (component == null)
                {
                    continue;
                }

                if (component is Behaviour behaviour)
                {
                    behaviour.enabled = false;
                }

                Destroy(component);
            }
        }

        private readonly struct TransformReplayBinding
        {
            public TransformReplayBinding(Transform source, Transform target)
            {
                Source = source;
                Target = target;
            }

            public Transform Source { get; }
            public Transform Target { get; }
        }

        private readonly struct SpriteReplayBinding
        {
            public SpriteReplayBinding(SpriteRenderer source, SpriteRenderer target)
            {
                Source = source;
                Target = target;
            }

            public SpriteRenderer Source { get; }
            public SpriteRenderer Target { get; }
        }

        private readonly struct TransformReplayState
        {
            public TransformReplayState(
                bool isValid,
                Vector3 localPosition,
                Quaternion localRotation,
                Vector3 localScale,
                bool activeSelf)
            {
                IsValid = isValid;
                LocalPosition = localPosition;
                LocalRotation = localRotation;
                LocalScale = localScale;
                ActiveSelf = activeSelf;
            }

            public bool IsValid { get; }
            public Vector3 LocalPosition { get; }
            public Quaternion LocalRotation { get; }
            public Vector3 LocalScale { get; }
            public bool ActiveSelf { get; }
        }

        private readonly struct SpriteReplayState
        {
            public SpriteReplayState(
                bool isValid,
                Sprite sprite,
                Color color,
                bool flipX,
                bool flipY,
                bool enabled,
                int sortingLayerId,
                int sortingOrder)
            {
                IsValid = isValid;
                Sprite = sprite;
                Color = color;
                FlipX = flipX;
                FlipY = flipY;
                Enabled = enabled;
                SortingLayerId = sortingLayerId;
                SortingOrder = sortingOrder;
            }

            public bool IsValid { get; }
            public Sprite Sprite { get; }
            public Color Color { get; }
            public bool FlipX { get; }
            public bool FlipY { get; }
            public bool Enabled { get; }
            public int SortingLayerId { get; }
            public int SortingOrder { get; }
        }

        private readonly struct RecordedReplayActionGroup
        {
            public RecordedReplayActionGroup(float time, IReadOnlyList<string> nodeIds)
            {
                Time = time;
                NodeIds = nodeIds;
            }

            public float Time { get; }
            public IReadOnlyList<string> NodeIds { get; }
        }

        private sealed class RecordedReplayFrame
        {
            public RecordedReplayFrame(
                float time,
                Vector3 rootPosition,
                Quaternion rootRotation,
                Vector3 rootLocalScale,
                bool hasPlayerPosition,
                Vector2 playerPosition,
                TransformReplayState[] transformStates,
                SpriteReplayState[] spriteStates)
            {
                Time = time;
                RootPosition = rootPosition;
                RootRotation = rootRotation;
                RootLocalScale = rootLocalScale;
                HasPlayerPosition = hasPlayerPosition;
                PlayerPosition = playerPosition;
                TransformStates = transformStates;
                SpriteStates = spriteStates;
            }

            public float Time { get; }
            public Vector3 RootPosition { get; }
            public Quaternion RootRotation { get; }
            public Vector3 RootLocalScale { get; }
            public bool HasPlayerPosition { get; }
            public Vector2 PlayerPosition { get; }
            public TransformReplayState[] TransformStates { get; }
            public SpriteReplayState[] SpriteStates { get; }
        }

        private Vector3 GetRestPosition()
        {
            return sourceBoss != null ? sourceBoss.transform.position : transform.position;
        }
    }
}
