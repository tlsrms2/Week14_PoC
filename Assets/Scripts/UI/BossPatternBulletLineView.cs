using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Week14.UI
{
    public sealed class BossPatternBulletLineView : MonoBehaviour
    {
        private const string BulletRootName = "PatternBulletLines";

        [System.Serializable]
        public sealed class Settings
        {
            [SerializeField, Min(0.1f), Tooltip("대상 스프라이트를 찾지 못했을 때 보스 중심에서 오른쪽 기준점까지 사용할 기본 거리입니다.")]
            private float fallbackRadius = 0.85f;

            [SerializeField, Min(0f), Tooltip("대상 스프라이트 오른쪽 경계에서 총알 줄을 얼마나 더 띄울지 정합니다.")]
            private float radiusPadding = 0.06f;

            [SerializeField, Tooltip("자동으로 잡은 오른쪽 기준점에서 추가로 밀 위치입니다. X는 오른쪽, Y는 위쪽입니다.")]
            private Vector2 anchorOffset = new(0.14f, 0f);

            [SerializeField, FormerlySerializedAs("maxBulletsPerRow"), Min(1), Tooltip("한 세로줄에 위에서 아래로 쌓을 총알 줄 개수입니다. 넘치면 오른쪽에 다음 열이 생깁니다.")]
            private int maxBulletsPerColumn = 10;

            [SerializeField, Min(0.001f), Tooltip("각 총알 줄의 두께입니다.")]
            private float bulletWidth = 0.045f;

            [SerializeField, Min(0.01f), Tooltip("각 총알 줄의 가로 길이입니다.")]
            private float bulletLength = 0.15f;

            [SerializeField, Min(0.01f), Tooltip("여러 열이 생길 때 열 사이의 가로 간격입니다.")]
            private float bulletGap = 0.095f;

            [SerializeField, Min(0.01f), Tooltip("같은 열 안에서 총알 줄끼리 떨어지는 세로 간격입니다.")]
            private float bulletRowGap = 0.13f;

            [SerializeField, Tooltip("총알 줄 묶음 전체의 회전 각도입니다. 오른쪽에 가로로 눕혀 세우려면 0을 사용합니다.")]
            private float bulletRotationDegrees;

            [SerializeField, Tooltip("대기 중 이미 차오른 총알 줄 색입니다.")]
            private Color loadedColor = new(1f, 0.76f, 0.12f, 0.95f);

            [SerializeField, Tooltip("패턴 진행 중 다음에 발사될 총알 묶음 색입니다.")]
            private Color nextColor = new(1f, 0.39f, 0.06f, 1f);

            [SerializeField, Tooltip("아직 차오르지 않았거나 이미 발사된 총알 줄 색입니다.")]
            private Color spentColor = new(1f, 1f, 1f, 0.24f);

            [SerializeField, Min(0.1f), Tooltip("다음 발사 묶음을 표시할 때 적용할 두께 배율입니다.")]
            private float nextWidthScale = 1.05f;

            [SerializeField, Tooltip("LineRenderer 정렬 순서입니다. 다른 스프라이트에 가려지면 값을 올립니다.")]
            private int sortingOrder = 9;

            public float FallbackRadius => Mathf.Max(0.1f, fallbackRadius);
            public float RadiusPadding => Mathf.Max(0f, radiusPadding);
            public Vector2 AnchorOffset => anchorOffset;
            public int MaxBulletsPerColumn => Mathf.Max(1, maxBulletsPerColumn);
            public float BulletWidth => Mathf.Max(0.001f, bulletWidth);
            public float BulletLength => Mathf.Max(0.01f, bulletLength);
            public float BulletGap => Mathf.Max(0.01f, bulletGap);
            public float BulletRowGap => Mathf.Max(0.01f, bulletRowGap);
            public float BulletRotationDegrees => bulletRotationDegrees;
            public Color LoadedColor => loadedColor;
            public Color NextColor => nextColor;
            public Color SpentColor => spentColor;
            public float NextWidthScale => Mathf.Max(0.1f, nextWidthScale);
            public int SortingOrder => sortingOrder;
        }

        [SerializeField, Tooltip("이 컴포넌트에서 직접 조절하는 패턴 총알 줄 표시 설정입니다.")]
        private Settings settings = new();

        [SerializeField, Tooltip("총알 줄이 따라붙을 대상입니다. HogBossAI가 있으면 보스 BodyRoot로 자동 지정됩니다.")]
        private Transform targetRoot;

        private Transform bulletRoot;
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
            if (bulletRoot != null)
            {
                bulletRoot.SetParent(targetRoot, false);
            }
        }

        public void ShowLoading(IReadOnlyList<int> groups, int loadedGroupCount)
        {
            int totalBulletCount = GetTotalBulletCount(groups);
            if (totalBulletCount <= 0)
            {
                Hide();
                return;
            }

            int loadedBulletCount = GetLoadedBulletCount(groups, loadedGroupCount);
            EnsureBulletRoot();
            UpdateRootTransform();

            for (int i = 0; i < totalBulletCount; i++)
            {
                LineRenderer bullet = PrepareBulletLine(i, totalBulletCount);
                ApplyBulletStyle(bullet, i < loadedBulletCount ? settings.LoadedColor : settings.SpentColor, 1f);
            }

            HideUnusedBullets(totalBulletCount);
        }

        public void ShowNextGroup(IReadOnlyList<int> groups, int activeGroupIndex)
        {
            int totalBulletCount = GetTotalBulletCount(groups);
            if (totalBulletCount <= 0)
            {
                Hide();
                return;
            }

            EnsureBulletRoot();
            UpdateRootTransform();

            int groupCount = groups != null ? groups.Count : 0;
            activeGroupIndex = Mathf.Clamp(activeGroupIndex, 0, groupCount);
            if (activeGroupIndex >= groupCount)
            {
                Hide();
                return;
            }

            int bulletIndex = 0;
            for (int groupIndex = 0; groups != null && groupIndex < groupCount; groupIndex++)
            {
                int groupSize = Mathf.Max(0, groups[groupIndex]);
                Color color = groupIndex < activeGroupIndex
                    ? settings.SpentColor
                    : groupIndex == activeGroupIndex ? settings.NextColor : settings.LoadedColor;
                float widthScale = groupIndex == activeGroupIndex ? settings.NextWidthScale : 1f;

                for (int i = 0; i < groupSize && bulletIndex < totalBulletCount; i++)
                {
                    LineRenderer bullet = PrepareBulletLine(bulletIndex, totalBulletCount);
                    ApplyBulletStyle(bullet, color, widthScale);
                    bulletIndex++;
                }
            }

            HideUnusedBullets(totalBulletCount);
        }

        public void Hide()
        {
            for (int i = 0; i < bulletLines.Count; i++)
            {
                if (bulletLines[i] != null)
                {
                    bulletLines[i].enabled = false;
                }
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
                GameObject bulletRootObject = new(BulletRootName);
                bulletRootObject.transform.SetParent(targetRoot, false);
                bulletRoot = bulletRootObject.transform;
            }
        }

        private void UpdateRootTransform()
        {
            if (bulletRoot == null)
            {
                return;
            }

            bulletRoot.localPosition = GetRightAnchorPosition();
            bulletRoot.localRotation = Quaternion.Euler(0f, 0f, settings.BulletRotationDegrees);
            bulletRoot.localScale = Vector3.one;
        }

        private LineRenderer PrepareBulletLine(int index, int totalBulletCount)
        {
            int maxBulletsPerColumn = settings.MaxBulletsPerColumn;
            int column = index / maxBulletsPerColumn;
            int row = index % maxBulletsPerColumn;
            int columnBulletCount = Mathf.Min(maxBulletsPerColumn, totalBulletCount - column * maxBulletsPerColumn);
            float rowSpacing = Mathf.Max(settings.BulletRowGap, settings.BulletWidth);
            float x = column * settings.BulletGap;
            float y = ((columnBulletCount - 1) * 0.5f - row) * rowSpacing;

            LineRenderer bullet = GetBulletLine(index);
            bullet.enabled = true;
            bullet.positionCount = 2;
            bullet.loop = false;
            bullet.useWorldSpace = false;
            bullet.numCapVertices = 4;
            bullet.numCornerVertices = 2;
            bullet.material = GetMaterial();
            bullet.sortingOrder = settings.SortingOrder;
            bullet.SetPosition(0, new Vector3(x - settings.BulletLength * 0.5f, y, 0f));
            bullet.SetPosition(1, new Vector3(x + settings.BulletLength * 0.5f, y, 0f));
            return bullet;
        }

        private void ApplyBulletStyle(LineRenderer bullet, Color color, float widthScale)
        {
            float nextWidth = settings.BulletWidth * Mathf.Max(0.01f, widthScale);
            bullet.startColor = color;
            bullet.endColor = color;
            bullet.startWidth = nextWidth;
            bullet.endWidth = nextWidth;
        }

        private LineRenderer GetBulletLine(int index)
        {
            EnsureBulletRoot();

            while (bulletLines.Count <= index)
            {
                GameObject bulletObject = new($"PatternBullet_{bulletLines.Count}");
                bulletObject.transform.SetParent(bulletRoot, false);
                LineRenderer bullet = bulletObject.AddComponent<LineRenderer>();
                bulletLines.Add(bullet);
            }

            return bulletLines[index];
        }

        private void HideUnusedBullets(int totalBulletCount)
        {
            for (int i = totalBulletCount; i < bulletLines.Count; i++)
            {
                if (bulletLines[i] != null)
                {
                    bulletLines[i].enabled = false;
                }
            }
        }

        private static int GetTotalBulletCount(IReadOnlyList<int> groups)
        {
            int total = 0;
            if (groups == null)
            {
                return total;
            }

            for (int i = 0; i < groups.Count; i++)
            {
                total += Mathf.Max(0, groups[i]);
            }

            return total;
        }

        private Vector3 GetRightAnchorPosition()
        {
            Vector2 offset = settings.AnchorOffset;
            SpriteRenderer renderer = targetRoot != null ? targetRoot.GetComponent<SpriteRenderer>() : null;
            if (renderer == null || renderer.sprite == null)
            {
                return new Vector3(settings.FallbackRadius + settings.RadiusPadding + offset.x, offset.y, 0f);
            }

            Bounds bounds = renderer.sprite.bounds;
            return new Vector3(bounds.max.x + settings.RadiusPadding + offset.x, bounds.center.y + offset.y, 0f);
        }

        private static int GetLoadedBulletCount(IReadOnlyList<int> groups, int loadedGroupCount)
        {
            int total = 0;
            int count = Mathf.Clamp(loadedGroupCount, 0, groups != null ? groups.Count : 0);
            for (int i = 0; groups != null && i < count; i++)
            {
                total += Mathf.Max(0, groups[i]);
            }

            return total;
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
