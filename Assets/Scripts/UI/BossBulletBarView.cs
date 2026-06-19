using UnityEngine;
using UnityEngine.UI;
using Week14.Combat;

namespace Week14.UI
{
    public sealed class BossBulletBarView : MonoBehaviour
    {
        private const string FillRootName = "FillRoot";
        private const string FillBackgroundName = "Background";
        private const string FillGhostName = "Ghost";
        private const string FillForegroundName = "Fill";

        [SerializeField] private BulletGauge target;
        [SerializeField] private Color normalColor = new(1f, 0.55f, 0.1f);
        [SerializeField] private Color emptyColor = Color.red;
        [Tooltip("배경 바 색상입니다.")]
        [SerializeField] private Color filledBackgroundColor = new(0.15f, 0.15f, 0.15f, 0.85f);
        [Tooltip("체력이 줄어들 때 보이는 반투명 잔상 색상입니다.")]
        [SerializeField] private Color filledGhostColor = new(1f, 1f, 1f, 0.55f);
        [Tooltip("반투명 잔상이 실제 체력만큼 줄어드는 속도입니다. 값이 클수록 빠르게 줄어듭니다.")]
        [SerializeField, Min(0.1f)] private float filledGhostShrinkSpeed = 14f;
        [Tooltip("총알이 0이 되어 처형 가능 상태일 때 체력바 색입니다.")]
        [SerializeField] private Color executionWindowColor = new(0.95f, 0.05f, 0.05f, 1f);
        [Tooltip("처형 가능 상태에서 체력바가 깜빡이는 속도입니다.")]
        [SerializeField, Min(0.1f)] private float executionWindowBlinkSpeed = 6f;
        [Tooltip("처형 가능 상태에서 깜빡일 때 가장 어두워지는 알파값입니다.")]
        [SerializeField, Range(0f, 1f)] private float executionWindowBlinkMinAlpha = 0.35f;

        private RectTransform fillRoot;
        private Image filledBackgroundImage;
        private Image filledGhostImage;
        private Image filledForegroundImage;
        private float filledRatio;
        private float filledGhostRatio;
        private bool executionWindowActive;
        private int displayedBulletCount = -1;

        private static Sprite solidFillSprite;

        private void OnEnable()
        {
            EnsureFilledView();
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            TickEffects();
        }

        public void SetTarget(BulletGauge nextTarget)
        {
            if (target == nextTarget)
            {
                return;
            }

            Unsubscribe();
            target = nextTarget;
            Subscribe();
            Refresh();
        }

        public void SetExecutionWindow(bool active, float remainingRatio)
        {
            executionWindowActive = active;
            if (!active)
            {
                return;
            }

            EnsureFilledView();
            if (filledForegroundImage == null)
            {
                return;
            }

            filledRatio = Mathf.Clamp01(remainingRatio);
            filledGhostRatio = filledRatio;
            filledForegroundImage.fillAmount = filledRatio;
            if (filledGhostImage != null)
            {
                filledGhostImage.fillAmount = filledGhostRatio;
            }
        }

        public void ClearExecutionWindow()
        {
            if (!executionWindowActive)
            {
                return;
            }

            executionWindowActive = false;
            displayedBulletCount = -1;

            if (filledForegroundImage != null)
            {
                filledForegroundImage.color = ResolveBarColor(target != null ? target.CurrentBullets : 0, target != null ? target.MaxBullets : 1);
            }

            Refresh();
        }

        private void Subscribe()
        {
            if (target == null)
            {
                return;
            }

            target.Changed += HandleChanged;
        }

        private void Unsubscribe()
        {
            if (target == null)
            {
                return;
            }

            target.Changed -= HandleChanged;
        }

        private void HandleChanged(int current, int max)
        {
            SetFilledValue(current, max);
        }

        private void Refresh()
        {
            if (target == null)
            {
                SetFilledValue(0, 1);
                return;
            }

            SetFilledValue(target.CurrentBullets, target.MaxBullets);
        }

