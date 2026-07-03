using UnityEngine;

namespace Week14.Enemy
{
    internal static class ArsonistHazardVisual
    {
        private const int CircleSegments = 40;
        private static Material material;

        public static void ConfigureCircle(GameObject target, float radius, Color color, int sortingOrder)
        {
            if (target == null)
            {
                return;
            }

            LineRenderer line = target.GetComponent<LineRenderer>();
            if (line == null)
            {
                line = target.AddComponent<LineRenderer>();
            }

            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = CircleSegments;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.startWidth = Mathf.Max(0.025f, radius * 0.08f);
            line.endWidth = line.startWidth;
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = sortingOrder;
            line.material = ResolveMaterial();

            for (int i = 0; i < CircleSegments; i++)
            {
                float radians = Mathf.PI * 2f * i / CircleSegments;
                line.SetPosition(i, new Vector3(Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius, 0f));
            }
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
}
