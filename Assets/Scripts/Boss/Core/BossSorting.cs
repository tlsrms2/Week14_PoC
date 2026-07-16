using UnityEngine;

namespace Week14.Enemy
{
    public static class BossSorting
    {
        public const string LayerName = "Boss";
        private static readonly int LayerId = SortingLayer.NameToID(LayerName);

        public static void Apply(Renderer renderer)
        {
            if (renderer != null)
            {
                renderer.sortingLayerID = LayerId;
            }
        }

        public static void ApplyToChildren(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Apply(renderers[i]);
            }
        }
    }
}
