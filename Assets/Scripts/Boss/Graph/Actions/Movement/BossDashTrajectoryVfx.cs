using UnityEngine;
using UnityEngine.Rendering;

namespace Week14.Enemy
{
    // Background(전체 사거리)와 Fill(진행률)을 정점 컬러 그라데이션 quad로 그린다.
    // Sprites/Default 셰이더는 Cull Off + (텍스처 * 정점 컬러) 조합이라 커스텀 셰이더 없이도
    // 폭 방향 중심(밝음)-가장자리(어두움) 그라데이션을 낼 수 있다.
    internal sealed class BossDashTrajectoryVfx : MonoBehaviour
    {
        private static Shader gradientShader;

        private Transform fillTransform;
        private float length;
        private float width;

        internal static BossDashTrajectoryVfx Spawn(
            Sprite sprite,
            float length,
            float width,
            Color backgroundInnerColor,
            Color backgroundOuterColor,
            Color fillInnerColor,
            Color fillOuterColor,
            int sortingOrder,
            float gradientFalloffPower)
        {
            GameObject go = new("BossDashTrajectoryVfx");
            BossDashTrajectoryVfx vfx = go.AddComponent<BossDashTrajectoryVfx>();
            vfx.Setup(sprite, length, width, backgroundInnerColor, backgroundOuterColor, fillInnerColor, fillOuterColor, sortingOrder, gradientFalloffPower);
            return vfx;
        }

        private void Setup(
            Sprite sprite,
            float length,
            float width,
            Color backgroundInnerColor,
            Color backgroundOuterColor,
            Color fillInnerColor,
            Color fillOuterColor,
            int sortingOrder,
            float gradientFalloffPower)
        {
            this.length = length;
            this.width = width;

            Material material = CreateGradientMaterial(sprite != null ? sprite.texture : null);

            GameObject bgGo = CreateGradientQuad("Background", material, backgroundInnerColor, backgroundOuterColor, sortingOrder, gradientFalloffPower);
            bgGo.transform.SetParent(transform, false);
            bgGo.transform.localPosition = new Vector3(0f, -length * 0.5f, 0f);
            bgGo.transform.localScale = new Vector3(width, length, 1f);

            GameObject fillGo = CreateGradientQuad("Fill", material, fillInnerColor, fillOuterColor, sortingOrder + 1, gradientFalloffPower);
            fillGo.transform.SetParent(transform, false);
            fillGo.transform.localPosition = new Vector3(0f, -length * 0.5f, 0f);
            fillGo.transform.localScale = new Vector3(width, 0f, 1f);
            fillTransform = fillGo.transform;
        }

        // progress: 0~1, 1이면 fill이 보스 위치부터 Background 끝까지 꽉 채움
        internal void UpdateVfx(Vector3 bossPosition, Vector2 direction, float progress)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector2 dir = direction.normalized;
            Vector2 center = (Vector2)bossPosition + dir * (length * 0.5f);
            transform.position = new Vector3(center.x, center.y, bossPosition.z);

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            if (fillTransform != null)
            {
                fillTransform.localScale = new Vector3(width, length * Mathf.Clamp01(progress), 1f);
            }
        }

        private static GameObject CreateGradientQuad(string name, Material material, Color innerColor, Color outerColor, int sortingOrder, float falloffPower)
        {
            GameObject go = new(name);

            MeshFilter meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = CreateGradientQuadMesh(innerColor, outerColor, falloffPower);

            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            BossSorting.Apply(meshRenderer);
            meshRenderer.sortingOrder = sortingOrder;

            return go;
        }

        // 폭(x) 방향으로 중심부(innerColor, 밝음)는 넓게 유지되다가 좌우 가장자리(outerColor, 어두움)
        // 근처에서만 급격히 진해지는 1x1 quad. y는 0(안쪽/보스 쪽)~1(바깥쪽/사거리 끝)이며 색은 y와 무관하게
        // 전체 길이에 동일 패턴으로 유지된다.
        //
        // 정점 컬러는 정점 사이를 GPU가 항상 선형으로 보간하므로, "중심부는 평평하고 가장자리 근처에서만
        // 급격히 어두워지는" 곡선 형태를 표현하려면 폭을 여러 칸으로 쪼개서 각 분할 지점의 색을
        // 지수 커브(t = (거리/half) ^ falloffPower)로 미리 계산해둬야 한다. falloffPower가 클수록
        // 중심부가 더 넓고 평평하게 밝은 상태로 유지되다가 가장자리 근처에서만 급격히 어두워진다.
        private const int GradientColumnsPerHalf = 8;

        private static Mesh CreateGradientQuadMesh(Color innerColor, Color outerColor, float falloffPower)
        {
            int columnCount = GradientColumnsPerHalf * 2 + 1;
            Vector3[] vertices = new Vector3[columnCount * 2];
            Vector2[] uv = new Vector2[columnCount * 2];
            Color[] colors = new Color[columnCount * 2];
            int[] triangles = new int[(columnCount - 1) * 6];

            for (int i = 0; i < columnCount; i++)
            {
                float uvX = i / (float)(columnCount - 1);
                float x = Mathf.Lerp(-0.5f, 0.5f, uvX);
                float distanceFromCenter = Mathf.Abs(x) / 0.5f;
                float t = Mathf.Pow(distanceFromCenter, falloffPower);
                Color color = Color.Lerp(innerColor, outerColor, t);

                int bottomIndex = i * 2;
                int topIndex = bottomIndex + 1;
                vertices[bottomIndex] = new Vector3(x, 0f, 0f);
                vertices[topIndex] = new Vector3(x, 1f, 0f);
                uv[bottomIndex] = new Vector2(uvX, 0f);
                uv[topIndex] = new Vector2(uvX, 1f);
                colors[bottomIndex] = color;
                colors[topIndex] = color;

                if (i < columnCount - 1)
                {
                    int nextBottomIndex = bottomIndex + 2;
                    int nextTopIndex = topIndex + 2;
                    int triangleIndex = i * 6;
                    triangles[triangleIndex] = bottomIndex;
                    triangles[triangleIndex + 1] = topIndex;
                    triangles[triangleIndex + 2] = nextBottomIndex;
                    triangles[triangleIndex + 3] = nextBottomIndex;
                    triangles[triangleIndex + 4] = topIndex;
                    triangles[triangleIndex + 5] = nextTopIndex;
                }
            }

            Mesh mesh = new()
            {
                vertices = vertices,
                uv = uv,
                colors = colors,
                triangles = triangles,
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreateGradientMaterial(Texture texture)
        {
            gradientShader ??= Shader.Find("Sprites/Default");
            return new Material(gradientShader) { mainTexture = texture };
        }
    }
}
