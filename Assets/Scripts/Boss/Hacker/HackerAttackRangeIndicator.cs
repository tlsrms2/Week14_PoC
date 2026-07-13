using UnityEngine;

namespace Week14.Enemy
{
    internal sealed class HackerAttackRangeIndicator : MonoBehaviour
    {
        private const int ArcSegments = 24;

        private LineRenderer line;
        private Mesh fillMesh;
        private MeshRenderer fillRenderer;
        internal float Range { get; private set; }
        internal float ArcDegrees { get; private set; }

        internal static HackerAttackRangeIndicator CreateArc(Vector2 origin, Vector2 direction, float range, float arcDegrees)
        {
            HackerAttackRangeIndicator indicator = Create();
            indicator.SetArc(origin, direction, range, arcDegrees);
            return indicator;
        }

        internal static HackerAttackRangeIndicator CreateEllipse(
            Vector2 center,
            float majorRadius,
            float minorRadius,
            float angleDegrees)
        {
            HackerAttackRangeIndicator indicator = Create();
            indicator.SetEllipse(center, majorRadius, minorRadius, angleDegrees);
            return indicator;
        }

        internal static HackerAttackRangeIndicator CreateThrust(Vector2 origin, Vector2 direction, float length, float width)
        {
            HackerAttackRangeIndicator indicator = Create();
            indicator.SetThrust(origin, direction, length, width);
            return indicator;
        }

        internal static HackerAttackRangeIndicator CreateCircle(Vector2 center, float radius)
        {
            HackerAttackRangeIndicator indicator = Create();
            indicator.SetCircle(center, radius);
            return indicator;
        }

        internal static HackerAttackRangeIndicator CreateRing(Vector2 center, float innerRadius, float outerRadius)
        {
            HackerAttackRangeIndicator indicator = Create();
            indicator.SetRing(center, innerRadius, outerRadius);
            return indicator;
        }

        internal static void Destroy(HackerAttackRangeIndicator indicator)
        {
            if (indicator != null)
            {
                Object.Destroy(indicator.gameObject);
            }
        }

        internal void SetArc(Vector2 origin, Vector2 direction, float range, float arcDegrees)
        {
            line.loop = false;
            Range = Mathf.Max(0.05f, range);
            ArcDegrees = Mathf.Clamp(arcDegrees, 1f, 360f);
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;

            float centerAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float halfAngle = ArcDegrees * 0.5f;
            line.positionCount = ArcSegments + 3;
            line.SetPosition(0, origin);
            for (int i = 0; i <= ArcSegments; i++)
            {
                float angle = (centerAngle - halfAngle + ArcDegrees * i / ArcSegments) * Mathf.Deg2Rad;
                line.SetPosition(i + 1, origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Range);
            }

            line.SetPosition(ArcSegments + 2, origin);
            UpdateArcFill(origin, direction);
        }

        internal void SetArcFillVisible(bool visible)
        {
            SetFillVisible(visible);
        }

        internal void SetFillVisible(bool visible)
        {
            if (fillRenderer != null)
            {
                fillRenderer.enabled = visible;
            }
        }

        internal void SetThrust(Vector2 origin, Vector2 direction, float length, float width)
        {
            line.loop = false;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x) * Mathf.Max(0.025f, width * 0.5f);
            Vector2 end = origin + direction * Mathf.Max(0.05f, length);

            line.positionCount = 5;
            line.SetPosition(0, origin + perpendicular);
            line.SetPosition(1, end + perpendicular);
            line.SetPosition(2, end - perpendicular);
            line.SetPosition(3, origin - perpendicular);
            line.SetPosition(4, origin + perpendicular);
            UpdateThrustFill(origin, direction, length, width);
        }

        internal void SetEllipse(Vector2 center, float majorRadius, float minorRadius, float angleDegrees)
        {
            float major = Mathf.Max(0.05f, majorRadius);
            float minor = Mathf.Max(0.05f, minorRadius);
            float radians = angleDegrees * Mathf.Deg2Rad;
            Vector2 majorAxis = new(Mathf.Cos(radians), Mathf.Sin(radians));
            Vector2 minorAxis = new(-majorAxis.y, majorAxis.x);

            line.loop = true;
            line.positionCount = ArcSegments;
            for (int i = 0; i < ArcSegments; i++)
            {
                float angle = Mathf.PI * 2f * i / ArcSegments;
                Vector2 point = center
                    + majorAxis * (Mathf.Cos(angle) * major)
                    + minorAxis * (Mathf.Sin(angle) * minor);
                line.SetPosition(i, point);
            }

            UpdateEllipseFill(center, major, minor, majorAxis, minorAxis);
        }

        internal void SetCircle(Vector2 center, float radius)
        {
            line.loop = false;
            float safeRadius = Mathf.Max(0.05f, radius);
            line.positionCount = ArcSegments + 1;
            for (int i = 0; i <= ArcSegments; i++)
            {
                float angle = Mathf.PI * 2f * i / ArcSegments;
                line.SetPosition(i, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * safeRadius);
            }

            UpdateEllipseFill(center, safeRadius, safeRadius, Vector2.right, Vector2.up);
        }

        internal void SetRing(Vector2 center, float innerRadius, float outerRadius)
        {
            line.loop = true;
            float outer = Mathf.Max(0.05f, outerRadius);
            float inner = Mathf.Clamp(innerRadius, 0f, outer - 0.01f);
            line.positionCount = ArcSegments;
            for (int i = 0; i < ArcSegments; i++)
            {
                float angle = Mathf.PI * 2f * i / ArcSegments;
                line.SetPosition(i, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * outer);
            }

            UpdateRingFill(center, inner, outer);
        }

