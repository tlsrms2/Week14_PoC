using UnityEngine;
using UnityEngine.UI;
using Week14.Enemy;

namespace Week14.UI
{
    public sealed class BossEnrageBarView : MonoBehaviour
    {
        private const string FillName = "BossEnrageFill";

        [SerializeField] private BossAI target;
        [SerializeField] private Color backgroundColor = new(0.24f, 0.24f, 0.24f, 0.86f);
        [SerializeField] private Color phase0FillColor = new(1f, 0.48f, 0.08f, 1f);
        [SerializeField] private Color phase1FillColor = new(0.95f, 0.04f, 0.03f, 1f);

        private RectTransform rectTransform;
        private Image backgroundImage;
        private Image fillImage;

        private static Sprite solidSprite;

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

        public static BossEnrageBarView CreateUnder(Transform parent)
        {
            GameObject viewObject = new("BossEnrageBarView", typeof(RectTransform));
            viewObject.transform.SetParent(parent, false);

            RectTransform rect = viewObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 44f);
            rect.sizeDelta = new Vector2(1080f, 16f);

            return viewObject.AddComponent<BossEnrageBarView>();
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
            if (target == null)
            {
                SetValue(0, 0f);
                return;
            }

            SetValue(target.CurrentEnragePhase, target.CurrentEnrageProgress);
        }

        private void Subscribe()
        {
            if (target != null)
            {
                target.EnrageChanged += HandleEnrageChanged;
            }
        }

        private void Unsubscribe()
        {
            if (target != null)
            {
                target.EnrageChanged -= HandleEnrageChanged;
            }
        }

        private void HandleEnrageChanged(int phase, float progress)
        {
            SetValue(phase, progress);
        }

        private void SetValue(int phase, float progress)
        {
            EnsureView();

            fillImage.color = phase <= 0 ? phase0FillColor : phase1FillColor;
            fillImage.fillAmount = phase >= 2 ? 1f : Mathf.Clamp01(progress);
        }

        private void EnsureView()
        {
            rectTransform ??= transform as RectTransform;
            if (rectTransform == null)
            {
                rectTransform = gameObject.AddComponent<RectTransform>();
            }

            backgroundImage ??= GetComponent<Image>();
            if (backgroundImage == null)
            {
                backgroundImage = gameObject.AddComponent<Image>();
            }

            backgroundImage.sprite = GetSolidSprite();
            backgroundImage.type = Image.Type.Sliced;
            backgroundImage.raycastTarget = false;
            backgroundImage.color = backgroundColor;

            if (fillImage == null)
            {
                Transform existing = rectTransform.Find(FillName);
                fillImage = existing != null ? existing.GetComponent<Image>() : null;
            }

            if (fillImage == null)
            {
                GameObject fillObject = new(FillName, typeof(RectTransform));
                fillObject.transform.SetParent(rectTransform, false);
                fillImage = fillObject.AddComponent<Image>();
            }

            RectTransform fillRect = fillImage.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.localScale = Vector3.one;
            fillRect.localRotation = Quaternion.identity;

            fillImage.sprite = GetSolidSprite();
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillClockwise = true;
            fillImage.raycastTarget = false;
        }

        private static Sprite GetSolidSprite()
        {
            if (solidSprite != null)
            {
                return solidSprite;
            }

            Texture2D texture = new(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            solidSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return solidSprite;
        }
    }
}
