using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Week14.Enemy;

namespace Week14.UI
{
    public sealed class BossLivesView : MonoBehaviour
    {
        private const string IconName = "BossLifeIcon";

        [SerializeField] private BossAI target;
        [SerializeField, Min(1)] private int fallbackMaxLives = 1;
        [SerializeField, Min(1f)] private float iconSize = 34f;
        [SerializeField, Min(0f)] private float iconSpacing = 10f;
        [SerializeField] private Color activeColor = new(0.9f, 0.08f, 0.05f, 1f);
        [SerializeField] private Color spentColor = new(0.28f, 0.28f, 0.28f, 0.82f);
        [SerializeField] private Color backplateColor = new(0f, 0f, 0f, 0.62f);

        private readonly List<Image> icons = new();
        private RectTransform rectTransform;
        private Image backplate;

        private static Sprite lifeIconSprite;

        private void Awake()
        {
            EnsureView();
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void SetTarget(BossAI nextTarget)
        {
            if (target == nextTarget)
            {
                Refresh();
                return;
            }

            Unsubscribe();
            target = nextTarget;
            Subscribe();
            Refresh();
        }

        public void Refresh()
        {
            EnsureView();

            int maxLives = target != null ? target.MaxLives : fallbackMaxLives;
            int currentLives = target != null ? target.CurrentLives : maxLives;
            SetValue(currentLives, maxLives);
        }

        private void Subscribe()
        {
            if (target != null)
            {
                target.LivesChanged += HandleLivesChanged;
            }
        }

        private void Unsubscribe()
        {
            if (target != null)
            {
                target.LivesChanged -= HandleLivesChanged;
            }
        }

        private void HandleLivesChanged(int currentLives, int maxLives)
        {
            SetValue(currentLives, maxLives);
        }

        private void SetValue(int currentLives, int maxLives)
        {
            maxLives = Mathf.Max(1, maxLives);
            currentLives = Mathf.Clamp(currentLives, 0, maxLives);
            fallbackMaxLives = maxLives;

            EnsureIconCount(maxLives);
            LayoutIcons(maxLives);

            for (int i = 0; i < icons.Count; i++)
            {
                bool alive = i < currentLives;
                icons[i].color = alive ? activeColor : spentColor;
            }
        }

        private void EnsureView()
        {
            rectTransform ??= transform as RectTransform;
            if (rectTransform == null)
            {
                rectTransform = gameObject.AddComponent<RectTransform>();
            }

            backplate ??= GetComponent<Image>();
            if (backplate == null)
            {
                backplate = gameObject.AddComponent<Image>();
            }

            backplate.raycastTarget = false;
            backplate.color = backplateColor;
        }

        private void EnsureIconCount(int maxLives)
        {
            while (icons.Count < maxLives)
            {
                GameObject iconObject = new(IconName, typeof(RectTransform));
                iconObject.transform.SetParent(rectTransform, false);
                Image image = iconObject.AddComponent<Image>();
                image.sprite = GetLifeIconSprite();
                image.raycastTarget = false;
                image.preserveAspect = true;
                icons.Add(image);
            }

            while (icons.Count > maxLives)
            {
                Image image = icons[icons.Count - 1];
                icons.RemoveAt(icons.Count - 1);
                if (image != null)
                {
                    Destroy(image.gameObject);
                }
            }
        }

        private void LayoutIcons(int maxLives)
        {
            float totalWidth = maxLives * iconSize + Mathf.Max(0, maxLives - 1) * iconSpacing;
            rectTransform.sizeDelta = new Vector2(Mathf.Max(totalWidth + 18f, iconSize + 18f), iconSize + 8f);
            float startX = -totalWidth * 0.5f + iconSize * 0.5f;

            for (int i = 0; i < icons.Count; i++)
            {
                RectTransform iconRect = icons[i].rectTransform;
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.sizeDelta = new Vector2(iconSize, iconSize);
                iconRect.anchoredPosition = new Vector2(startX + i * (iconSize + iconSpacing), 0f);
                iconRect.localScale = Vector3.one;
                iconRect.localRotation = Quaternion.identity;
            }
        }

        private static Sprite GetLifeIconSprite()
        {
            if (lifeIconSprite != null)
            {
                return lifeIconSprite;
            }

            const int size = 48;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Vector2 center = new((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.36f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = new Vector2(x, y) - center;
                    float distance = point.magnitude;
                    float fill = Mathf.Clamp01(Mathf.InverseLerp(radius + 1.6f, radius - 1.6f, distance));
                    float ring = Mathf.Clamp01(Mathf.InverseLerp(radius + 2.2f, radius - 0.8f, distance))
                        * Mathf.Clamp01(Mathf.InverseLerp(radius - 5.4f, radius - 1.2f, distance));
                    float highlight = point.x < -radius * 0.18f && point.y > radius * 0.1f
                        ? Mathf.Clamp01(Mathf.InverseLerp(radius * 0.62f, radius * 0.18f, (point + new Vector2(radius * 0.22f, -radius * 0.2f)).magnitude))
                        : 0f;
                    float alpha = Mathf.Clamp01(fill * 0.78f + ring * 0.2f + highlight * 0.18f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            lifeIconSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            return lifeIconSprite;
        }
    }
}
