using System.Collections.Generic;
using UnityEngine;

namespace Week14.UI
{
    public sealed class AttackTimingOutline : MonoBehaviour
    {
        private const string RootName = "AttackTimingOutline";
        private const string BulletRootName = "AttackBullets";
        private const int MaxPointCount = 73;

        [SerializeField, Min(0.1f)] private float fallbackRadius = 0.85f;
        [SerializeField, Min(0f)] private float radiusPadding = 0.02f;
        [SerializeField, Min(0.001f)] private float width = 0.045f;
        [SerializeField, Min(0.001f)] private float bulletWidth = 0.06f;
        [SerializeField, Min(0.01f)] private float bulletLength = 0.18f;
        [SerializeField, Min(0.01f)] private float bulletGap = 0.12f;
        [SerializeField, Min(0f)] private float bulletYOffset = 0.24f;
        [SerializeField] private Color color = new(1f, 0.82f, 0.18f, 0.95f);
        [SerializeField] private Color spentBulletColor = new(1f, 1f, 1f, 0.22f);
        [SerializeField] private int sortingOrder = 8;
        [SerializeField] private Transform targetRoot;

        private Transform root;
        private Transform bulletRoot;
        private LineRenderer line;
        private readonly List<LineRenderer> bulletLines = new();
        private static Material material;

        public void SetTarget(Transform target)
        {
            Transform nextTarget = target != null ? target : transform;
            if (targetRoot == nextTarget)
            {
                return;
            }

            targetRoot = nextTarget;
            if (root != null)
            {
                root.SetParent(targetRoot, false);
            }

            if (bulletRoot != null)
            {
                bulletRoot.SetParent(targetRoot, false);
            }
        }

        public void Show(float remainingSeconds, float durationSeconds)
        {
            Show(remainingSeconds, durationSeconds, 0, 0);
        }

        public void Show(float remainingSeconds, float durationSeconds, int loadedBulletCount, int totalBulletCount)
        {
            if (durationSeconds <= 0f || remainingSeconds <= 0f)
            {
                if (totalBulletCount > 0)
                {
                    ShowBullets(loadedBulletCount, totalBulletCount);
                }
                else
                {
                    Hide();
                }

                return;
            }

            EnsureLine();
            UpdateRootTransform();

            float ratio = Mathf.Clamp01(remainingSeconds / durationSeconds);
            DrawArc(line, ratio, 90f, 360f, color, MaxPointCount, ratio >= 0.995f);
            DrawBullets(loadedBulletCount, totalBulletCount);
        }

        public void ShowBullets(int loadedBulletCount, int totalBulletCount)
        {
            if (totalBulletCount <= 0)
            {
                Hide();
                return;
            }

            EnsureLine();
            UpdateRootTransform();
            SetLineVisible(line, false);
            DrawBullets(loadedBulletCount, totalBulletCount);
        }

        public void Hide()
        {
            SetLineVisible(line, false);
            SetBulletsVisible(false);
        }

        private void EnsureLine()
        {
            if (targetRoot == null)
            {
                targetRoot = transform;
            }

            if (line != null)
            {
                if (root != null && root.parent != targetRoot)
                {
                    root.SetParent(targetRoot, false);
                }

                if (bulletRoot != null && bulletRoot.parent != targetRoot)
                {
                    bulletRoot.SetParent(targetRoot, false);
                }

                return;
            }

            root = targetRoot.Find(RootName);
            if (root == null)
            {
                GameObject rootObject = new GameObject(RootName);
                rootObject.transform.SetParent(targetRoot, false);
                root = rootObject.transform;
            }

            line = root.GetComponent<LineRenderer>();
            if (line == null)
            {
                line = root.gameObject.AddComponent<LineRenderer>();
            }

            line.useWorldSpace = false;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.material = GetMaterial();
            EnsureBulletRoot();
        }

        private void DrawArc(
            LineRenderer renderer,
            float ratio,
            float startDegrees,
            float sweepDegrees,
            Color lineColor,
            int maxPointCount,
            bool loop)
        {
            if (renderer == null || ratio <= 0f)
            {
                SetLineVisible(renderer, false);
                return;
            }

            float outlineRadius = GetOutlineRadius();
            float arcDegrees = sweepDegrees * Mathf.Clamp01(ratio);
            int pointCount = Mathf.Max(2, Mathf.CeilToInt(maxPointCount * ratio));

            renderer.enabled = true;
            renderer.loop = loop;
            renderer.positionCount = loop ? maxPointCount : pointCount;
            renderer.startWidth = width;
            renderer.endWidth = width;
            renderer.startColor = lineColor;
            renderer.endColor = lineColor;
            renderer.sortingOrder = sortingOrder;

            int count = renderer.positionCount;
            for (int i = 0; i < count; i++)
            {
                float t = count <= 1 ? 0f : (float)i / (count - 1);
                float angle = (startDegrees - arcDegrees * t) * Mathf.Deg2Rad;
                renderer.SetPosition(i, new Vector3(Mathf.Cos(angle) * outlineRadius, Mathf.Sin(angle) * outlineRadius, 0f));
            }
        }