        private static HackerAttackRangeIndicator Create()
        {
            GameObject indicatorObject = new("HackerAttackRangeIndicator");
            HackerAttackRangeIndicator indicator = indicatorObject.AddComponent<HackerAttackRangeIndicator>();
            indicator.CreateLine();
            return indicator;
        }

        private void CreateLine()
        {
            line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = false;
            line.startWidth = 0.045f;
            line.endWidth = 0.045f;
            line.sortingOrder = 19;
            Color color = new(1f, 0.25f, 0.08f, 0.9f);
            line.startColor = color;
            line.endColor = color;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                line.material = new Material(shader);
            }

            CreateArcFill(shader);
        }

        private void CreateArcFill(Shader shader)
        {
            GameObject fillObject = new("HackerAttackRangeFill");
            fillObject.transform.SetParent(transform, false);
            MeshFilter meshFilter = fillObject.AddComponent<MeshFilter>();
            fillRenderer = fillObject.AddComponent<MeshRenderer>();
            fillRenderer.sortingOrder = 18;
            fillRenderer.enabled = false;
            fillMesh = new Mesh { name = "HackerAttackRangeFillMesh" };
            meshFilter.sharedMesh = fillMesh;
            if (shader != null)
            {
                Material material = new Material(shader) { color = new Color(1f, 0.25f, 0.08f, 0.24f) };
                fillRenderer.sharedMaterial = material;
            }
        }

        private void UpdateArcFill(Vector2 origin, Vector2 direction)
        {
            if (fillMesh == null)
            {
                return;
            }

            float centerAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float halfAngle = ArcDegrees * 0.5f;
            Vector3[] vertices = new Vector3[ArcSegments + 2];
            int[] triangles = new int[ArcSegments * 3];
            vertices[0] = origin;
            for (int i = 0; i <= ArcSegments; i++)
            {
                float angle = (centerAngle - halfAngle + ArcDegrees * i / ArcSegments) * Mathf.Deg2Rad;
                vertices[i + 1] = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Range;
            }

            for (int i = 0; i < ArcSegments; i++)
            {
                int triangleIndex = i * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = i + 1;
                triangles[triangleIndex + 2] = i + 2;
            }

            fillMesh.Clear();
            fillMesh.vertices = vertices;
            fillMesh.triangles = triangles;
            fillMesh.RecalculateBounds();
        }

        private void UpdateEllipseFill(
            Vector2 center,
            float majorRadius,
            float minorRadius,
            Vector2 majorAxis,
            Vector2 minorAxis)
        {
            if (fillMesh == null)
            {
                return;
            }

            Vector3[] vertices = new Vector3[ArcSegments + 1];
            int[] triangles = new int[ArcSegments * 3];
            vertices[0] = center;
            for (int i = 0; i < ArcSegments; i++)
            {
                float angle = Mathf.PI * 2f * i / ArcSegments;
                vertices[i + 1] = center
                    + majorAxis * (Mathf.Cos(angle) * majorRadius)
                    + minorAxis * (Mathf.Sin(angle) * minorRadius);
            }

            for (int i = 0; i < ArcSegments; i++)
            {
                int next = i == ArcSegments - 1 ? 1 : i + 2;
                int triangleIndex = i * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = i + 1;
                triangles[triangleIndex + 2] = next;
            }

            fillMesh.Clear();
            fillMesh.vertices = vertices;
            fillMesh.triangles = triangles;
            fillMesh.RecalculateBounds();
        }

        private void UpdateRingFill(Vector2 center, float innerRadius, float outerRadius)
        {
            if (fillMesh == null)
            {
                return;
            }

            Vector3[] vertices = new Vector3[(ArcSegments + 1) * 2];
            int[] triangles = new int[ArcSegments * 6];
            for (int i = 0; i <= ArcSegments; i++)
            {
                float angle = Mathf.PI * 2f * i / ArcSegments;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                vertices[i * 2] = center + direction * outerRadius;
                vertices[i * 2 + 1] = center + direction * innerRadius;
            }

            for (int i = 0; i < ArcSegments; i++)
            {
                int vertexIndex = i * 2;
                int triangleIndex = i * 6;
                triangles[triangleIndex] = vertexIndex;
                triangles[triangleIndex + 1] = vertexIndex + 2;
                triangles[triangleIndex + 2] = vertexIndex + 3;
                triangles[triangleIndex + 3] = vertexIndex;
                triangles[triangleIndex + 4] = vertexIndex + 3;
                triangles[triangleIndex + 5] = vertexIndex + 1;
            }

            fillMesh.Clear();
            fillMesh.vertices = vertices;
            fillMesh.triangles = triangles;
            fillMesh.RecalculateBounds();
        }

        private void UpdateThrustFill(Vector2 origin, Vector2 direction, float length, float width)
        {
            if (fillMesh == null)
            {
                return;
            }

            Vector2 perpendicular = new Vector2(-direction.y, direction.x) * Mathf.Max(0.025f, width * 0.5f);
            Vector2 end = origin + direction * Mathf.Max(0.05f, length);
            Vector3[] vertices =
            {
                origin + perpendicular,
                end + perpendicular,
                end - perpendicular,
                origin - perpendicular
            };
            int[] triangles = { 0, 1, 2, 0, 2, 3 };
            fillMesh.Clear();
            fillMesh.vertices = vertices;
            fillMesh.triangles = triangles;
            fillMesh.RecalculateBounds();
        }

        private void OnDestroy()
        {
            if (fillMesh != null)
            {
                Destroy(fillMesh);
            }
        }
    }
}
