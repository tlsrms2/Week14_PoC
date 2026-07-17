using System.Collections;
using UnityEngine;
using Week14.Bootstrap;

namespace Week14.Enemy
{
    internal sealed class ConductorPatternCameraFocus
    {
        private readonly BossActionContext context;
        private readonly BossAI coroutineOwner;
        private readonly CameraFollow2D cameraFollow;
        private readonly float delaySeconds;
        private readonly Vector2 worldCenter;

        private Coroutine routine;
        private GameObject focusObject;
        private bool isDisposed;

        private ConductorPatternCameraFocus(
            BossActionContext context,
            CameraFollow2D cameraFollow,
            float delaySeconds,
            Vector2 worldCenter)
        {
            this.context = context;
            coroutineOwner = context.Boss;
            this.cameraFollow = cameraFollow;
            this.delaySeconds = Mathf.Max(0f, delaySeconds);
            this.worldCenter = worldCenter;
        }

        public static ConductorPatternCameraFocus Start(
            BossActionContext context,
            float delaySeconds,
            Vector2 worldCenter)
        {
            if (context?.Boss == null)
            {
                return null;
            }

            Camera mainCamera = Camera.main;
            CameraFollow2D cameraFollow = mainCamera != null
                ? mainCamera.GetComponent<CameraFollow2D>()
                : null;
            if (cameraFollow == null)
            {
                return null;
            }

            ConductorPatternCameraFocus focus = new(
                context,
                cameraFollow,
                delaySeconds,
                worldCenter);
            focus.routine = context.Boss.StartCoroutine(focus.WaitAndFocus());
            return focus;
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            if (routine != null && coroutineOwner != null)
            {
                coroutineOwner.StopCoroutine(routine);
                routine = null;
            }

            if (focusObject == null)
            {
                return;
            }

            cameraFollow?.EndCinematicFocusIfTarget(focusObject.transform);
            Object.Destroy(focusObject);
            focusObject = null;
        }

        private IEnumerator WaitAndFocus()
        {
            float remainingSeconds = delaySeconds;
            while (!isDisposed && remainingSeconds > 0f)
            {
                if (context == null || !context.IsExecutionPaused)
                {
                    remainingSeconds -= EnemyTimeScale.DeltaTime;
                }

                yield return null;
            }

            while (!isDisposed && context != null && context.IsExecutionPaused)
            {
                yield return null;
            }

            if (isDisposed)
            {
                yield break;
            }

            focusObject = new GameObject("ConductorPatternCameraFocus");
            focusObject.transform.position = new Vector3(worldCenter.x, worldCenter.y, 0f);
            cameraFollow.BeginCinematicFocus(focusObject.transform, 1f, 1f);
            routine = null;
        }
    }
}
