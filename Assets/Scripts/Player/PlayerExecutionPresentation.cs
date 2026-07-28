using System.Collections;
using UnityEngine;
using Week14.Bootstrap;
using Week14.Enemy;
using Week14.UI;

namespace Week14.Combat
{
    internal sealed class PlayerExecutionPresentation
    {
        private const int ExecutionDimSortingOrder = 65;

        private readonly PlayerCombatController.PlayerCombatContext context;
        private Transform executionFocusPoint;
        private SpriteRenderer executionDimRenderer;
        private Coroutine executionDimRoutine;
        private bool playerHpHiddenForExecution;
        private BossEncounterIntroController executionLetterboxController;
        private bool finalExecutionLetterboxShown;
        private ExecutionFinalPhaseVfx finalPhaseVfx;
        private bool finalExecutionSlowMotionActive;
        private float finalExecutionPreviousTimeScale = 1f;
        private float finalExecutionAppliedTimeScale = 1f;
        private Coroutine finalExecutionSlowMotionRoutine;
        private static Sprite executionDimSprite;

        internal PlayerExecutionPresentation(PlayerCombatController.PlayerCombatContext context)
        {
            this.context = context;
        }

        internal Transform ExecutionFocusPoint => executionFocusPoint;

        internal void HidePlayerHpForExecution()
        {
            PlayerHP hpView = GetPlayerHpView();
            playerHpHiddenForExecution = hpView != null && hpView.gameObject.activeSelf;
            if (playerHpHiddenForExecution)
            {
                hpView.SetExecutionVisible(false);
            }
        }

        internal float ShowPlayerHpForExecutionRecovery()
        {
            PlayerHP hpView = GetPlayerHpView();
            if (hpView == null)
            {
                playerHpHiddenForExecution = false;
                return 0f;
            }

            hpView.SetExecutionVisible(true);
            playerHpHiddenForExecution = false;
            return hpView.ExecutionRecoveryEffectSeconds;
        }

        internal void RestorePlayerHpAfterExecution()
        {
            if (!playerHpHiddenForExecution)
            {
                return;
            }

            PlayerHP hpView = GetPlayerHpView();
            if (hpView != null)
            {
                hpView.SetExecutionVisible(true);
            }

            playerHpHiddenForExecution = false;
        }

        internal void KeepPlayerHpHiddenAfterFinalExecution()
        {
            playerHpHiddenForExecution = false;
        }

        internal void StartPendingExecutionBulletTimers()
        {
            GetPlayerHpView()?.StartPendingExecutionBulletTimers();
        }

        internal IEnumerator ShowFinalExecutionLetterbox()
        {
            BossEncounterIntroController letterbox = ResolveExecutionLetterboxController();
            if (letterbox == null)
            {
                yield break;
            }

            float duration = context.Config != null
                ? context.Config.FinalExecutionLetterboxEnterSeconds
                : 0f;
            finalExecutionLetterboxShown = true;
            yield return letterbox.ShowExecutionLetterbox(duration);
        }

        internal IEnumerator HideFinalExecutionLetterbox()
        {
            if (!finalExecutionLetterboxShown)
            {
                yield break;
            }

            BossEncounterIntroController letterbox = ResolveExecutionLetterboxController();
            if (letterbox == null)
            {
                finalExecutionLetterboxShown = false;
                yield break;
            }

            float duration = context.Config != null
                ? context.Config.FinalExecutionLetterboxExitSeconds
                : 0f;
            yield return letterbox.HideExecutionLetterbox(duration);
            finalExecutionLetterboxShown = false;
        }

        internal void HideFinalExecutionLetterboxImmediate()
        {
            if (!finalExecutionLetterboxShown)
            {
                return;
            }

            ResolveExecutionLetterboxController()?.HideExecutionLetterboxImmediate();
            finalExecutionLetterboxShown = false;
        }

        internal int FinalExecutionSortingLayerId => executionDimRenderer != null
            ? executionDimRenderer.sortingLayerID
            : 0;
        internal int FinalExecutionBossBackSortingOrder => finalPhaseVfx?.BossBackSortingOrder ?? 66;
        internal int FinalExecutionBossFrontSortingOrder => finalPhaseVfx?.BossFrontSortingOrder ?? 68;

