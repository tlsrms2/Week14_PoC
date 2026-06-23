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
