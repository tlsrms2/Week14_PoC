using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Week14.Audio;
using Week14.Bootstrap;
using Week14.Enemy;

namespace Week14.Combat
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Week14/Combat/Conductor Execution Sequence")]
    public sealed class ConductorExecutionSequence : BossExecutionSequence
    {
        [Header("References")]
        [SerializeField] private BossExecutionStage stage;
        [SerializeField] private ParticleSystem aimChargeVfx;
        [SerializeField] private ExecutionChargeGatherVfx aimChargeGatherVfx;

        [Header("Drone Visuals")]
        [SerializeField, Tooltip("드론 프리팹입니다. 비어있으면 빈 오브젝트로 대체됩니다.")]
        private GameObject dronePrefab;
        [SerializeField, Min(0.1f)] private float droneScale = 0.8f;

        [Header("Phase 1 — Drone Vertical Line Sweep")]
        [SerializeField, Min(0.2f)] private float droneVerticalSpacing = 0.55f;
        [SerializeField, Min(0.5f)] private float droneMoveSpeed = 3.6f;
        [SerializeField, Min(0.5f)] private float droneSweepSeconds = 2.2f;
        [SerializeField, Min(1f)] private float droneStartLeftOffset = 5.5f;
        [SerializeField, Min(0f)] private float droneStartTopOffset = 1.8f;
        [SerializeField, BossGraphProjectileName] private string droneSweepProjectileName = "패링불가탄";
        [SerializeField, Min(0.4f)] private float droneVolleyIntervalSeconds = 0.4f;
        [FormerlySerializedAs("droneSweepPlayerRightDistance")]
        [SerializeField, Min(0f)] private float droneSweepPlayerLeftDistance = 4f;
        [SerializeField, Min(0f)] private float droneSweepPlayerUpDistance = 0.8f;
        [SerializeField, Min(0f)] private float droneSweepPlayerRollDelaySeconds = 0.3f;
        [SerializeField, Min(0.05f)] private float droneSweepPlayerRollSeconds = 0.45f;

        [Header("Phase 2 — Staff Formation & Parry")]
        [SerializeField, Min(0.15f)] private float staffLineSpacing = 0.38f;
        [SerializeField, Range(0.5f, 1f)] private float formationSpacingMultiplier = 0.84f;
        [SerializeField, Range(0.01f, 1f)] private float projectileSlowMultiplier = 0.05f;
        [SerializeField, Min(0.1f)] private float projectileSlowSafetySeconds = 10f;
        [SerializeField, Min(0f)] private float preParryHoldSeconds = 0.45f;
        [SerializeField, Min(0.1f)] private float executionImageSeconds = 4f;
        [SerializeField, Min(0.05f)] private float parryPlayerDescentSeconds = 0.18f;
        [SerializeField, Min(0.01f)] private float parryShotIntervalSeconds = 0.22f;
        [SerializeField, Min(1f)] private float parryShotLineLength = 22f;
        [SerializeField, Min(0f)] private float parryShotLineEndPadding = 1f;
        [SerializeField, Range(1f, 1.5f)] private float staffLineViewportEnd = 1.1f;

        [Header("Phase 3 — Aim Charge & Final Line")]
        [SerializeField, Min(0f)] private float aimChargeSeconds = 1.5f;
        [SerializeField, Min(0.05f)] private float finalPositionMoveSeconds = 0.25f;

        [Header("Camera")]
        [SerializeField, Range(0f, 1f)] private float wideCameraFocusWeight = 1f;
        [SerializeField, Range(0.1f, 1.5f)] private float wideCameraZoomMultiplier = 0.72f;
        [SerializeField, Min(0.01f)] private float wideCameraBlendSmoothTime = 0.2f;
        [SerializeField, Range(0f, 1f)] private float parryCameraFocusWeight = 1f;
        [SerializeField, Range(0.1f, 1f)] private float parryCameraZoomMultiplier = 0.42f;
        [SerializeField, Min(0.01f)] private float parryCameraBlendSmoothTime = 0.08f;
        [SerializeField, Range(0.1f, 1f)] private float preParryCameraZoomMultiplier = 0.38f;
        [SerializeField, Min(0.01f)] private float preParryCameraBlendSmoothTime = 0.12f;
        [SerializeField, Range(0.5f, 2f)] private float finalShotCameraZoomMultiplier = 1.15f;
        [SerializeField, Min(0.01f)] private float finalShotCameraBlendSmoothTime = 0.12f;

        [Header("Boss Animation")]
        [SerializeField, Range(0f, 1f)] private float bossAnimationSlowMultiplier = 0.5f;

        [Header("Final Blackout")]
        [SerializeField, Min(1f)] private float finalBlackoutTimeMultiplier = 1.75f;
        [SerializeField, Min(0.1f)] private float finalShotNoteMarkerScale = 1f;

        private const int DroneCount = 4;
        private readonly GameObject[] droneInstances = new GameObject[DroneCount];
        private readonly bool[] droneStopped = new bool[DroneCount];
        private readonly List<GameObject> staffShotLines = new();
        private readonly Dictionary<Animator, float> bossAnimatorSpeeds = new();
        private PlayerCombatController player;
        private Conductor conductor;
        private Rigidbody2D playerBody;
        private CameraFollow2D cameraFollow;
        private Coroutine playerMoveRoutine;
        private Coroutine droneSweepRoutine;
        private Coroutine finalShotNoteMarkerRoutine;
        private GameObject finalShotNoteMarker;
        private bool runtimePrepared;
        private bool slowActive;
        private bool finalShotImpacted;
        private bool droneOutlineVisibilityActive;
        private bool finalBlackoutActive;
        private bool playerSweepMoving;
        private float lockedStaffStartX;
        private float lockedStaffEndX;

        // ─── Abstract Property Overrides ────────────────────────────────

        public override bool CanPlay
        {
            get
            {
                ResolveReferences();
                return stage != null && stage.HasRequiredAnchors;
            }
        }

        public override Transform InitialCameraFocus =>
            stage != null && stage.WideCameraFocus != null
                ? stage.WideCameraFocus
                : conductor != null ? conductor.transform : transform;

        public override float WideCameraFocusWeight => wideCameraFocusWeight;
        public override float WideCameraZoomMultiplier => wideCameraZoomMultiplier;
        public override float WideCameraBlendSmoothTime => wideCameraBlendSmoothTime;
        public override float FinalBlackoutTimeMultiplier => finalBlackoutTimeMultiplier;
        public override bool UseParryColorForFinalShotLine => true;

        public override float ExpectedDurationSeconds =>
            droneSweepSeconds
            + preParryHoldSeconds
            + (parryPlayerDescentSeconds + parryShotIntervalSeconds) * DroneCount
            + finalPositionMoveSeconds
            + aimChargeSeconds;

        // ─── Unity Lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            ResolveReferences();
        }

        private void LateUpdate()
        {
            if (!runtimePrepared || player == null || conductor == null)
            {
                return;
            }

            MaintainSlow();
        }

        private void OnDisable()
        {
            CleanupRuntime(true);
            RestoreBossAnimatorSpeeds();
        }

        // ─── Abstract Method Overrides ──────────────────────────────────

        public override bool SupportsBoss(BossAI boss)
        {
            return boss is Conductor;
        }

        internal override bool Prepare(
            PlayerCombatController nextPlayer,
            BossAI nextBoss)
        {
            RestoreBossAnimatorSpeeds();
            finalShotImpacted = false;
            ResolveReferences();
            CleanupRuntime(false);

            if (nextPlayer == null || nextBoss is not Conductor nextConductor)
            {
                return false;
            }

            player = nextPlayer;
            conductor = nextConductor;
            if (!CanPlay)
            {
                player = null;
                conductor = null;
                return false;
            }

            cameraFollow = nextPlayer.CameraFollow;
            playerBody = nextPlayer.GetComponent<Rigidbody2D>();
            runtimePrepared = stage.PlaceActors(player, conductor);
            if (!runtimePrepared)
            {
                CleanupRuntime(false);
                return false;
            }

            AimPlayerAtBoss();
            conductor.FaceTowards(GetPlayerPosition());
            return true;
        }

        internal override IEnumerator PlayPrelude()
        {
            if (!runtimePrepared)
            {
                yield break;
            }

            bool preludeCompleted = false;
            try
            {
                // ── Phase 1: Drone Vertical Line Sweep (Score Lane Rush Style) ──
                FindAndCollectSceneDrones();
                BeginDroneOutlinePresentation();
                yield return RunDroneSweepPhase();
                if (!runtimePrepared)
                {
                    yield break;
                }

                // ── Phase 2: Move Player to Top Line Y (Line 0) & Slow Down ──
                Vector2 topParryPos = new(GetPlayerPosition().x, GetStaffLineY(0));
                yield return MovePlayerSegment(topParryPos, 0.35f);
                if (!runtimePrepared)
                {
                    yield break;
                }

                // Strictly record lockedStaffStartX from Player's Gun Muzzle for all 5 lines!
                lockedStaffStartX = player.RightFireOrigin != null
                    ? player.RightFireOrigin.position.x
                    : GetPlayerPosition().x;
                lockedStaffEndX =
                    ResolveLockedStaffLineEndX(lockedStaffStartX);

                BeginSlowPresentation();
                if (preParryHoldSeconds > 0f)
                {
                    yield return new WaitForSecondsRealtime(preParryHoldSeconds);
                }

                if (!runtimePrepared)
                {
                    yield break;
                }

                // ── Phase 3: Parry Shots (Sequential Drone Stop -> Diagonal Staff) ──
                yield return RunParryShotsPhase();
                if (!runtimePrepared)
                {
                    yield break;
                }

                // ── Phase 4: Downward Movement Only for 5th Line & Aim Charge ──
                yield return MoveToFinalPositionAndCharge();
                preludeCompleted = true;
            }
            finally
            {
                if (!preludeCompleted)
                {
                    CleanupRuntime(false);
                }
            }
        }

        internal override void Cancel()
        {
            CleanupRuntime(true);
            if (!finalShotImpacted)
            {
                RestoreBossAnimatorSpeeds();
            }
        }

        internal override void OnFinalBlackoutStarted(BossAI boss)
        {
            finalBlackoutActive = true;
            BeginFinalShotWideCamera();
            SetStaffShotLineColor(Color.white);
            SetAllDroneOutlinesVisible(true);
        }

        internal override void OnFinalShotImpact(BossAI boss)
        {
            finalShotImpacted = true;
            PlayFinalShotNoteMarker();

            // Draw 5th staff line connecting player's shot horizontally through boss head
            if (player != null && conductor != null)
            {
                // Unify start X to lockedStaffStartX so all 5 lines start at the exact same X!
                float line5Y = GetStaffLineY(4);
                Vector3 firePos = new(lockedStaffStartX, line5Y, 0f);
                Vector3 shotEnd = GetParryShotLineEnd(firePos);
                GameObject shotLine5 = ProjectileVfx.PlayShotLine(
                    firePos, shotEnd,
                    GetParryShotLineColor(),
                    60f, 0.04f, 75, null, false);
                if (shotLine5 != null)
                {
                    staffShotLines.Add(shotLine5);
                }
            }

        }

        internal override void OnFinalBlackoutFadeOutStarted(
            BossAI boss,
            float durationSeconds)
        {
            if (finalShotNoteMarker != null)
            {
                finalShotNoteMarkerRoutine = StartCoroutine(
                    FadeFinalShotNoteMarker(durationSeconds));
            }
        }

        internal override void OnFinalBlackoutEnded(BossAI boss)
        {
            finalBlackoutActive = false;
            StopFinalShotNoteMarker();
            DestroyStaffShotLines();
            EndDroneOutlinePresentation();
        }

        internal override void OnFinalDeathSequenceComplete(BossAI boss)
        {
            RestoreBossAnimatorSpeeds();
            finalShotImpacted = false;
        }

        // ─── Phase 1: Drone Vertical Sweep (Score Lane Rush Style) ───────

        private IEnumerator RunDroneSweepPhase()
        {
            if (!runtimePrepared || player == null)
            {
                yield break;
            }

            // Place drones in vertical line at player's far left-top
            Vector2 playerPos = GetPlayerPosition();
            float startX = playerPos.x - droneStartLeftOffset;
            float startTopY = playerPos.y + droneStartTopOffset;

            for (int i = 0; i < DroneCount; i++)
            {
                droneStopped[i] = false;
                if (droneInstances[i] != null)
                {
                    float y = startTopY
                        - i * GetCompressedDroneSpacing();
                    droneInstances[i].transform.position = new Vector3(startX, y, 0f);
                }
            }

            BossProjectileSettings projectile =
                conductor != null
                    ? conductor.ResolveMinionProjectileSettings(
                        droneSweepProjectileName)
                    : null;
            if (projectile == null)
            {
                Debug.LogWarning(
                    $"{nameof(ConductorExecutionSequence)}: 드론 그래프에서 " +
                    $"투사체 '{droneSweepProjectileName}'을 찾을 수 없습니다.",
                    this);
            }

            // Launch background sweep and downward firing
            droneSweepRoutine = StartCoroutine(
                AnimateDroneSweep(projectile));

            // 아래로 떨어지는 탄막 열보다 오른쪽을 유지하며 계속 이동한다.
            Vector2 dodgePos = new(
                playerPos.x - droneSweepPlayerLeftDistance,
                playerPos.y + droneSweepPlayerUpDistance);
            float rollDelaySeconds = Mathf.Min(
                Mathf.Max(0f, droneSweepPlayerRollDelaySeconds),
                Mathf.Max(0f, droneSweepSeconds));
            if (rollDelaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(rollDelaySeconds);
            }

            float rollSeconds = Mathf.Min(
                Mathf.Max(0.05f, droneSweepPlayerRollSeconds),
                Mathf.Max(
                    0.05f,
                    droneSweepSeconds - rollDelaySeconds));
            playerSweepMoving = true;
            player.Visual?.BeginCinematicMovement(dodgePos - playerPos);
            yield return MovePlayerRoll(dodgePos, rollSeconds);
            Vector2 bossDirection = conductor != null
                ? (Vector2)conductor.transform.position - GetPlayerPosition()
                : Vector2.right;
            player?.Visual?.EndCinematicMovement(bossDirection);
            AimPlayerAtBoss();
            playerSweepMoving = false;

            float remainingSweepSeconds =
                Mathf.Max(
                    0f,
                    droneSweepSeconds
                        - rollDelaySeconds
                        - rollSeconds);
            if (remainingSweepSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(remainingSweepSeconds);
            }
        }

        private IEnumerator AnimateDroneSweep(
            BossProjectileSettings projectile)
        {
            float elapsed = 0f;
            float nextFireTime = 0f;
            float fireEndTime = Mathf.Max(0f, droneSweepSeconds);
            MinionGraphProjectileFireSpec fireSpec =
                new MinionGraphProjectileFireSpec(
                    new MinionGraphProjectileOriginSpec(
                        MinionGraphProjectileOriginMode.ProjectileOrigin,
                        0f),
                    null,
                    new BossGraphEffectSettings(),
                    null)
                    .WithFixedDirection(Vector2.down)
                    .WithProjectilePathIndicatorSuppressed();

            while (runtimePrepared && elapsed < droneSweepSeconds + 4f)
            {
                float delta = Time.unscaledDeltaTime;

                for (int i = 0; i < DroneCount; i++)
                {
                    if (droneInstances[i] == null || droneStopped[i])
                    {
                        continue;
                    }

                    // Move rightward (+X)
                    Vector3 pos = droneInstances[i].transform.position;
                    pos.x += droneMoveSpeed * delta;
                    droneInstances[i].transform.position = pos;
                }

                // Lane Rush Special과 동일한 등록 투사체를 아래로 발사한다.
                while (projectile != null
                    && nextFireTime < fireEndTime
                    && elapsed >= nextFireTime)
                {
                    float projectileColumnX = GetDroneColumnX();
                    for (int i = 0; i < DroneCount; i++)
                    {
                        if (droneInstances[i] == null || droneStopped[i])
                        {
                            continue;
                        }

                        Minion minion =
                            droneInstances[i].GetComponent<Minion>();
                        EnemyProjectile spawned =
                            minion?.FireOnceForCinematic(
                            projectile,
                            fireSpec,
                            i);
                        spawned?.ConfigureExecutionPauseIgnored(true);
                        spawned?.ConfigurePathIndicatorDelayedUntilLaunch(
                            true);
                        AlignProjectileToVerticalColumn(
                            spawned,
                            projectileColumnX);
                    }

                    nextFireTime +=
                        Mathf.Max(
                            0.4f,
                            droneVolleyIntervalSeconds);
                }

                elapsed += delta;
                yield return null;
            }

            droneSweepRoutine = null;
        }

        // ─── Phase 2 & 3: Slow & Parry Shots ─────────────────────────────

        private float GetDroneColumnX()
        {
            for (int i = 0; i < DroneCount; i++)
            {
                if (droneInstances[i] != null)
                {
                    return droneInstances[i].transform.position.x;
                }
            }

            return 0f;
        }

        private static void AlignProjectileToVerticalColumn(
            EnemyProjectile projectile,
            float columnX)
        {
            if (projectile == null)
            {
                return;
            }

            Vector3 position = projectile.transform.position;
            position.x = columnX;
            projectile.transform.position = position;
            Rigidbody2D body = projectile.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = position;
            }
        }

        private void BeginSlowPresentation()
        {
            slowActive = true;
            EnemyTimeScale.SetTemporary(projectileSlowMultiplier, projectileSlowSafetySeconds);
            ApplyBossAnimatorSlow();

            cameraFollow?.BeginCinematicFocus(
                player != null ? player.transform : null,
                1f,
                preParryCameraZoomMultiplier,
                preParryCameraBlendSmoothTime);

            player?.PlayExecutionImageForCinematic(executionImageSeconds);

            SoundManager.PlaySfx(SoundEvent.Execution_ConductorPreParry);
        }

        private IEnumerator RunParryShotsPhase()
        {
            if (!runtimePrepared || player == null)
            {
                yield break;
            }

            // Lock player's Body X to match lockedStaffStartX offset
            float playerBodyX = GetPlayerPosition().x;

            for (int i = 0; i < DroneCount && runtimePrepared; i++)
            {
                float targetY = GetStaffLineY(i);

                // Move player down to line i height (keeping locked X position)
                if (i > 0)
                {
                    Vector2 playerTarget = new(playerBodyX, targetY);
                    yield return MovePlayerSegment(playerTarget, parryPlayerDescentSeconds);
                    if (!runtimePrepared)
                    {
                        yield break;
                    }
                }

                // Aim player rightward
                Vector2 aimDirection = Vector2.right;
                player.Visual?.BeginExecutionAimVisual(aimDirection);
                player.Visual?.SetBodyAimDirection(aimDirection);

                // Stop drone i at current moving position!
                droneStopped[i] = true;
                Vector3 dronePos = droneInstances[i] != null
                    ? droneInstances[i].transform.position
                    : new Vector3(playerBodyX + 3f, targetY, 0f);

                // Lock drone Y to staff line Y for clean horizontal staff line
                dronePos.y = targetY;
                if (droneInstances[i] != null)
                {
                    droneInstances[i].transform.position = dronePos;
                }

                // Camera focus on drone
                Transform focusProxy = stage != null ? stage.ProjectileFocusProxy : null;
                if (focusProxy != null)
                {
                    focusProxy.position = dronePos;
                    cameraFollow?.BeginCinematicFocus(
                        focusProxy,
                        parryCameraFocusWeight,
                        parryCameraZoomMultiplier,
                        parryCameraBlendSmoothTime);
                }

                // Play shot sound
                SoundManager.PlaySfx(SoundEvent.Player_NormalShot);

                // Draw persistent staff line using lockedStaffStartX so all lines start at exact same X
                Vector3 firePosition = new(lockedStaffStartX, targetY, 0f);
                Vector3 shotEnd = GetParryShotLineEnd(firePosition);
                GameObject shotLine = ProjectileVfx.PlayShotLine(
                    firePosition, shotEnd,
                    GetParryShotLineColor(),
                    60f, 0.04f, 73, null, false);
                if (shotLine != null)
                {
                    staffShotLines.Add(shotLine);
                }

                // Disable/highlight drone
                DisableDrone(i);

                // Camera impact
                cameraFollow?.PlayImpact(Vector2.right, 0.06f, 0.1f, 0.08f);

                // Interval between parry shots
                if (parryShotIntervalSeconds > 0f)
                {
                    yield return new WaitForSecondsRealtime(parryShotIntervalSeconds);
                }
                else
                {
                    yield return null;
                }
            }
        }

        // ─── Phase 4: Downward Only Move & Aim Charge ────────────────────

        private IEnumerator MoveToFinalPositionAndCharge()
        {
            if (!runtimePrepared || player == null || conductor == null)
            {
                yield break;
            }

            // Move ONLY DOWNWARD (do not move forward X).
            // Match Player's Gun Muzzle (RightFireOrigin) Y exactly with Boss Head Y!
            float bossHeadY = GetBossHeadY();
            float muzzleLocalY = GetPlayerMuzzleLocalY();
            float targetPlayerY = bossHeadY - muzzleLocalY;

            Vector2 finalPosition = new(GetPlayerPosition().x, targetPlayerY);

            yield return MovePlayerSegment(finalPosition, finalPositionMoveSeconds);
            if (!runtimePrepared)
            {
                yield break;
            }

            BeginAimCharge();
            if (aimChargeSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(aimChargeSeconds);
            }
        }

        private void BeginAimCharge()
        {
            if (player == null || conductor == null)
            {
                return;
            }

            // Restore enemy time scale for charge phase
            RestoreEnemyTimeScale();
            slowActive = false;

            // Wide camera
            if (stage != null && stage.WideCameraFocus != null)
            {
                cameraFollow?.BeginCinematicFocus(
                    stage.WideCameraFocus,
                    wideCameraFocusWeight,
                    wideCameraZoomMultiplier,
                    wideCameraBlendSmoothTime);
            }

            // Aim at boss
            Vector2 aimDirection = Vector2.right;
            player.Visual?.BeginExecutionAimVisual(aimDirection);
            player.Visual?.SetBodyAimDirection(aimDirection);

            // Charge VFX
            if (aimChargeVfx != null)
            {
                aimChargeVfx.Play(true);
            }

            aimChargeGatherVfx?.Play(player.RightFireOrigin);

            SoundManager.PlaySfx(SoundEvent.Execution_ConductorAimCharge);
        }

        // ─── Helper Calculations ─────────────────────────────────────────

        private void BeginFinalShotWideCamera()
        {
            if (cameraFollow == null)
            {
                return;
            }

            Transform focusTarget = stage != null
                ? stage.ProjectileFocusProxy
                : null;
            if (focusTarget != null)
            {
                Vector2 min = conductor != null
                    ? (Vector2)conductor.transform.position
                    : GetPlayerPosition();
                Vector2 max = min;

                if (player != null)
                {
                    Vector2 playerPosition = GetPlayerPosition();
                    min = Vector2.Min(min, playerPosition);
                    max = Vector2.Max(max, playerPosition);
                }

                for (int i = 0; i < DroneCount; i++)
                {
                    if (droneInstances[i] == null)
                    {
                        continue;
                    }

                    Vector2 dronePosition =
                        droneInstances[i].transform.position;
                    min = Vector2.Min(min, dronePosition);
                    max = Vector2.Max(max, dronePosition);
                }

                focusTarget.position = (min + max) * 0.5f;
            }
            else if (stage != null)
            {
                focusTarget = stage.WideCameraFocus;
            }

            cameraFollow.BeginCinematicFocus(
                focusTarget != null
                    ? focusTarget
                    : conductor != null
                        ? conductor.transform
                        : transform,
                1f,
                finalShotCameraZoomMultiplier,
                finalShotCameraBlendSmoothTime);
        }

        private void PlayFinalShotNoteMarker()
        {
            StopFinalShotNoteMarker();
            if (conductor == null)
            {
                return;
            }

            float diameter = staffLineSpacing
                * formationSpacingMultiplier
                * finalShotNoteMarkerScale
                * 0.9f;
            finalShotNoteMarker = ProjectileVfx.PlayCircleFlash(
                GetBossHeadPosition(),
                diameter,
                Color.white,
                74);
            if (finalShotNoteMarker != null)
            {
                SpriteRenderer renderer =
                    finalShotNoteMarker.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.color = Color.white;
                }
            }
        }

        private IEnumerator FadeFinalShotNoteMarker(float seconds)
        {
            float duration = Mathf.Max(0f, seconds);
            SpriteRenderer renderer = finalShotNoteMarker != null
                ? finalShotNoteMarker.GetComponent<SpriteRenderer>()
                : null;
            if (duration > 0f && renderer != null)
            {
                for (float elapsed = 0f;
                     elapsed < duration && renderer != null;
                     elapsed += Time.unscaledDeltaTime)
                {
                    Color color = Color.white;
                    color.a = 1f - Mathf.Clamp01(elapsed / duration);
                    renderer.color = color;
                    yield return null;
                }
            }

            if (finalShotNoteMarker != null)
            {
                Destroy(finalShotNoteMarker);
            }

            finalShotNoteMarker = null;
            finalShotNoteMarkerRoutine = null;
        }

        private void StopFinalShotNoteMarker()
        {
            if (finalShotNoteMarkerRoutine != null)
            {
                StopCoroutine(finalShotNoteMarkerRoutine);
                finalShotNoteMarkerRoutine = null;
            }

            if (finalShotNoteMarker != null)
            {
                Destroy(finalShotNoteMarker);
                finalShotNoteMarker = null;
            }
        }

        private float GetStaffLineY(int index)
        {
            float bossHeadY = GetBossHeadY();
            // Line 4 is at boss head Y
            // Lines 0..3 are spaced upwards by staffLineSpacing * (4 - index)
            return bossHeadY
                + (4 - index)
                * staffLineSpacing
                * formationSpacingMultiplier;
        }

        private float GetCompressedDroneSpacing()
        {
            return droneVerticalSpacing * formationSpacingMultiplier;
        }

        private float GetBossHeadY()
        {
            return GetBossHeadPosition().y;
        }

        private Vector3 GetBossHeadPosition()
        {
            if (conductor == null)
            {
                return Vector3.zero;
            }

            Transform[] bossTransforms =
                conductor.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < bossTransforms.Length; i++)
            {
                if (bossTransforms[i] != null
                    && bossTransforms[i].name == "Head")
                {
                    return bossTransforms[i].position;
                }
            }

            return conductor.transform.position;
        }

        private float GetPlayerMuzzleLocalY()
        {
            if (player == null) return 0f;
            Transform fireOrigin = player.RightFireOrigin;
            if (fireOrigin == null) return 0f;
            return fireOrigin.position.y - GetPlayerPosition().y;
        }

        // ─── Drone Management ───────────────────────────────────────

        private void FindAndCollectSceneDrones()
        {
            List<GameObject> candidates = new();
            IReadOnlyList<Minion> controlledMinions =
                conductor != null
                    ? conductor.GetControlledMinionsForGraph()
                    : null;

            if (controlledMinions != null)
            {
                for (int i = 0; i < controlledMinions.Count; i++)
                {
                    Minion minion = controlledMinions[i];
                    if (minion != null
                        && minion.gameObject.activeInHierarchy)
                    {
                        candidates.Add(minion.gameObject);
                    }
                }
            }

            // Fallback: If less than 4 scene minions, find by type or name
            if (candidates.Count < DroneCount)
            {
                Minion[] sceneMinions = Object.FindObjectsByType<Minion>(FindObjectsSortMode.None);
                for (int i = 0; i < sceneMinions.Length; i++)
                {
                    if (sceneMinions[i] != null
                        && sceneMinions[i].gameObject.activeInHierarchy
                        && ReferenceEquals(
                            sceneMinions[i].Owner,
                            conductor)
                        && !candidates.Contains(sceneMinions[i].gameObject))
                    {
                        candidates.Add(sceneMinions[i].gameObject);
                    }
                }
            }

            Vector2 playerPos = GetPlayerPosition();

            for (int i = 0; i < DroneCount; i++)
            {
                if (i < candidates.Count && candidates[i] != null)
                {
                    droneInstances[i] = candidates[i];
                    // Stop any ongoing minion routines or physics during sequence
                    Minion minionComponent = droneInstances[i].GetComponent<Minion>();
                    if (minionComponent != null)
                    {
                        minionComponent.StopAllCoroutines();
                    }
                    Rigidbody2D rb = droneInstances[i].GetComponent<Rigidbody2D>();
                    if (rb != null)
                    {
                        rb.linearVelocity = Vector2.zero;
                        rb.bodyType = RigidbodyType2D.Kinematic;
                    }
                }
                else
                {
                    // Fallback proxy if fewer than 4 minions exist in map
                    float startX = playerPos.x - droneStartLeftOffset;
                    float startTopY = playerPos.y + droneStartTopOffset;
                    Vector2 spawnPos = new(
                        startX,
                        startTopY - i * GetCompressedDroneSpacing());

                    GameObject drone;
                    if (dronePrefab != null)
                    {
                        drone = Instantiate(dronePrefab, spawnPos, Quaternion.identity);
                    }
                    else
                    {
                        drone = new GameObject($"Drone_Proxy_{i}");
                        drone.transform.position = (Vector3)spawnPos;
                    }

                    drone.transform.localScale = Vector3.one * droneScale;
                    droneInstances[i] = drone;
                }
            }
        }

        private void DisableDrone(int index)
        {
            if (index < 0 || index >= DroneCount)
            {
                return;
            }

            if (droneInstances[index] != null)
            {
                Minion minion = droneInstances[index].GetComponent<Minion>();
                if (minion != null && conductor != null)
                {
                    conductor.SetExecutionMinionOutlineVisible(minion, false);
                }
                else
                {
                    SetSingleDroneOutlineActive(
                        droneInstances[index],
                        false,
                        0f);
                }
            }
        }

        private void BeginDroneOutlinePresentation()
        {
            if (conductor != null)
            {
                conductor.BeginExecutionMinionOutlineVisibility();
                droneOutlineVisibilityActive = true;
            }

            SetAllDroneOutlinesVisible(true);
        }

        private void SetAllDroneOutlinesVisible(bool visible)
        {
            for (int i = 0; i < DroneCount; i++)
            {
                GameObject drone = droneInstances[i];
                if (drone == null)
                {
                    continue;
                }

                Minion minion = drone.GetComponent<Minion>();
                if (minion != null && conductor != null)
                {
                    conductor.SetExecutionMinionOutlineVisible(minion, visible);
                }
                else
                {
                    SetSingleDroneOutlineActive(
                        drone,
                        visible,
                        visible ? 1f : 0f);
                }
            }
        }

        private void EndDroneOutlinePresentation()
        {
            if (droneOutlineVisibilityActive && conductor != null)
            {
                conductor.EndExecutionMinionOutlineVisibility();
            }

            droneOutlineVisibilityActive = false;
            SetDroneOutlinesActive(false, 0f);
        }

        private void SetDroneOutlinesActive(bool active, float alpha = 1f)
        {
            for (int i = 0; i < DroneCount; i++)
            {
                if (droneInstances[i] != null)
                {
                    SetSingleDroneOutlineActive(droneInstances[i], active, alpha);
                }
            }
        }

        private static void SetSingleDroneOutlineActive(GameObject droneGo, bool active, float alpha)
        {
            if (droneGo == null) return;

            SpriteRenderer[] renderers = droneGo.GetComponentsInChildren<SpriteRenderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                if (renderers[r] != null && renderers[r].name.Contains("Outline"))
                {
                    renderers[r].gameObject.SetActive(active);
                    Color color = renderers[r].color;
                    color.a = alpha;
                    renderers[r].color = color;
                }
            }
        }

        private void DestroyAllDrones()
        {
            for (int i = 0; i < DroneCount; i++)
            {
                if (droneInstances[i] != null)
                {
                    // Restore minion physics if real minion
                    Rigidbody2D rb = droneInstances[i].GetComponent<Rigidbody2D>();
                    if (rb != null)
                    {
                        rb.bodyType = RigidbodyType2D.Dynamic;
                    }

                    // Destroy only dynamically instantiated fallback proxies
                    if (droneInstances[i].name.StartsWith("Drone_Proxy_"))
                    {
                        Destroy(droneInstances[i]);
                    }
                    droneInstances[i] = null;
                }
            }
        }

        // ─── Player Movement Helpers ────────────────────────────────

        private Color GetParryShotLineColor()
        {
            if (finalBlackoutActive)
            {
                return Color.white;
            }

            return player != null && player.Config != null
                ? player.Config.ParryEffectColor
                : new Color(0.2f, 0.65f, 1f, 0.45f);
        }

        private void SetStaffShotLineColor(Color color)
        {
            for (int i = 0; i < staffShotLines.Count; i++)
            {
                GameObject lineObject = staffShotLines[i];
                if (lineObject == null)
                {
                    continue;
                }

                LineRenderer line = lineObject.GetComponent<LineRenderer>();
                if (line != null)
                {
                    line.startColor = color;
                    line.endColor = color;
                }
            }
        }

        private void DestroyStaffShotLines()
        {
            for (int i = 0; i < staffShotLines.Count; i++)
            {
                if (staffShotLines[i] != null)
                {
                    Destroy(staffShotLines[i]);
                }
            }

            staffShotLines.Clear();
        }

        private Vector3 GetParryShotLineEnd(Vector3 start)
        {
            float endX = Mathf.Max(
                lockedStaffEndX,
                start.x + Mathf.Max(1f, parryShotLineLength));
            return new Vector3(endX, start.y, start.z);
        }

        private float ResolveLockedStaffLineEndX(float startX)
        {
            float endX = startX + Mathf.Max(1f, parryShotLineLength);
            Camera targetCamera = Camera.main;
            if (targetCamera != null)
            {
                float cameraDepth = Mathf.Abs(
                    targetCamera.transform.position.z);
                float viewportEndX = targetCamera.ViewportToWorldPoint(
                    new Vector3(
                        staffLineViewportEnd,
                        0.5f,
                        cameraDepth)).x;
                endX = Mathf.Max(endX, viewportEndX);
            }

            for (int i = 0; i < DroneCount; i++)
            {
                GameObject drone = droneInstances[i];
                if (drone == null)
                {
                    continue;
                }

                SpriteRenderer[] renderers =
                    drone.GetComponentsInChildren<SpriteRenderer>(true);
                if (renderers.Length == 0)
                {
                    endX = Mathf.Max(
                        endX,
                        drone.transform.position.x
                            + parryShotLineEndPadding);
                    continue;
                }

                for (int rendererIndex = 0;
                     rendererIndex < renderers.Length;
                     rendererIndex++)
                {
                    SpriteRenderer renderer = renderers[rendererIndex];
                    if (renderer != null)
                    {
                        endX = Mathf.Max(
                            endX,
                            renderer.bounds.max.x
                                + parryShotLineEndPadding);
                    }
                }
            }

            return endX;
        }

        private IEnumerator MovePlayerRoll(Vector2 target, float seconds)
        {
            if (player == null)
            {
                yield break;
            }

            float duration = Mathf.Max(0.05f, seconds);
            player.Visual?.PlayCinematicRoll(
                duration,
                target - GetPlayerPosition());
            yield return MovePlayerSegment(target, duration);
        }

        private IEnumerator MovePlayerSegment(Vector2 target, float seconds)
        {
            if (player == null)
            {
                yield break;
            }

            float duration = Mathf.Max(0.05f, seconds);
            Vector2 start = GetPlayerPosition();

            for (float elapsed = 0f;
                 runtimePrepared && player != null && elapsed < duration;
                 elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - (1f - progress) * (1f - progress);
                SetPlayerPosition(Vector2.LerpUnclamped(start, target, eased));
                yield return null;
            }

            if (runtimePrepared && player != null)
            {
                SetPlayerPosition(target);
            }
        }

        private Vector2 GetPlayerPosition()
        {
            if (playerBody != null)
            {
                return playerBody.position;
            }

            return player != null ? (Vector2)player.transform.position : Vector2.zero;
        }

        private void SetPlayerPosition(Vector2 position)
        {
            if (playerBody != null)
            {
                playerBody.linearVelocity = Vector2.zero;
                playerBody.position = position;
                return;
            }

            if (player == null)
            {
                return;
            }

            Vector3 worldPosition = player.transform.position;
            worldPosition.x = position.x;
            worldPosition.y = position.y;
            player.transform.position = worldPosition;
        }

        private void AimPlayerAtBoss()
        {
            if (player == null || conductor == null)
            {
                return;
            }

            Vector2 direction = (Vector2)conductor.transform.position - GetPlayerPosition();
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.right;
            }

            player.Visual?.BeginExecutionAimVisual(direction);
            player.Visual?.SetBodyAimDirection(direction);
            player.Visual?.SetLeftArmAimDirection(direction);
        }

        // ─── Staff Line Positions ───────────────────────────────────

        /// <summary>
        /// 오선보 라인 위치를 계산합니다.
        /// index 0~3: 드론 4기 (대각선 계단식 배치, index 0이 최상단-좌측, index 3이 4번째-우측)
        /// index 4: 5번째 오선보 줄 (보스 머리/몸체 Y 높이)
        /// </summary>
        private Vector2 GetStaffLinePosition(int index)
        {
            if (conductor == null)
            {
                return Vector2.zero;
            }

            Vector2 bossPos = conductor.transform.position;
            float y = GetStaffLineY(index);
            float x = bossPos.x - 3f;
            return new Vector2(x, y);
        }

        // ─── Boss Animation Slow ────────────────────────────────────

        private void ApplyBossAnimatorSlow()
        {
            if (conductor == null)
            {
                return;
            }

            Transform bossRoot = conductor.BodyRoot != null
                ? conductor.BodyRoot
                : conductor.transform;
            Animator[] animators = bossRoot.GetComponentsInChildren<Animator>(true);

            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null || bossAnimatorSpeeds.ContainsKey(animator))
                {
                    continue;
                }

                bossAnimatorSpeeds[animator] = animator.speed;
                animator.speed *= bossAnimationSlowMultiplier;
            }
        }

        private void RestoreBossAnimatorSpeeds()
        {
            foreach (KeyValuePair<Animator, float> entry in bossAnimatorSpeeds)
            {
                if (entry.Key != null)
                {
                    entry.Key.speed = entry.Value;
                }
            }

            bossAnimatorSpeeds.Clear();
        }

        // ─── Slow Maintenance ───────────────────────────────────────

        private void MaintainSlow()
        {
            if (!slowActive
                || Mathf.Approximately(
                    EnemyTimeScale.Current,
                    projectileSlowMultiplier))
            {
                return;
            }

            EnemyTimeScale.SetTemporary(
                projectileSlowMultiplier,
                projectileSlowSafetySeconds);
        }

        // ─── Cleanup ────────────────────────────────────────────────

        private void CleanupRuntime(bool endCameraFocus)
        {
            bool hadActiveRuntime = runtimePrepared
                || player != null
                || droneSweepRoutine != null;
            runtimePrepared = false;
            slowActive = false;
            finalBlackoutActive = false;
            lockedStaffEndX = 0f;

            StopDroneSweepRoutine();
            StopFinalShotNoteMarker();
            EndDroneOutlinePresentation();
            if (playerSweepMoving && player != null)
            {
                player.Visual?.EndCinematicMovement(Vector2.right);
            }

            playerSweepMoving = false;

            if (playerMoveRoutine != null)
            {
                StopCoroutine(playerMoveRoutine);
                playerMoveRoutine = null;
            }

            if (hadActiveRuntime)
            {
                RestoreEnemyTimeScale();
            }

            // Destroy drones
            DestroyAllDrones();

            DestroyStaffShotLines();

            // VFX cleanup
            if (aimChargeVfx != null)
            {
                aimChargeVfx.Stop(
                    true,
                    endCameraFocus
                        ? ParticleSystemStopBehavior.StopEmittingAndClear
                        : ParticleSystemStopBehavior.StopEmitting);
            }

            aimChargeGatherVfx?.Stop(endCameraFocus);

            if (endCameraFocus && stage != null && cameraFollow != null)
            {
                cameraFollow.EndCinematicFocusIfTarget(stage.ProjectileFocusProxy);
            }

            player = null;
            conductor = null;
            playerBody = null;
            cameraFollow = null;
        }

        private void StopDroneSweepRoutine()
        {
            if (droneSweepRoutine != null)
            {
                StopCoroutine(droneSweepRoutine);
                droneSweepRoutine = null;
            }
        }

        private static void RestoreEnemyTimeScale()
        {
            EnemyTimeScale.SetTemporary(1f, 0f);
        }

        // ─── Resolve References ─────────────────────────────────────

        private void ResolveReferences()
        {
            stage ??= GetComponent<BossExecutionStage>();
            aimChargeGatherVfx ??= GetComponent<ExecutionChargeGatherVfx>();
            if (aimChargeGatherVfx == null && Application.isPlaying)
            {
                aimChargeGatherVfx = gameObject.AddComponent<ExecutionChargeGatherVfx>();
            }
        }
    }
}