        internal void BeginFinalExecutionShotSlowMotion()
        {
            PlayerCombatConfig config = context.Config;
            if (config == null)
            {
                return;
            }

            StopFinalExecutionSlowMotion();
            BeginFinalExecutionSlowMotion(config.FinalExecutionShotTimeScale);
            if (config.FinalExecutionShotSlowSeconds <= 0f)
            {
                RestoreFinalExecutionSlowMotion();
                return;
            }

            finalExecutionSlowMotionRoutine = context.CoroutineHost.StartCoroutine(
                RestoreFinalExecutionSlowMotionAfter(config.FinalExecutionShotSlowSeconds));
        }

        internal IEnumerator BeginFinalExecutionBlackout(BossAI boss)
        {
            Camera targetCamera = Camera.main;
            if (targetCamera == null || boss == null)
            {
                yield break;
            }

            StopExecutionShotDim();
            SpriteRenderer dimRenderer = EnsureExecutionDimRenderer(targetCamera);
            if (dimRenderer == null)
            {
                yield break;
            }

            finalPhaseVfx ??= new ExecutionFinalPhaseVfx();
            finalPhaseVfx.Begin(
                context.BodyRenderers,
                boss.BodyRenderers,
                dimRenderer.sortingLayerID,
                ExecutionDimSortingOrder + 2);

            dimRenderer.enabled = true;
            float duration = context.Config != null
                ? context.Config.FinalExecutionBlackoutFadeInSeconds
                : 0f;
            yield return FadeExecutionDim(targetCamera, dimRenderer, 0f, 1f, duration);
        }

        internal IEnumerator PlayFinalExecutionImpactAndRelease(GameObject[] shotLineObjects)
        {
            PlayerCombatConfig config = context.Config;
            if (executionDimRenderer == null || finalPhaseVfx == null || config == null)
            {
                DestroyShotLineObjects(shotLineObjects);
                yield break;
            }

            SetExecutionDimAlpha(executionDimRenderer, 1f);
            try
            {
                finalPhaseVfx.ShowWhiteOutline(config.FinalExecutionOutlineWidthPixels);
                yield return FadeShotLinesAndOutline(
                    shotLineObjects,
                    config.FinalExecutionShotLineSeconds,
                    config.FinalExecutionOutlineFlashSeconds);
                SetExecutionDimAlpha(executionDimRenderer, 1f);
                if (config.FinalExecutionBlackoutHoldSeconds > 0f)
                {
                    yield return new WaitForSecondsRealtime(config.FinalExecutionBlackoutHoldSeconds);
                }
            }
            finally
            {
                DestroyShotLineObjects(shotLineObjects);
                finalPhaseVfx.HideWhiteOutline();
            }

            Camera targetCamera = Camera.main;
            yield return FadeExecutionDim(
                targetCamera,
                executionDimRenderer,
                1f,
                0f,
                config.FinalExecutionBlackoutFadeOutSeconds);

            executionDimRenderer.enabled = false;
            finalPhaseVfx.Restore();
        }

        private IEnumerator FadeShotLinesAndOutline(
            GameObject[] shotLineObjects,
            float lineSeconds,
            float outlineSeconds)
        {
            float lineDuration = Mathf.Max(0.01f, lineSeconds);
            float outlineDuration = Mathf.Max(0f, outlineSeconds);
            float duration = Mathf.Max(lineDuration, outlineDuration);
            bool outlineHidden = false;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float lineAlpha = 1f - Mathf.Clamp01(elapsed / lineDuration);
                SetShotLineAlpha(shotLineObjects, lineAlpha);
                if (!outlineHidden && elapsed >= outlineDuration)
                {
                    finalPhaseVfx.HideWhiteOutline();
                    outlineHidden = true;
                }

                yield return null;
            }

            SetShotLineAlpha(shotLineObjects, 0f);
            DestroyShotLineObjects(shotLineObjects);
            finalPhaseVfx.HideWhiteOutline();
        }

        private static void SetShotLineAlpha(GameObject[] shotLineObjects, float alpha)
        {
            if (shotLineObjects == null)
            {
                return;
            }

            for (int i = 0; i < shotLineObjects.Length; i++)
            {
                LineRenderer line = shotLineObjects[i] != null
                    ? shotLineObjects[i].GetComponent<LineRenderer>()
                    : null;
                if (line == null)
                {
                    continue;
                }

                Color startColor = line.startColor;
                Color endColor = line.endColor;
                startColor.a = Mathf.Clamp01(alpha);
                endColor.a = Mathf.Clamp01(alpha);
                line.startColor = startColor;
                line.endColor = endColor;
            }
        }

        private static void DestroyShotLineObjects(GameObject[] shotLineObjects)
        {
            if (shotLineObjects == null)
            {
                return;
            }

            for (int i = 0; i < shotLineObjects.Length; i++)
            {
                if (shotLineObjects[i] == null)
                {
                    continue;
                }

                UnityEngine.Object.Destroy(shotLineObjects[i]);
                shotLineObjects[i] = null;
            }
        }

