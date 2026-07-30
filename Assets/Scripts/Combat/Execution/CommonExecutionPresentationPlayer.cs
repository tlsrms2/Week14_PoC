using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Audio;
using Week14.Bootstrap;
using Week14.Enemy;

namespace Week14.Combat
{
    internal sealed class CommonExecutionPresentationPlayer
    {
        private readonly PlayerCombatController.PlayerCombatContext context;
        private readonly PlayerExecutionPresentation presentation;
        private readonly List<Coroutine> delayedCueRoutines = new();
        private CommonExecutionPresentationProfile profile;
        private BossAI boss;
        private Coroutine slowMotionRoutine;
        private Coroutine cameraHoldRoutine;
        private Coroutine finalShotTrackRoutine;
        private bool slowMotionActive;
        private bool hasFinalShotPath;
        private float previousTimeScale = 1f;
        private float appliedTimeScale = 1f;
        private Vector3 finalShotStart;
        private Vector3 finalShotEnd;
        private int cameraRevision;

        internal CommonExecutionPresentationPlayer(
            PlayerCombatController.PlayerCombatContext context,
            PlayerExecutionPresentation presentation)
        {
            this.context = context;
            this.presentation = presentation;
        }

        internal void Begin(CommonExecutionPresentationProfile nextProfile, BossAI nextBoss)
        {
            Stop();
            profile = nextProfile;
            boss = nextBoss;
        }

        internal void PrepareFinalShot(Vector3 start, Vector3 end)
        {
            finalShotStart = start;
            finalShotEnd = end;
            finalShotStart.z = 0f;
            finalShotEnd.z = 0f;
            hasFinalShotPath = true;
        }

        internal void Trigger(CommonExecutionCuePoint cuePoint, int flourishShotIndex = -1)
        {
            if (profile == null || boss == null)
            {
                return;
            }

            CommonExecutionPresentationCue[] cues = profile.Cues;
            for (int i = 0; i < cues.Length; i++)
            {
                CommonExecutionPresentationCue cue = cues[i];
                if (cue == null || !cue.Matches(cuePoint, flourishShotIndex))
                {
                    continue;
                }

                Coroutine routine = context.CoroutineHost.StartCoroutine(PlayCue(cue));
                delayedCueRoutines.Add(routine);
            }
        }

        internal void Stop()
        {
            for (int i = 0; i < delayedCueRoutines.Count; i++)
            {
                if (delayedCueRoutines[i] != null)
                {
                    context.CoroutineHost.StopCoroutine(delayedCueRoutines[i]);
                }
            }

            delayedCueRoutines.Clear();
            if (cameraHoldRoutine != null)
            {
                context.CoroutineHost.StopCoroutine(cameraHoldRoutine);
                cameraHoldRoutine = null;
            }

            StopFinalShotTracking();
            cameraRevision++;
            StopSlowMotion();
            hasFinalShotPath = false;
            profile = null;
            boss = null;
        }

        private IEnumerator PlayCue(CommonExecutionPresentationCue cue)
        {
            if (cue.DelaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(cue.DelaySeconds);
            }

            if (profile == null || boss == null)
            {
                yield break;
            }

            switch (cue.CueType)
            {
                case CommonExecutionCueType.Sound:
                    PlaySound(cue);
                    break;

                case CommonExecutionCueType.SlowMotion:
                    PlaySlowMotion(cue);
                    break;

                case CommonExecutionCueType.CameraFocus:
                    PlayCameraFocus(cue);
                    break;
            }
        }

        private static void PlaySound(CommonExecutionPresentationCue cue)
        {
            SoundManager.PlaySfx(cue.SoundEvent);
        }

        private void PlaySlowMotion(CommonExecutionPresentationCue cue)
        {
            StopSlowMotion();
            if (cue.SlowDurationSeconds <= 0f)
            {
                return;
            }

            previousTimeScale = Time.timeScale;
            appliedTimeScale = cue.SlowTimeScale;
            Time.timeScale = appliedTimeScale;
            slowMotionActive = true;
            slowMotionRoutine = context.CoroutineHost.StartCoroutine(
                RestoreSlowMotionAfter(cue.SlowDurationSeconds));
        }

        private IEnumerator RestoreSlowMotionAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            slowMotionRoutine = null;
            RestoreSlowMotion();
        }

        private void StopSlowMotion()
        {
            if (slowMotionRoutine != null)
            {
                context.CoroutineHost.StopCoroutine(slowMotionRoutine);
                slowMotionRoutine = null;
            }

            RestoreSlowMotion();
        }

