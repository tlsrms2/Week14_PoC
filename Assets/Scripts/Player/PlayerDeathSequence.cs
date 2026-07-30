using System.Collections;
using UnityEngine;
using Week14.Audio;
using Week14.Bootstrap;
using Week14.Skills;

namespace Week14.Combat
{
    public static class PlayerDeathSequence
    {
        public static IEnumerator Play(PlayerCombatController player = null)
        {
            TimeSlowScreenFx.CancelImmediate();
            TimeSlowSkillSO.CancelActiveAfterimages();
            SoundManager.StopBgm();

            player ??= PlayerCombatController.Active;
            CameraFollow2D playerCamera = player?.CameraFollow;
            PlayerCombatConfig config = player?.Config;
            SoundManager.PlaySfx(SoundEvent.Player_DeathSequence);

            Transform deathFocusTarget = player != null
                ? (player.BodyRoot != null ? player.BodyRoot : player.transform)
                : null;

            player?.PlayerHpView?.SetExecutionVisible(false);

            if (playerCamera != null && deathFocusTarget != null)
            {
                playerCamera.BeginCinematicFocus(
                    deathFocusTarget,
                    config != null ? config.DeathCameraFocusWeight : 1f,
                    config != null ? config.DeathCameraZoomMultiplier : 0.7f);
            }

            float maxWorldFreezeDelaySeconds = config != null ? config.DeathWorldFreezeDelaySeconds : 0.3f;
            float zoomWaitElapsed = 0f;
            while (playerCamera != null
                && !playerCamera.IsCinematicZoomSettled()
                && zoomWaitElapsed < maxWorldFreezeDelaySeconds)
            {
                zoomWaitElapsed += Time.deltaTime;
                yield return null;
            }

            float previousTimeScaleBeforeDeath = Time.timeScale;
            Time.timeScale = 0f;

            PlayerVisualRig visual = player?.Visual;
            if (visual != null)
            {
                visual.PlayDeath();
                float deathAnimationSeconds = config != null ? config.DeathAnimationSeconds : 1.5333333f;
                if (deathAnimationSeconds > 0f)
                {
                    yield return new WaitForSecondsRealtime(deathAnimationSeconds);
                }
            }

            Time.timeScale = previousTimeScaleBeforeDeath;
            playerCamera?.EndCinematicFocus();
        }
    }
}
