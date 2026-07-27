using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    internal readonly struct BossDashAttackArea
    {
        internal BossDashAttackArea(float length, float width, float lateralOffset)
        {
            Length = Mathf.Max(0f, length);
            Width = Mathf.Max(0f, width);
            LateralOffset = lateralOffset;
        }

        internal float Length { get; }
        internal float Width { get; }
        internal float LateralOffset { get; }
        internal bool IsValid => Length > 0.0001f && Width > 0.0001f;

        internal bool TryGetSegmentBox(
            Vector2 origin,
            Vector2 direction,
            float fromDistance,
            float toDistance,
            out Vector2 center,
            out Vector2 size,
            out float angle)
        {
            center = default;
            size = default;
            angle = 0f;
            if (!IsValid || direction.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            Vector2 forward = direction.normalized;
            Vector2 lateral = new(-forward.y, forward.x);
            float start = Mathf.Clamp(Mathf.Min(fromDistance, toDistance), 0f, Length);
            float end = Mathf.Clamp(Mathf.Max(fromDistance, toDistance), 0f, Length);
            float segmentLength = end - start;
            if (segmentLength <= 0.0001f)
            {
                return false;
            }

            center = origin
                + forward * ((start + end) * 0.5f)
                + lateral * LateralOffset;
            size = new Vector2(segmentLength, Width);
            angle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
            return true;
        }
    }

    internal static class BossDashMotion
    {
        private const int IntegrationStepCount = 64;

        internal static float GetDistance(float speed, float duration, AnimationCurve curve)
        {
            return GetDistanceAtProgress(speed, duration, curve, 1f);
        }

        internal static float GetDistanceAtProgress(
            float speed,
            float duration,
            AnimationCurve curve,
            float progress)
        {
            float clampedProgress = Mathf.Clamp01(progress);
            if (speed <= 0f || duration <= 0f || clampedProgress <= 0f)
            {
                return 0f;
            }

            float integral = 0f;
            float step = 1f / IntegrationStepCount;
            for (int i = 0; i < IntegrationStepCount; i++)
            {
                float start = i * step;
                if (start >= clampedProgress)
                {
                    break;
                }

                float end = Mathf.Min(start + step, clampedProgress);
                float startValue = EvaluateSpeedCurve(curve, start);
                float endValue = EvaluateSpeedCurve(curve, end);
                integral += (startValue + endValue) * 0.5f * (end - start);
            }

            return Mathf.Max(0f, speed) * Mathf.Max(0f, duration) * integral;
        }

        private static float EvaluateSpeedCurve(AnimationCurve curve, float progress)
        {
            return curve != null && curve.length > 0
                ? Mathf.Max(0f, curve.Evaluate(progress))
                : 1f;
        }
    }

    // 프리팹 타일을 한 프레임에 생성해 각 Animator의 재생 위상을 맞춘다.
    internal sealed class BossDashTrajectoryVfx : MonoBehaviour
    {
        private const float DefaultTileLength = 1f;

        private readonly List<RendererColorBinding> rendererBindings = new();
        private Color readyColor;
        private Color chargedColor;
        private Color currentColor;

        internal BossDashAttackArea AttackArea { get; private set; }

        internal static BossDashTrajectoryVfx Spawn(
            GameObject indicatorPrefab,
            float length,
            float tileSpacing,
            float tileRotationOffset,
            Color readyColor,
            Color chargedColor)
        {
            if (indicatorPrefab == null || length <= 0f)
            {
                return null;
            }

            GameObject root = new("BossDashTrajectoryVfx");
            BossDashTrajectoryVfx vfx = root.AddComponent<BossDashTrajectoryVfx>();
            vfx.Setup(indicatorPrefab, length, tileSpacing, tileRotationOffset, readyColor, chargedColor);
            return vfx;
        }

        private void Setup(
            GameObject indicatorPrefab,
            float length,
            float tileSpacing,
            float tileRotationOffset,
            Color readyColor,
            Color chargedColor)
        {
            this.readyColor = readyColor;
            this.chargedColor = chargedColor;
            currentColor = readyColor;

            GameObject firstTile = CreateTile(indicatorPrefab, tileRotationOffset, out Transform firstSlot);
            MeasureTileGeometry(
                firstTile,
                out float sourceLength,
                out float sourceCenterOffset,
                out float sourceWidth,
                out float sourceLateralOffset);
            AlignTileCenter(firstTile.transform, sourceCenterOffset);
            AttackArea = new BossDashAttackArea(length, sourceWidth, sourceLateralOffset);

            float segmentLength = tileSpacing > 0f
                ? tileSpacing
                : sourceLength;
            int tileCount = Mathf.Max(1, Mathf.CeilToInt(length / segmentLength));

            // 마지막 타일만 남은 경로 길이에 맞춰 줄여 시작점과 종점을 정확히 일치시킨다.
            ConfigureSlot(firstSlot, 0, length, segmentLength, sourceLength);
            RegisterRenderers(firstTile);

            for (int i = 1; i < tileCount; i++)
            {
                GameObject tile = CreateTile(indicatorPrefab, tileRotationOffset, out Transform slot);
                AlignTileCenter(tile.transform, sourceCenterOffset);
                ConfigureSlot(slot, i, length, segmentLength, sourceLength);
                RegisterRenderers(tile);
            }

            ApplyColor();
        }

        internal void UpdateVfx(Vector3 bossPosition, Vector2 direction, float progress)
        {
            currentColor = Color.Lerp(readyColor, chargedColor, Mathf.Clamp01(progress));
            ApplyColor();

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector2 normalizedDirection = direction.normalized;
            transform.position = bossPosition;
            float angle = Mathf.Atan2(normalizedDirection.y, normalizedDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void LateUpdate()
        {
            // Animator가 색을 키프레임으로 다뤄도 최종 경고 색을 우선 적용한다.
            ApplyColor();
        }

        private GameObject CreateTile(GameObject prefab, float rotationOffset, out Transform slot)
        {
            GameObject slotObject = new("IndicatorTile");
            slot = slotObject.transform;
            slot.SetParent(transform, false);

            GameObject tile = Object.Instantiate(prefab, slot, false);
            tile.transform.localRotation = Quaternion.Euler(0f, 0f, rotationOffset) * tile.transform.localRotation;
            return tile;
        }

        private static void AlignTileCenter(Transform tile, float centerOffset)
        {
            tile.localPosition -= Vector3.right * centerOffset;
        }

        private static void ConfigureSlot(
            Transform slot,
            int index,
            float totalLength,
            float segmentLength,
            float sourceLength)
        {
            float segmentStart = index * segmentLength;
            float visibleLength = Mathf.Clamp(totalLength - segmentStart, 0f, segmentLength);
            if (visibleLength <= 0f)
            {
                slot.gameObject.SetActive(false);
                return;
            }

            slot.localPosition = Vector3.right * (segmentStart + visibleLength * 0.5f);
            slot.localScale = new Vector3(visibleLength / sourceLength, 1f, 1f);
        }

        private static void MeasureTileGeometry(
            GameObject tile,
            out float sourceLength,
            out float sourceCenterOffset,
            out float sourceWidth,
            out float sourceLateralOffset)
        {
            SpriteRenderer[] tileRenderers = tile.GetComponentsInChildren<SpriteRenderer>(true);
            bool hasBounds = false;
            Bounds bounds = default;
            for (int i = 0; i < tileRenderers.Length; i++)
            {
                SpriteRenderer spriteRenderer = tileRenderers[i];
                if (spriteRenderer == null || spriteRenderer.sprite == null)
                {
                    continue;
                }

                if (hasBounds)
                {
                    bounds.Encapsulate(spriteRenderer.bounds);
                }
                else
                {
                    bounds = spriteRenderer.bounds;
                    hasBounds = true;
                }
            }

            if (!hasBounds || bounds.size.x <= 0.0001f)
            {
                sourceLength = DefaultTileLength;
                sourceCenterOffset = 0f;
                sourceWidth = 0f;
                sourceLateralOffset = 0f;
                return;
            }

            sourceLength = bounds.size.x;
            sourceCenterOffset = bounds.center.x;
            sourceWidth = Mathf.Max(0.0001f, bounds.size.y);
            sourceLateralOffset = bounds.center.y;
        }

        private void RegisterRenderers(GameObject tile)
        {
            SpriteRenderer[] tileRenderers = tile.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < tileRenderers.Length; i++)
            {
                rendererBindings.Add(new RendererColorBinding(tileRenderers[i]));
            }
        }

        private void ApplyColor()
        {
            for (int i = 0; i < rendererBindings.Count; i++)
            {
                RendererColorBinding binding = rendererBindings[i];
                SpriteRenderer spriteRenderer = binding.Renderer;
                if (spriteRenderer == null)
                {
                    continue;
                }

                Color color = currentColor;
                color.a *= binding.BaseAlpha;
                spriteRenderer.color = color;
            }
        }

        private readonly struct RendererColorBinding
        {
            internal RendererColorBinding(SpriteRenderer renderer)
            {
                Renderer = renderer;
                BaseAlpha = renderer != null ? renderer.color.a : 1f;
            }

            internal SpriteRenderer Renderer { get; }
            internal float BaseAlpha { get; }
        }
    }
}
