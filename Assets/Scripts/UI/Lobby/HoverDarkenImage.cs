using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Week14.UI
{
    public sealed class HoverDarkenImage : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Graphic targetGraphic;
        [SerializeField] private SpriteRenderer targetSpriteRenderer;
        [SerializeField, Range(0f, 1f)] private float darkenAmount = 0.25f;
        [SerializeField, Min(0f)] private float transitionSeconds = 0.08f;

        private Color baseColor;
        private Color targetColor;
        private bool hasBaseColor;

        private bool HasTarget => targetGraphic != null || targetSpriteRenderer != null;

        private void Awake()
        {
            if (HasTarget)
            {
                return;
            }

            targetGraphic = GetComponent<Graphic>();
            if (targetGraphic == null)
            {
                targetSpriteRenderer = GetComponent<SpriteRenderer>();
            }
        }

        private void OnEnable()
        {
            CacheBaseColor();
            targetColor = baseColor;
            ApplyColor(baseColor);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            targetColor = GetDarkenedColor();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            targetColor = baseColor;
        }

        private void Update()
        {
            if (!HasTarget)
            {
                return;
            }

            if (transitionSeconds <= 0f)
            {
                ApplyColor(targetColor);
                return;
            }

            float maxDelta = Time.unscaledDeltaTime / transitionSeconds;
            ApplyColor(MoveColor(GetColor(), targetColor, maxDelta));
        }

        private void CacheBaseColor()
        {
            if (hasBaseColor || !HasTarget)
            {
                return;
            }

            baseColor = GetColor();
            hasBaseColor = true;
        }

        private Color GetDarkenedColor()
        {
            Color darkened = baseColor * (1f - darkenAmount);
            darkened.a = baseColor.a;
            return darkened;
        }

        private Color GetColor()
        {
            return targetGraphic != null ? targetGraphic.color : targetSpriteRenderer.color;
        }

        private void ApplyColor(Color color)
        {
            if (targetGraphic != null)
            {
                targetGraphic.color = color;
            }
            else if (targetSpriteRenderer != null)
            {
                targetSpriteRenderer.color = color;
            }
        }

        private static Color MoveColor(Color current, Color target, float maxDelta)
        {
            return new Color(
                Mathf.MoveTowards(current.r, target.r, maxDelta),
                Mathf.MoveTowards(current.g, target.g, maxDelta),
                Mathf.MoveTowards(current.b, target.b, maxDelta),
                Mathf.MoveTowards(current.a, target.a, maxDelta));
        }
    }
}
