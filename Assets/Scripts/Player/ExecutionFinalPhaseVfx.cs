using System.Collections.Generic;
using UnityEngine;

namespace Week14.Combat
{
    internal sealed class ExecutionFinalPhaseVfx
    {
        private sealed class RendererState
        {
            internal SpriteRenderer Renderer;
            internal Color Color;
            internal int SortingLayerId;
            internal int SortingOrder;
        }

        private static readonly Vector2[] OutlineDirections =
        {
            Vector2.left,
            Vector2.right,
            Vector2.up,
            Vector2.down
        };

        private readonly List<RendererState> rendererStates = new();
        private readonly List<GameObject> outlineObjects = new();
        private Material silhouetteMaterial;

        internal int BossBackSortingOrder { get; private set; } = 66;
        internal int BossFrontSortingOrder { get; private set; } = 68;

        internal void Begin(
            SpriteRenderer[] playerRenderers,
            SpriteRenderer[] bossRenderers,
            int sortingLayerId,
            int minimumSortingOrder)
        {
            Restore();
            HashSet<SpriteRenderer> captured = new();
            CaptureRenderers(playerRenderers, sortingLayerId, minimumSortingOrder, captured);
            int bossRendererStartIndex = rendererStates.Count;
            CaptureRenderers(bossRenderers, sortingLayerId, minimumSortingOrder, captured);
            UpdateBossLineSortingOrders(bossRendererStartIndex, minimumSortingOrder);
        }

        internal void ShowWhiteOutline(float widthPixels)
        {
            HideWhiteOutline();
            Material material = GetSilhouetteMaterial();
            if (material == null)
            {
                return;
            }

            float sanitizedWidth = Mathf.Max(0.05f, widthPixels);
            for (int i = 0; i < rendererStates.Count; i++)
            {
                SpriteRenderer source = rendererStates[i].Renderer;
                if (!CanCapture(source))
                {
                    continue;
                }

                float pixelsPerUnit = Mathf.Max(1f, source.sprite.pixelsPerUnit);
                float localOffset = sanitizedWidth / pixelsPerUnit;
                for (int directionIndex = 0; directionIndex < OutlineDirections.Length; directionIndex++)
                {
                    CreateOutlineCopy(source, OutlineDirections[directionIndex] * localOffset, material);
                }
            }
        }

        internal void HideWhiteOutline()
        {
            for (int i = 0; i < outlineObjects.Count; i++)
            {
                if (outlineObjects[i] != null)
                {
                    Object.Destroy(outlineObjects[i]);
                }
            }

            outlineObjects.Clear();
        }

        internal void Restore()
        {
            HideWhiteOutline();
            for (int i = 0; i < rendererStates.Count; i++)
            {
                RendererState state = rendererStates[i];
                if (state.Renderer == null)
                {
                    continue;
                }

                state.Renderer.color = state.Color;
                state.Renderer.sortingLayerID = state.SortingLayerId;
                state.Renderer.sortingOrder = state.SortingOrder;
            }

            rendererStates.Clear();
        }

        private void CaptureRenderers(
            SpriteRenderer[] renderers,
            int sortingLayerId,
            int minimumSortingOrder,
            HashSet<SpriteRenderer> captured)
        {
            if (renderers == null)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (!CanCapture(renderer) || !captured.Add(renderer))
                {
                    continue;
                }

                Color originalColor = renderer.color;
                rendererStates.Add(new RendererState
                {
                    Renderer = renderer,
                    Color = originalColor,
                    SortingLayerId = renderer.sortingLayerID,
                    SortingOrder = renderer.sortingOrder
                });

                renderer.color = new Color(0f, 0f, 0f, originalColor.a);
                renderer.sortingLayerID = sortingLayerId;
                renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, minimumSortingOrder);
            }
        }

        private void UpdateBossLineSortingOrders(int bossRendererStartIndex, int minimumSortingOrder)
        {
            int minimumBossOrder = int.MaxValue;
            int maximumBossOrder = int.MinValue;
            for (int i = bossRendererStartIndex; i < rendererStates.Count; i++)
            {
                SpriteRenderer renderer = rendererStates[i].Renderer;
                if (renderer == null)
                {
                    continue;
                }

                minimumBossOrder = Mathf.Min(minimumBossOrder, renderer.sortingOrder);
                maximumBossOrder = Mathf.Max(maximumBossOrder, renderer.sortingOrder);
            }

            if (minimumBossOrder == int.MaxValue)
            {
                minimumBossOrder = minimumSortingOrder;
                maximumBossOrder = minimumSortingOrder;
            }

            BossBackSortingOrder = minimumBossOrder - 1;
            BossFrontSortingOrder = maximumBossOrder + 1;
        }

        private void CreateOutlineCopy(SpriteRenderer source, Vector2 localOffset, Material material)
        {
            GameObject outlineObject = new GameObject("FinalExecutionOutline")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = source.gameObject.layer
            };
            outlineObject.transform.SetParent(source.transform, false);
            outlineObject.transform.localPosition = localOffset;
            outlineObject.transform.localRotation = Quaternion.identity;
            outlineObject.transform.localScale = Vector3.one;

            SpriteRenderer outline = outlineObject.AddComponent<SpriteRenderer>();
            outline.sprite = source.sprite;
            outline.drawMode = source.drawMode;
            outline.size = source.size;
            outline.tileMode = source.tileMode;
            outline.flipX = source.flipX;
            outline.flipY = source.flipY;
            outline.maskInteraction = source.maskInteraction;
            outline.sortingLayerID = source.sortingLayerID;
            outline.sortingOrder = Mathf.Max(66, source.sortingOrder - 1);
            outline.sharedMaterial = material;
            outline.color = new Color(1f, 1f, 1f, source.color.a);
            outlineObjects.Add(outlineObject);
        }

        private Material GetSilhouetteMaterial()
        {
            if (silhouetteMaterial != null)
            {
                return silhouetteMaterial;
            }

            Shader shader = Resources.Load<Shader>("Shaders/ExecutionSilhouette");
            shader ??= Shader.Find("Week14/ExecutionSilhouette");
            if (shader == null)
            {
                Debug.LogWarning("Execution silhouette shader could not be found.");
                return null;
            }

            silhouetteMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            return silhouetteMaterial;
        }

        private static bool CanCapture(SpriteRenderer renderer)
        {
            return renderer != null
                && renderer.enabled
                && renderer.gameObject.activeInHierarchy
                && renderer.sprite != null
                && renderer.color.a > 0.001f
                && !HasShadowAncestor(renderer.transform);
        }

        private static bool HasShadowAncestor(Transform transform)
        {
            for (Transform current = transform; current != null; current = current.parent)
            {
                if (current.name == "Shadow")
                {
                    return true;
                }
            }

            return false;
        }
    }
}