        internal void RestoreFinalExecutionPresentation(bool hideLetterbox)
        {
            if (executionDimRoutine != null)
            {
                context.CoroutineHost.StopCoroutine(executionDimRoutine);
                executionDimRoutine = null;
            }

            if (executionDimRenderer != null)
            {
                executionDimRenderer.enabled = false;
            }

            finalPhaseVfx?.Restore();
            StopFinalExecutionSlowMotion();
            if (hideLetterbox && finalExecutionLetterboxShown)
            {
                ResolveExecutionLetterboxController()?.HideExecutionLetterboxImmediate();
                finalExecutionLetterboxShown = false;
            }
        }

        private void BeginFinalExecutionSlowMotion(float timeScale)
        {
            if (!finalExecutionSlowMotionActive)
            {
                finalExecutionPreviousTimeScale = Time.timeScale;
            }

            finalExecutionAppliedTimeScale = Mathf.Clamp(timeScale, 0.01f, 1f);
            Time.timeScale = finalExecutionAppliedTimeScale;
            finalExecutionSlowMotionActive = true;
        }

        private void RestoreFinalExecutionSlowMotion()
        {
            if (!finalExecutionSlowMotionActive)
            {
                return;
            }

            if (Mathf.Approximately(Time.timeScale, finalExecutionAppliedTimeScale))
            {
                Time.timeScale = finalExecutionPreviousTimeScale <= 0f
                    ? 1f
                    : finalExecutionPreviousTimeScale;
            }

            finalExecutionSlowMotionActive = false;
        }