        private void SetFilledValue(int current, int max)
        {
            if (executionWindowActive)
            {
                return;
            }

            EnsureFilledView();
            if (filledForegroundImage == null)
            {
                return;
            }

            bool hasPreviousValue = displayedBulletCount >= 0;
            int bulletCount = Mathf.Max(0, current);
            float ratio = Mathf.Clamp01(bulletCount / (float)Mathf.Max(1, max));

            filledRatio = ratio;
            filledForegroundImage.color = ResolveBarColor(current, max);
            filledForegroundImage.fillAmount = filledRatio;

            if (!hasPreviousValue || ratio > filledGhostRatio)
            {
                filledGhostRatio = ratio;
            }

            filledGhostImage.fillAmount = filledGhostRatio;
            displayedBulletCount = bulletCount;
        }

        private void EnsureFilledView()
        {
            RectTransform parent = transform as RectTransform;
            if (parent == null)
            {
                return;
            }

            if (fillRoot == null || fillRoot.parent != parent)
            {
                Transform existing = parent.Find(FillRootName);
                fillRoot = existing != null ? existing.GetComponent<RectTransform>() : null;
                if (fillRoot == null)
                {
                    GameObject rootObject = new(FillRootName, typeof(RectTransform));
                    rootObject.transform.SetParent(parent, false);
                    fillRoot = rootObject.GetComponent<RectTransform>();
                }

                fillRoot.anchorMin = Vector2.zero;
                fillRoot.anchorMax = Vector2.one;
                fillRoot.offsetMin = Vector2.zero;
                fillRoot.offsetMax = Vector2.zero;
            }

            fillRoot.gameObject.SetActive(true);

            if (filledBackgroundImage == null)
            {
                filledBackgroundImage = CreateFillImage(FillBackgroundName, filledBackgroundColor);
            }

            if (filledGhostImage == null)
            {
                filledGhostImage = CreateFillImage(FillGhostName, filledGhostColor);
                filledGhostImage.type = Image.Type.Filled;
                filledGhostImage.fillMethod = Image.FillMethod.Horizontal;
                filledGhostImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            }

            if (filledForegroundImage == null)
            {
                filledForegroundImage = CreateFillImage(FillForegroundName, normalColor);
                filledForegroundImage.type = Image.Type.Filled;
                filledForegroundImage.fillMethod = Image.FillMethod.Horizontal;
                filledForegroundImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            }
        }

        private Image CreateFillImage(string imageName, Color color)
        {
            Transform existing = fillRoot.Find(imageName);
            Image image = existing != null ? existing.GetComponent<Image>() : null;
            if (image == null)
            {
                GameObject imageObject = new(imageName, typeof(RectTransform));
                imageObject.transform.SetParent(fillRoot, false);
                image = imageObject.AddComponent<Image>();
            }

            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;

            image.sprite = GetSolidFillSprite();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Sprite GetSolidFillSprite()
        {
            if (solidFillSprite != null)
            {
                return solidFillSprite;
            }

            Texture2D texture = new(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            solidFillSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return solidFillSprite;
        }

        private void TickEffects()
        {
            float deltaTime = Time.unscaledDeltaTime;

            if (executionWindowActive)
            {
                TickExecutionWindowBlink();
                return;
            }

            if (filledGhostImage != null && filledGhostRatio > filledRatio)
            {
                filledGhostRatio = Mathf.Max(filledRatio, Mathf.Lerp(filledGhostRatio, filledRatio, 1f - Mathf.Exp(-deltaTime * filledGhostShrinkSpeed)));
                filledGhostImage.fillAmount = filledGhostRatio;
            }
        }

        private void TickExecutionWindowBlink()
        {
            if (filledForegroundImage == null)
            {
                return;
            }

            float blink = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * executionWindowBlinkSpeed * Mathf.PI * 2f);
            float alpha = Mathf.Lerp(executionWindowBlinkMinAlpha, 1f, blink);
            Color color = executionWindowColor;
            color.a *= alpha;
            filledForegroundImage.color = color;

            if (filledGhostImage != null)
            {
                filledGhostImage.fillAmount = filledRatio;
            }
        }

        private Color ResolveBarColor(float current, float max)
        {
            float amount = Mathf.Clamp01(current / Mathf.Max(1f, max));
            return Color.Lerp(emptyColor, normalColor, amount);
        }
    }
}