        private static void SetLineVisible(LineRenderer renderer, bool visible)
        {
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }

        private void DrawBullets(int loadedBulletCount, int totalBulletCount)
        {
            if (totalBulletCount <= 0)
            {
                SetBulletsVisible(false);
                return;
            }

            EnsureBulletRoot();

            int loadedCount = Mathf.Clamp(loadedBulletCount, 0, totalBulletCount);
            float radius = GetOutlineRadius();
            float y = radius + bulletYOffset;
            float startX = -((totalBulletCount - 1) * bulletGap) * 0.5f;

            for (int i = 0; i < totalBulletCount; i++)
            {
                LineRenderer bullet = GetBulletLine(i);
                bullet.enabled = true;
                bullet.positionCount = 2;
                bullet.loop = false;
                bullet.useWorldSpace = false;
                bullet.numCapVertices = 4;
                bullet.numCornerVertices = 2;
                bullet.material = GetMaterial();
                bullet.startWidth = bulletWidth;
                bullet.endWidth = bulletWidth;
                bullet.startColor = i < loadedCount ? color : spentBulletColor;
                bullet.endColor = bullet.startColor;
                bullet.sortingOrder = sortingOrder + 1;

                float x = startX + i * bulletGap;
                bullet.SetPosition(0, new Vector3(x, y - bulletLength * 0.5f, 0f));
                bullet.SetPosition(1, new Vector3(x, y + bulletLength * 0.5f, 0f));
            }

            for (int i = totalBulletCount; i < bulletLines.Count; i++)
            {
                SetLineVisible(bulletLines[i], false);
            }
        }

        private void SetBulletsVisible(bool visible)
        {
            for (int i = 0; i < bulletLines.Count; i++)
            {
                SetLineVisible(bulletLines[i], visible);
            }
        }

        private void UpdateRootTransform()
        {
            if (root == null)
            {
                return;
            }

            root.localPosition = GetSpriteCenter();
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;

            if (bulletRoot != null)
            {
                bulletRoot.localPosition = root.localPosition;
                bulletRoot.localRotation = Quaternion.identity;
                bulletRoot.localScale = Vector3.one;
            }
        }

        private void EnsureBulletRoot()
        {
            if (targetRoot == null)
            {
                targetRoot = transform;
            }

            if (bulletRoot != null)
            {
                return;
            }

            bulletRoot = targetRoot.Find(BulletRootName);
            if (bulletRoot == null)
            {
                GameObject bulletRootObject = new GameObject(BulletRootName);
                bulletRootObject.transform.SetParent(targetRoot, false);
                bulletRoot = bulletRootObject.transform;
            }
        }

        private LineRenderer GetBulletLine(int index)
        {
            EnsureBulletRoot();

            while (bulletLines.Count <= index)
            {
                GameObject bulletObject = new GameObject($"Bullet_{bulletLines.Count}");
                bulletObject.transform.SetParent(bulletRoot, false);
                LineRenderer bullet = bulletObject.AddComponent<LineRenderer>();
                bulletLines.Add(bullet);
            }

            return bulletLines[index];
        }

        private float GetOutlineRadius()
        {
            SpriteRenderer renderer = targetRoot != null ? targetRoot.GetComponent<SpriteRenderer>() : null;
            if (renderer == null || renderer.sprite == null)
            {
                return fallbackRadius;
            }

            Bounds bounds = renderer.sprite.bounds;
            return Mathf.Max(bounds.extents.x, bounds.extents.y) + radiusPadding;
        }

        private Vector3 GetSpriteCenter()
        {
            SpriteRenderer renderer = targetRoot != null ? targetRoot.GetComponent<SpriteRenderer>() : null;
            if (renderer == null || renderer.sprite == null)
            {
                return Vector3.zero;
            }

            return renderer.sprite.bounds.center;
        }

        private static Material GetMaterial()
        {
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Sprites/Default");
            material = shader != null ? new Material(shader) : null;
            return material;
        }
    }
}