        private IEnumerator RestoreFinalExecutionSlowMotionAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            finalExecutionSlowMotionRoutine = null;
            RestoreFinalExecutionSlowMotion();
        }

        private void StopFinalExecutionSlowMotion()
        {
            if (finalExecutionSlowMotionRoutine != null)
            {
                context.CoroutineHost.StopCoroutine(finalExecutionSlowMotionRoutine);
                finalExecutionSlowMotionRoutine = null;
            }

            RestoreFinalExecutionSlowMotion();
        }

        internal PlayerHP GetPlayerHpView()
        {
            if (context.PlayerHpView == null)
            {
                context.PlayerHpView = UnityEngine.Object.FindFirstObjectByType<PlayerHP>(FindObjectsInactive.Include);
            }

            return context.PlayerHpView;
        }

        internal void UpdateExecutionFocusPoint(Vector3 playerPosition, Vector3 targetPosition)
        {
            if (executionFocusPoint == null)
            {
                GameObject focusObject = new GameObject("ExecutionCameraFocus")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                executionFocusPoint = focusObject.transform;
            }

            Vector3 focusPosition = (playerPosition + targetPosition) * 0.5f;
            focusPosition.z = playerPosition.z;
            executionFocusPoint.position = focusPosition;
        }

        internal void SetExecutionFocusPoint(Vector3 worldPosition)
        {
            UpdateExecutionFocusPoint(worldPosition, worldPosition);
        }

        internal IEnumerator WaitBeforeFinalDeathFocus()
        {
            if (context.FinalDeathCameraReturnSeconds > 0f)
            {
                yield return new WaitForSeconds(context.FinalDeathCameraReturnSeconds);
            }
            else
            {
                yield return null;
            }
        }

        internal CameraFollow2D BeginFinalDeathCameraFocus(BossAI boss)
        {
            if (boss == null)
            {
                return null;
            }

            PlayerCombatConfig config = context.Config;
            CameraFollow2D activeCamera = context.CameraFollow;
            Transform focusTarget = boss.BodyRoot != null ? boss.BodyRoot : boss.transform;
            activeCamera?.BeginCinematicFocus(
                focusTarget,
                config != null ? config.ExecutionCameraFocusWeight : 1f,
                config != null ? config.ExecutionCameraZoomMultiplier : 0.75f);
            return activeCamera;
        }

        internal void PlayExecutionShotDim()
        {
            PlayerCombatConfig config = context.Config;
            if (config == null || config.ExecutionShotDimSeconds <= 0f || config.ExecutionShotDimAlpha <= 0f)
            {
                return;
            }

            Camera targetCamera = Camera.main;
            if (targetCamera == null)
            {
                return;
            }

            if (executionDimRoutine != null)
            {
                context.CoroutineHost.StopCoroutine(executionDimRoutine);
            }

            executionDimRoutine = context.CoroutineHost.StartCoroutine(PlayExecutionShotDimRoutine(targetCamera));
        }

        internal void StopExecutionShotDim()
        {
            if (executionDimRoutine != null)
            {
                context.CoroutineHost.StopCoroutine(executionDimRoutine);
                executionDimRoutine = null;
            }

            if (executionDimRenderer != null)
            {
                executionDimRenderer.enabled = false;
            }

            finalPhaseVfx?.Restore();
        }

        private IEnumerator PlayExecutionShotDimRoutine(Camera targetCamera)
        {
            SpriteRenderer renderer = EnsureExecutionDimRenderer(targetCamera);
            if (renderer == null)
            {
                yield break;
            }

            PlayerCombatConfig config = context.Config;
            float duration = Mathf.Max(0.01f, config.ExecutionShotDimSeconds);
            float maxAlpha = Mathf.Clamp01(config.ExecutionShotDimAlpha);
            renderer.enabled = true;

            float elapsed = 0f;
            while (elapsed < duration && targetCamera != null)
            {
                elapsed += Time.deltaTime;
                UpdateExecutionDimTransform(targetCamera, renderer.transform);
                float t = Mathf.Clamp01(elapsed / duration);
                renderer.color = new Color(0f, 0f, 0f, maxAlpha * (1f - t));
                yield return null;
            }

            renderer.enabled = false;
            executionDimRoutine = null;
        }

        private static IEnumerator FadeExecutionDim(
            Camera targetCamera,
            SpriteRenderer renderer,
            float fromAlpha,
            float toAlpha,
            float duration)
        {
            float animationDuration = Mathf.Max(0f, duration);
            if (animationDuration <= 0f)
            {
                SetExecutionDimAlpha(renderer, toAlpha);
                yield break;
            }

            for (float elapsed = 0f; elapsed < animationDuration; elapsed += Time.unscaledDeltaTime)
            {
                if (targetCamera == null || renderer == null)
                {
                    yield break;
                }

                UpdateExecutionDimTransform(targetCamera, renderer.transform);
                float progress = Mathf.Clamp01(elapsed / animationDuration);
                SetExecutionDimAlpha(renderer, Mathf.Lerp(fromAlpha, toAlpha, progress));
                yield return null;
            }

            SetExecutionDimAlpha(renderer, toAlpha);
        }

        private static void SetExecutionDimAlpha(SpriteRenderer renderer, float alpha)
        {
            if (renderer != null)
            {
                renderer.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
            }
        }

        private BossEncounterIntroController ResolveExecutionLetterboxController()
        {
            if (executionLetterboxController != null && executionLetterboxController.HasExecutionLetterbox)
            {
                return executionLetterboxController;
            }

            BossEncounterIntroController[] controllers = UnityEngine.Object.FindObjectsByType<BossEncounterIntroController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < controllers.Length; i++)
            {
                if (controllers[i] != null && controllers[i].HasExecutionLetterbox)
                {
                    executionLetterboxController = controllers[i];
                    return executionLetterboxController;
                }
            }

            return null;
        }

        private SpriteRenderer EnsureExecutionDimRenderer(Camera targetCamera)
        {
            if (targetCamera == null)
            {
                return null;
            }

            if (executionDimRenderer == null)
            {
                GameObject dimObject = new GameObject("ExecutionShotDim")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                executionDimRenderer = dimObject.AddComponent<SpriteRenderer>();
                executionDimRenderer.sprite = GetExecutionDimSprite();
                executionDimRenderer.sortingOrder = ExecutionDimSortingOrder;
                executionDimRenderer.enabled = false;
            }

            executionDimRenderer.transform.SetParent(targetCamera.transform, false);
            UpdateExecutionDimTransform(targetCamera, executionDimRenderer.transform);
            return executionDimRenderer;
        }

        private static void UpdateExecutionDimTransform(Camera targetCamera, Transform dimTransform)
        {
            if (targetCamera == null || dimTransform == null)
            {
                return;
            }

            float height = targetCamera.orthographic ? targetCamera.orthographicSize * 2f : 50f;
            float width = height * targetCamera.aspect;
            dimTransform.localPosition = new Vector3(0f, 0f, targetCamera.nearClipPlane + 0.05f);
            dimTransform.localRotation = Quaternion.identity;
            dimTransform.localScale = new Vector3(width, height, 1f);
        }

        private static Sprite GetExecutionDimSprite()
        {
            if (executionDimSprite != null)
            {
                return executionDimSprite;
            }

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            executionDimSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return executionDimSprite;
        }
    }
}