        private void RestoreSlowMotion()
        {
            if (!slowMotionActive)
            {
                return;
            }

            if (Mathf.Approximately(Time.timeScale, appliedTimeScale))
            {
                Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
            }

            slowMotionActive = false;
        }

        private void PlayCameraFocus(CommonExecutionPresentationCue cue)
        {
            CameraFollow2D camera = context.CameraFollow;
            if (camera == null)
            {
                return;
            }

            if (cameraHoldRoutine != null)
            {
                context.CoroutineHost.StopCoroutine(cameraHoldRoutine);
                cameraHoldRoutine = null;
            }

            int revision = ++cameraRevision;
            Transform focusTarget = ResolveFocusTarget(cue);
            if (focusTarget == null)
            {
                return;
            }

            camera.BeginCinematicFocus(
                focusTarget,
                cue.CameraFocusWeight,
                cue.CameraZoomMultiplier,
                cue.CameraBlendSmoothTime);

            float restoreDelay = cue.CameraHoldSeconds;
            if (cue.FocusTarget == CommonExecutionFocusTarget.FinalShotProjectile)
            {
                StopFinalShotTracking();
                finalShotTrackRoutine = context.CoroutineHost.StartCoroutine(
                    TrackFinalShot(cue.FinalShotTravelSeconds, revision));
                restoreDelay += cue.FinalShotTravelSeconds;
            }

            if (cue.RestoreDefaultCameraAfterHold)
            {
                cameraHoldRoutine = context.CoroutineHost.StartCoroutine(
                    RestoreDefaultCameraAfter(
                        restoreDelay,
                        revision,
                        cue.CameraBlendSmoothTime));
            }
        }

        private Transform ResolveFocusTarget(CommonExecutionPresentationCue cue)
        {
            Transform player = context.PlayerTransform;
            Transform bossRoot = boss != null
                ? boss.BodyRoot != null ? boss.BodyRoot : boss.transform
                : null;
            switch (cue.FocusTarget)
            {
                case CommonExecutionFocusTarget.Player:
                    return player;

                case CommonExecutionFocusTarget.Boss:
                    return bossRoot;

                case CommonExecutionFocusTarget.BossOffset:
                {
                    if (bossRoot == null)
                    {
                        return null;
                    }

                    Vector3 focusPosition = bossRoot.position + (Vector3)cue.BossFocusOffset;
                    presentation.SetExecutionFocusPoint(focusPosition);
                    return presentation.ExecutionFocusPoint;
                }

                case CommonExecutionFocusTarget.FinalShotProjectile:
                    if (!hasFinalShotPath)
                    {
                        return null;
                    }

                    presentation.SetExecutionFocusPoint(finalShotStart);
                    return presentation.ExecutionFocusPoint;

                default:
                {
                    if (player == null || bossRoot == null)
                    {
                        return bossRoot != null ? bossRoot : player;
                    }

                    presentation.UpdateExecutionFocusPoint(player.position, bossRoot.position);
                    return presentation.ExecutionFocusPoint;
                }
            }
        }

        private IEnumerator TrackFinalShot(float seconds, int revision)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (revision != cameraRevision || profile == null || !hasFinalShotPath)
                {
                    finalShotTrackRoutine = null;
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / seconds);
                presentation.SetExecutionFocusPoint(
                    Vector3.Lerp(finalShotStart, finalShotEnd, progress));
                yield return null;
            }

            presentation.SetExecutionFocusPoint(finalShotEnd);
            finalShotTrackRoutine = null;
        }

        private void StopFinalShotTracking()
        {
            if (finalShotTrackRoutine == null)
            {
                return;
            }

            context.CoroutineHost.StopCoroutine(finalShotTrackRoutine);
            finalShotTrackRoutine = null;
        }

        private IEnumerator RestoreDefaultCameraAfter(
            float seconds,
            int revision,
            float blendSmoothTime)
        {
            if (seconds > 0f)
            {
                yield return new WaitForSecondsRealtime(seconds);
            }

            cameraHoldRoutine = null;
            if (revision != cameraRevision || profile == null || boss == null)
            {
                yield break;
            }

            Transform player = context.PlayerTransform;
            Transform bossRoot = boss.BodyRoot != null ? boss.BodyRoot : boss.transform;
            presentation.UpdateExecutionFocusPoint(player.position, bossRoot.position);
            PlayerCombatConfig config = context.Config;
            context.CameraFollow?.BeginCinematicFocus(
                presentation.ExecutionFocusPoint,
                config != null ? config.ExecutionCameraFocusWeight : 1f,
                config != null ? config.ExecutionCameraZoomMultiplier : 0.75f,
                blendSmoothTime);
        }
    }
}
