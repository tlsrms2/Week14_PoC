using UnityEngine;

namespace Week14.Combat
{
    public sealed class BaseballBatRangePreviewVfx : MonoBehaviour
    {
        private const int ArcSegments = 32;

        private static Material previewMaterial;

        private Mesh mesh;
        private Vector3[] vertices;
        private Color[] colors;
        private int[] triangles;

        public void Initialize()
        {
            MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = GetPreviewMaterial();
            meshRenderer.sortingOrder = 68;

            vertices = new Vector3[ArcSegments + 2];
            colors = new Color[vertices.Length];
            triangles = new int[ArcSegments * 3];
            vertices[0] = Vector3.zero;

            for (int i = 0; i < ArcSegments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            mesh = new Mesh { name = "BaseballBatRangePreviewMesh" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            meshFilter.mesh = mesh;
        }

        public void UpdatePreview(Vector3 origin, Vector2 direction, float radius, Color color)
        {
            if (mesh == null || radius <= 0f)
            {
                return;
            }

            origin.z = 0f;
            transform.position = origin;

            Vector2 forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            float baseAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
            vertices[0] = Vector3.zero;

            for (int i = 0; i <= ArcSegments; i++)
            {
                float t = (float)i / ArcSegments;
                float angleRad = (baseAngle - 90f + t * 180f) * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f) * radius;
            }

            Color previewColor = color;
            previewColor.a *= 0.45f;
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = previewColor;
            }

            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.RecalculateBounds();
        }

        private static Material GetPreviewMaterial()
        {
            if (previewMaterial != null)
            {
                return previewMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            previewMaterial = shader != null ? new Material(shader) : null;
            return previewMaterial;
        }
    }
}
