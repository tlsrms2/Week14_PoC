using UnityEngine;

namespace Week14.Enemy
{
    internal static class ArsonistHazardVisual
    {
        internal const int SortingOrder = -5;

        private static Material material;

        public static void ConfigureCircle(GameObject target, float radius, Color color, bool fireLike)
        {
            if (target == null)
            {
                return;
            }

            LineRenderer legacyLine = target.GetComponent<LineRenderer>();
            if (legacyLine != null)
            {
                legacyLine.enabled = false;
            }

            ArsonistHazardBlobVisual blob = target.GetComponent<ArsonistHazardBlobVisual>();
            if (blob == null)
            {
                blob = target.AddComponent<ArsonistHazardBlobVisual>();
            }

            blob.Configure(Mathf.Max(0.05f, radius), color, SortingOrder, fireLike, ResolveMaterial());
        }

        private static Material ResolveMaterial()
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

    [AddComponentMenu("")]
    internal sealed class ArsonistHazardBlobVisual : MonoBehaviour
    {
        private const int SegmentCount = 22;
        private const int MainLobeIndex = 0;
        private const float InnerRingScale = 0.58f;
        private const float EdgeScaleMin = 0.7f;
        private const float EdgeScaleMax = 1.22f;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private BlobLobe[] lobes;
        private float[] edgeScales;
        private bool shapeConfigured;
        private bool shapeFireLike;

        public void Configure(float radius, Color color, int sortingOrder, bool fireLike, Material material)
        {
            EnsureComponents(material, sortingOrder);
            EnsureShape(fireLike);
            BuildMesh(radius, color);
        }

        private void EnsureComponents(Material material, int sortingOrder)
        {
            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
                if (meshFilter == null)
                {
                    meshFilter = gameObject.AddComponent<MeshFilter>();
                }
            }

            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
                if (meshRenderer == null)
                {
                    meshRenderer = gameObject.AddComponent<MeshRenderer>();
                }
            }

            if (mesh == null)
            {
                mesh = new Mesh { name = "ArsonistHazardBlob" };
                mesh.MarkDynamic();
                meshFilter.sharedMesh = mesh;
            }

            meshRenderer.sharedMaterial = material;
            meshRenderer.sortingOrder = sortingOrder;
        }

        private void EnsureShape(bool fireLike)
        {
            if (shapeConfigured && shapeFireLike == fireLike && lobes != null && edgeScales != null)
            {
                return;
            }

            shapeConfigured = true;
            shapeFireLike = fireLike;

            int seed = Mathf.Abs(gameObject.GetInstanceID());
            int lobeCount = fireLike ? 6 : 7;
            lobes = new BlobLobe[lobeCount];
            edgeScales = new float[lobeCount * SegmentCount];

            lobes[MainLobeIndex] = new BlobLobe(
                Vector2.zero,
                fireLike ? 0.88f : 1f,
                fireLike ? RandomRange(seed, 100, 0.95f, 1.22f) : RandomRange(seed, 100, 1.25f, 1.85f),
                fireLike ? RandomRange(seed, 101, 0.88f, 1.35f) : RandomRange(seed, 101, 0.56f, 0.9f),
                RandomRange(seed, 102, 0f, 180f),
                1f);

            for (int lobeIndex = 1; lobeIndex < lobeCount; lobeIndex++)
            {
                float angle = RandomRange(seed, lobeIndex * 31, 0f, Mathf.PI * 2f);
                float distance = fireLike
                    ? RandomRange(seed, lobeIndex * 31 + 1, 0.18f, 0.62f)
                    : RandomRange(seed, lobeIndex * 31 + 1, 0.22f, 0.78f);
                Vector2 center = new(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance);
                float radiusScale = fireLike
                    ? RandomRange(seed, lobeIndex * 31 + 2, 0.22f, 0.52f)
                    : RandomRange(seed, lobeIndex * 31 + 2, 0.26f, 0.64f);
                float xScale = RandomRange(seed, lobeIndex * 31 + 3, 0.72f, fireLike ? 1.36f : 1.75f);
                float yScale = RandomRange(seed, lobeIndex * 31 + 4, fireLike ? 0.7f : 0.42f, 1.05f);
                float rotation = RandomRange(seed, lobeIndex * 31 + 5, 0f, 180f);
                float alphaScale = RandomRange(seed, lobeIndex * 31 + 6, 0.48f, 0.82f);
                lobes[lobeIndex] = new BlobLobe(center, radiusScale, xScale, yScale, rotation, alphaScale);
            }

            float[] rawScales = new float[SegmentCount];
            for (int lobeIndex = 0; lobeIndex < lobeCount; lobeIndex++)
            {
                for (int i = 0; i < SegmentCount; i++)
                {
                    rawScales[i] = Mathf.Lerp(EdgeScaleMin, EdgeScaleMax, Hash01(seed, lobeIndex * 101 + i));
                }

                for (int i = 0; i < SegmentCount; i++)
                {
                    float previous = rawScales[(i - 1 + SegmentCount) % SegmentCount];
                    float current = rawScales[i];
                    float next = rawScales[(i + 1) % SegmentCount];
                    edgeScales[lobeIndex * SegmentCount + i] = (previous + current * 2f + next) * 0.25f;
                }
            }
        }

