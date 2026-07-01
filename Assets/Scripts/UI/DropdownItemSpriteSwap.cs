using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Week14.UI
{
    public sealed class DropdownItemSpriteSwap : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image targetImage;
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite hoverSprite;
        [SerializeField] private bool disableSelectableTransition = true;

        private Selectable selectable;
        private bool cachedNormalSprite;

        private void Awake()
        {
            CacheReferences();
            ApplySelectableTransition();
        }

        private void OnEnable()
        {
            CacheReferences();
            CacheNormalSprite();
            ApplySprite(false);
        }

        private void OnDisable()
        {
            ApplySprite(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            ApplySprite(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ApplySprite(false);
        }

        private void CacheReferences()
        {
            if (targetImage == null)
            {
                Transform background = FindChildRecursive(transform, "Item Background");
                targetImage = background != null ? background.GetComponent<Image>() : GetComponent<Image>();
                targetImage ??= GetComponentInChildren<Image>(true);
            }

            selectable ??= GetComponent<Selectable>();
            selectable ??= GetComponentInParent<Selectable>();
        }

        private void ApplySelectableTransition()
        {
            if (disableSelectableTransition && selectable != null)
            {
                selectable.transition = Selectable.Transition.None;
            }
        }

        private void CacheNormalSprite()
        {
            if (cachedNormalSprite || targetImage == null || normalSprite != null)
            {
                return;
            }

            normalSprite = targetImage.sprite;
            cachedNormalSprite = true;
        }

        private void ApplySprite(bool hovering)
        {
            if (targetImage == null)
            {
                return;
            }

            Sprite sprite = hovering && hoverSprite != null ? hoverSprite : normalSprite;
            if (sprite != null)
            {
                targetImage.sprite = sprite;
            }
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            foreach (Transform child in root)
            {
                if (child.name == childName)
                {
                    return child;
                }

                Transform match = FindChildRecursive(child, childName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