        private void BuildMesh(float radius, Color color)
        {
            int lobeCount = lobes != null ? lobes.Length : 0;
            int verticesPerLobe = 1 + SegmentCount * 2;
            int vertexCount = verticesPerLobe * lobeCount;
            Vector3[] vertices = new Vector3[vertexCount];
            Color[] colors = new Color[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            int[] triangles = new int[SegmentCount * 9 * lobeCount];

            Color centerColor = color;
            centerColor.a *= 0.82f;
            Color innerColor = color;
            innerColor.a *= 0.56f;
            Color outerColor = color;
            outerColor.a = 0f;

            int triangleIndex = 0;
            for (int lobeIndex = 0; lobeIndex < lobeCount; lobeIndex++)
            {
                BlobLobe lobe = lobes[lobeIndex];
                int baseIndex = lobeIndex * verticesPerLobe;
                Vector2 lobeCenter = lobe.Center * radius;
                Color lobeCenterColor = centerColor;
                Color lobeInnerColor = innerColor;
                lobeCenterColor.a *= lobe.AlphaScale;
                lobeInnerColor.a *= lobe.AlphaScale;

                vertices[baseIndex] = lobeCenter;
                colors[baseIndex] = lobeCenterColor;
                uvs[baseIndex] = new Vector2(0.5f, 0.5f);

                for (int i = 0; i < SegmentCount; i++)
                {
                    float angle = Mathf.PI * 2f * i / SegmentCount;
                    Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                    float edgeRadius = radius * lobe.RadiusScale * edgeScales[lobeIndex * SegmentCount + i];
                    int innerIndex = baseIndex + 1 + i;
                    int outerIndex = baseIndex + 1 + SegmentCount + i;
                    Vector2 innerPoint = lobeCenter + Rotate(Scale(direction, lobe.XScale, lobe.YScale) * (edgeRadius * InnerRingScale), lobe.RotationDegrees);
                    Vector2 outerPoint = lobeCenter + Rotate(Scale(direction, lobe.XScale, lobe.YScale) * edgeRadius, lobe.RotationDegrees);

                    vertices[innerIndex] = innerPoint;
                    vertices[outerIndex] = outerPoint;
                    colors[innerIndex] = lobeInnerColor;
                    colors[outerIndex] = outerColor;
                    uvs[innerIndex] = direction * 0.32f + Vector2.one * 0.5f;
                    uvs[outerIndex] = direction * 0.5f + Vector2.one * 0.5f;
                }

                for (int i = 0; i < SegmentCount; i++)
                {
                    int next = (i + 1) % SegmentCount;
                    int inner = baseIndex + 1 + i;
                    int nextInner = baseIndex + 1 + next;
                    int outer = baseIndex + 1 + SegmentCount + i;
                    int nextOuter = baseIndex + 1 + SegmentCount + next;

                    triangles[triangleIndex++] = baseIndex;
                    triangles[triangleIndex++] = inner;
                    triangles[triangleIndex++] = nextInner;

                    triangles[triangleIndex++] = inner;
                    triangles[triangleIndex++] = outer;
                    triangles[triangleIndex++] = nextOuter;

                    triangles[triangleIndex++] = inner;
                    triangles[triangleIndex++] = nextOuter;
                    triangles[triangleIndex++] = nextInner;
                }
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * radius * 3.1f);
        }

        private static Vector2 Scale(Vector2 value, float xScale, float yScale)
        {
            return new Vector2(value.x * xScale, value.y * yScale);
        }

        private static Vector2 Rotate(Vector2 value, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(value.x * cos - value.y * sin, value.x * sin + value.y * cos);
        }

        private static float RandomRange(int seed, int index, float min, float max)
        {
            return Mathf.Lerp(min, max, Hash01(seed, index));
        }

        private static float Hash01(int seed, int index)
        {
            uint value = (uint)(seed + index * 374761393);
            value = (value ^ (value >> 13)) * 1274126177u;
            value ^= value >> 16;
            return (value & 0x00FFFFFF) / 16777215f;
        }

        private readonly struct BlobLobe
        {
            public BlobLobe(
                Vector2 center,
                float radiusScale,
                float xScale,
                float yScale,
                float rotationDegrees,
                float alphaScale)
            {
                Center = center;
                RadiusScale = radiusScale;
                XScale = xScale;
                YScale = yScale;
                RotationDegrees = rotationDegrees;
                AlphaScale = alphaScale;
            }

            public Vector2 Center { get; }
            public float RadiusScale { get; }
            public float XScale { get; }
            public float YScale { get; }
            public float RotationDegrees { get; }
            public float AlphaScale { get; }
        }
    }
}
