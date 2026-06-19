using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Week14.Combat;

namespace Week14.UI
{
    public class BulletBarView : MonoBehaviour
    {
        public enum DisplayMode
        {
            Icons,
            Filled
        }

        private const string IconRootName = "BulletIconRoot";
        private const string OverflowTextName = "BulletOverflowText";
        private const string FillRootName = "BulletFillRoot";
        private const string FillBackgroundName = "Background";
        private const string FillGhostName = "Ghost";
        private const string FillForegroundName = "Fill";

        [SerializeField] private BulletGauge target;
        [SerializeField] private bool bindPlayerOnStart = true;
        [SerializeField, Min(1)] private int maxVisibleIcons = 10;
        [SerializeField, Range(0.1f, 1f)] private float iconHeightRatio = 0.9f;
        [SerializeField, Range(0.1f, 1f)] private float iconWidthRatio = 0.48f;
        [SerializeField, Min(0f)] private float iconSpacingRatio = 0.16f;
        [SerializeField, Min(1f)] private float overflowTextFontSize = 18f;
        [SerializeField] private List<Image> staticBulletImages = new();
        [SerializeField] private Color normalColor = new(1f, 0.55f, 0.1f);
        [SerializeField] private Color emptyColor = Color.red;
        [SerializeField] private Color parryOutlineColor = Color.white;
        [SerializeField] private Color hitOutlineColor = Color.yellow;
        [SerializeField] private Color attackOutlineColor = new(1f, 0.55f, 0.1f, 1f);
        [SerializeField] private DisplayMode displayMode = DisplayMode.Icons;
        [Tooltip("Filled 모드에서 배경 바 색상입니다.")]
        [SerializeField] private Color filledBackgroundColor = new(0.15f, 0.15f, 0.15f, 0.85f);
        [Tooltip("Filled 모드에서 체력이 줄어들 때 보이는 반투명 잔상 색상입니다.")]
        [SerializeField] private Color filledGhostColor = new(1f, 1f, 1f, 0.55f);
        [Tooltip("Filled 모드에서 반투명 잔상이 실제 체력만큼 줄어드는 속도입니다. 값이 클수록 빠르게 줄어듭니다.")]
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

        private static Sprite solidFillSprite;

        private readonly List<BulletIcon> icons = new();
        private readonly List<BulletIcon> retiringIcons = new();
        private readonly List<IconShard> shards = new();
        private RectTransform iconRoot;
        [SerializeField] private TextMeshProUGUI overflowText;
        private RectTransform overflowTextRect;
        private int displayedBulletCount = -1;
        private int displayedVisibleCount;
        private float iconWidth;
        private float iconHeight;
        private float iconSpacing;
        private float lastIconRootHeight;
        private Color currentIconColor;

        private static Sprite bulletIconSprite;
        private void OnEnable()
        {
            if (displayMode == DisplayMode.Filled)
            {
                EnsureFilledView();
                SetStaticBulletImagesVisible(false);
            }
            else
            {
                EnsureIconView();
            }

            TryBindPlayer();
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

            if (!bindPlayerOnStart || PlayerCombatController.Active == null)
            {
                return;
            }

            if (target == PlayerCombatController.Active.Bullets)
            {
                return;
            }

            TryBindPlayer();
            Subscribe();
            Refresh();
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

        public void Configure()
        {
            EnsureIconView();
            Refresh();
        }

        public void SetBindPlayerOnStart(bool value)
        {
            bindPlayerOnStart = value;
        }

        public void SetColors(Color normal, Color empty)
        {
            normalColor = normal;
            emptyColor = empty;
            Refresh();
        }

        public void SetDisplayMode(DisplayMode mode)
        {
            if (displayMode == mode)
            {
                return;
            }

            displayMode = mode;
            displayedBulletCount = -1;

            if (mode == DisplayMode.Filled)
            {
                EnsureFilledView();
                SetStaticBulletImagesVisible(false);
                if (iconRoot != null)
                {
                    iconRoot.gameObject.SetActive(false);
                }
            }
            else
            {
                EnsureIconView();
                if (fillRoot != null)
                {
                    fillRoot.gameObject.SetActive(false);
                }
            }

            Refresh();
        }

        public void SetExecutionWindow(bool active, float remainingRatio)
        {
            if (displayMode != DisplayMode.Filled)
            {
                return;
            }

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
                filledForegroundImage.color = ResolveIconColor(target != null ? target.CurrentBullets : 0, target != null ? target.MaxBullets : 1);
            }

            Refresh();
        }

        public void SetIconLayout(int visibleIconLimit, float heightRatio, float widthRatio, float spacingRatio)
        {
            maxVisibleIcons = Mathf.Max(1, visibleIconLimit);
            iconHeightRatio = Mathf.Clamp(heightRatio, 0.1f, 1f);
            iconWidthRatio = Mathf.Clamp(widthRatio, 0.1f, 1f);
            iconSpacingRatio = Mathf.Max(0f, spacingRatio);
            Refresh();
        }

        private void TryBindPlayer()
        {
            if (!bindPlayerOnStart || PlayerCombatController.Active == null)
            {
                return;
            }

            BulletGauge playerBullets = PlayerCombatController.Active.Bullets;
            if (target == playerBullets)
            {
                ApplyPlayerConfigColors();
                return;
            }

            Unsubscribe();
            target = playerBullets;
            ApplyPlayerConfigColors();
            displayedBulletCount = -1;
            displayedVisibleCount = 0;
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
            SetValue(current, max, true);
        }

        private void Refresh()
        {
            if (target == null)
            {
                SetValue(0, 1, false);
                return;
            }

            ApplyPlayerConfigColorsIfNeeded();
            SetValue(target.CurrentBullets, target.MaxBullets, false);
        }

        private void SetValue(int current, int max, bool animate)
        {
            if (displayMode == DisplayMode.Filled)
            {
                if (executionWindowActive)
                {
                    return;
                }

                SetFilledValue(current, max);
                return;
            }

            if (HasStaticBulletImages())
            {
                SetStaticIconValue(current, max);
                return;
            }

            EnsureIconRoot();
            if (iconRoot == null)
            {
                return;
            }

            int bulletCount = Mathf.Max(0, current);
            int visibleCount = Mathf.Min(bulletCount, Mathf.Max(1, maxVisibleIcons));
            int previousBulletCount = displayedBulletCount < 0 ? bulletCount : displayedBulletCount;
            int previousVisibleCount = displayedVisibleCount;
            bool hasPreviousValue = displayedBulletCount >= 0;
            bool changed = hasPreviousValue && bulletCount != previousBulletCount;
            bool gained = bulletCount > previousBulletCount;
            BulletChangeSource source = target != null ? target.LastChangeSource : BulletChangeSource.None;
            bool animateIcons = animate && changed;

            currentIconColor = ResolveIconColor(current, max);
            ResizeVisibleIcons(visibleCount, animateIcons, gained, source);
            LayoutIcons(bulletCount);
            if (!animate || !changed || !hasPreviousValue)
            {
                SnapActiveIcons();
            }

            UpdateIconColors();
            UpdateOverflowText(bulletCount - visibleCount);

            if (animate && changed && visibleCount == previousVisibleCount)
            {
                PlayOverflowChangeFeedback(gained, source);
            }

            displayedBulletCount = bulletCount;
            displayedVisibleCount = visibleCount;
        }

        private void SetFilledValue(int current, int max)
        {
            EnsureFilledView();
            if (filledForegroundImage == null)
            {
                return;
            }

            bool hasPreviousValue = displayedBulletCount >= 0;
            int bulletCount = Mathf.Max(0, current);
            float ratio = Mathf.Clamp01(bulletCount / (float)Mathf.Max(1, max));

            filledRatio = ratio;
            filledForegroundImage.color = ResolveIconColor(current, max);
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
            RectTransform parent = ResolveIconParent();
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

        private void EnsureIconRoot()
        {
            RectTransform parent = ResolveIconParent();
            if (parent == null)
            {
                return;
            }

            if (iconRoot == null || iconRoot.parent != parent)
            {
                Transform existing = parent.Find(IconRootName);
                iconRoot = existing != null ? existing.GetComponent<RectTransform>() : null;
                if (iconRoot == null)
                {
                    GameObject rootObject = new(IconRootName, typeof(RectTransform));
                    rootObject.transform.SetParent(parent, false);
                    iconRoot = rootObject.GetComponent<RectTransform>();
                }

                iconRoot.anchorMin = Vector2.zero;
                iconRoot.anchorMax = Vector2.one;
                iconRoot.offsetMin = Vector2.zero;
                iconRoot.offsetMax = Vector2.zero;
            }

            iconRoot.gameObject.SetActive(true);
            EnsureOverflowText();
        }

        private void EnsureIconView()
        {
            if (HasStaticBulletImages())
            {
                return;
            }

            EnsureIconRoot();
        }

        private RectTransform ResolveIconParent()
        {
            return transform as RectTransform;
        }

        private void EnsureOverflowText()
        {
            if (iconRoot == null)
            {
                return;
            }

            if (overflowText == null)
            {
                Transform existing = iconRoot.Find(OverflowTextName);
                overflowText = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;
            }

            if (overflowText == null)
            {
                GameObject textObject = new(OverflowTextName, typeof(RectTransform));
                textObject.transform.SetParent(iconRoot, false);
                overflowText = textObject.AddComponent<TextMeshProUGUI>();
                overflowText.raycastTarget = false;
            }
            else if (overflowText.transform.parent != iconRoot)
            {
                overflowText.transform.SetParent(iconRoot, false);
            }

            overflowText.alignment = TextAlignmentOptions.MidlineLeft;
            overflowText.color = normalColor;
            overflowText.enableAutoSizing = false;
            overflowText.textWrappingMode = TextWrappingModes.NoWrap;
            overflowText.overflowMode = TextOverflowModes.Overflow;
            overflowText.fontSize = GetEffectiveOverflowFontSize(iconRoot.rect.height);
            overflowTextRect = overflowText.rectTransform;
            ConfigureOverflowTextTransform(0f, 0f);
        }

        private float GetIconRootHeight()
        {
            return TryGetIconRootHeight(out float rootHeight)
                ? rootHeight
                : Mathf.Max(0.01f, lastIconRootHeight);
        }

        private bool TryGetIconRootHeight(out float rootHeight)
        {
            Canvas.ForceUpdateCanvases();

            float iconRootHeight = iconRoot != null ? Mathf.Abs(iconRoot.rect.height) : 0f;
            if (iconRootHeight > 0.01f)
            {
                lastIconRootHeight = iconRootHeight;
                rootHeight = iconRootHeight;
                return true;
            }

            RectTransform parent = ResolveIconParent();
            float parentHeight = parent != null ? Mathf.Abs(parent.rect.height) : 0f;
            if (parentHeight > 0.01f)
            {
                lastIconRootHeight = parentHeight;
                rootHeight = parentHeight;
                return true;
            }

            float sizeDeltaHeight = parent != null ? Mathf.Abs(parent.sizeDelta.y) : 0f;
            if (sizeDeltaHeight > 0.01f)
            {
                lastIconRootHeight = sizeDeltaHeight;
                rootHeight = sizeDeltaHeight;
                return true;
            }

            rootHeight = 0f;
            return false;
        }

        private void ResizeVisibleIcons(int visibleCount, bool animate, bool gained, BulletChangeSource source)
        {
            while (icons.Count < visibleCount)
            {
                BulletIcon icon = CreateIcon();
                icons.Add(icon);
                if (animate)
                {
                    icon.PlaySpawn(ResolveEffectColor(source), Random.insideUnitCircle * Mathf.Max(iconHeight, 0.01f) * 0.45f);
                    SpawnShards(icon.Rect.anchoredPosition, ResolveEffectColor(source), true);
                }
            }

            while (icons.Count > visibleCount)
            {
                BulletIcon icon = icons[icons.Count - 1];
                icons.RemoveAt(icons.Count - 1);
                if (animate)
                {
                    icon.PlayTear(ResolveEffectColor(source));
                    retiringIcons.Add(icon);
                    SpawnShards(icon.Rect.anchoredPosition, ResolveEffectColor(source), false);
                }
                else
                {
                    Destroy(icon.Rect.gameObject);
                }
            }

            if (animate && icons.Count > 0 && gained)
            {
                icons[icons.Count - 1].PlayPulse(ResolveEffectColor(source));
            }
        }

        private BulletIcon CreateIcon()
        {
            GameObject iconObject = new("BulletIcon", typeof(RectTransform));
            iconObject.transform.SetParent(iconRoot, false);
            Image image = iconObject.AddComponent<Image>();
            image.sprite = GetBulletIconSprite();
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.color = currentIconColor;
            return new BulletIcon(iconObject.GetComponent<RectTransform>(), image);
        }

        private void LayoutIcons(int bulletCount)
        {
            if (iconRoot == null)
            {
                return;
            }

            Rect rect = iconRoot.rect;
            float rootWidth = Mathf.Abs(rect.width);
            float rootHeight = GetIconRootHeight();
            iconHeight = rootHeight * iconHeightRatio;
            iconWidth = rootHeight * iconWidthRatio;
            iconSpacing = rootHeight * iconSpacingRatio;

            int overflowCount = Mathf.Max(0, bulletCount - icons.Count);
            float overflowWidth = overflowCount > 0 ? GetOverflowWidth(overflowCount, rootHeight) : 0f;
            float iconAreaWidth = icons.Count > 0
                ? icons.Count * iconWidth + Mathf.Max(0, icons.Count - 1) * iconSpacing
                : 0f;
            float overflowGap = overflowCount > 0 && icons.Count > 0 ? iconSpacing * 1.4f : 0f;
            float totalWidth = iconAreaWidth + overflowGap + overflowWidth;

            if (rootWidth > 0.01f && totalWidth > rootWidth)
            {
                float scale = rootWidth / totalWidth;
                iconHeight *= scale;
                iconWidth *= scale;
                iconSpacing *= scale;
                overflowWidth *= scale;
                overflowGap *= scale;
                iconAreaWidth = icons.Count > 0
                    ? icons.Count * iconWidth + Mathf.Max(0, icons.Count - 1) * iconSpacing
                    : 0f;
                totalWidth = iconAreaWidth + overflowGap + overflowWidth;
            }

            float startX = -totalWidth * 0.5f + iconWidth * 0.5f;
            for (int i = 0; i < icons.Count; i++)
            {
                BulletIcon icon = icons[i];
                icon.SetSize(iconWidth, iconHeight);
                icon.BaseRotationDegrees = 0f;
                icon.TargetPosition = new Vector2(startX + i * (iconWidth + iconSpacing), 0f);
            }

            if (overflowTextRect != null)
            {
                ConfigureOverflowTextTransform(overflowWidth, rootHeight);
                overflowTextRect.anchoredPosition = new Vector2(-totalWidth * 0.5f + iconAreaWidth + overflowGap, 0f);
                overflowText.fontSize = GetEffectiveOverflowFontSize(rootHeight);
            }
        }

        private void ConfigureOverflowTextTransform(float width, float height)
        {
            if (overflowTextRect == null)
            {
                return;
            }

            overflowTextRect.anchorMin = new Vector2(0.5f, 0.5f);
            overflowTextRect.anchorMax = new Vector2(0.5f, 0.5f);
            overflowTextRect.pivot = new Vector2(0f, 0.5f);
            overflowTextRect.sizeDelta = new Vector2(Mathf.Max(0f, width), Mathf.Max(0f, height));
            overflowTextRect.localScale = Vector3.one;
            overflowTextRect.localRotation = Quaternion.identity;
            Vector3 localPosition = overflowTextRect.localPosition;
            localPosition.z = 0f;
            overflowTextRect.localPosition = localPosition;
        }

        private float GetOverflowWidth(int overflowCount, float rootHeight)
        {
            int digitCount = overflowCount > 0 ? overflowCount.ToString().Length : 1;
            float fontSize = GetEffectiveOverflowFontSize(rootHeight);
            return Mathf.Max(rootHeight, fontSize * (0.85f + digitCount * 0.55f));
        }

        private float GetEffectiveOverflowFontSize(float rootHeight)
        {
            float safeHeight = Mathf.Max(0.01f, Mathf.Abs(rootHeight));
            return Mathf.Min(overflowTextFontSize, safeHeight * 0.9f);
        }

        private void UpdateIconColors()
        {
            for (int i = 0; i < icons.Count; i++)
            {
                icons[i].BaseColor = currentIconColor;
            }

            for (int i = 0; i < retiringIcons.Count; i++)
            {
                retiringIcons[i].BaseColor = currentIconColor;
            }
        }

        private void SnapActiveIcons()
        {
            for (int i = 0; i < icons.Count; i++)
            {
                icons[i].SnapToTarget();
            }
        }

        private void UpdateOverflowText(int overflowCount)
        {
            if (overflowText == null)
            {
                return;
            }

            bool visible = overflowCount > 0;
            overflowText.gameObject.SetActive(visible);
            if (!visible)
            {
                overflowText.text = string.Empty;
                return;
            }

            overflowText.text = $"+{overflowCount:0}";
            overflowText.color = currentIconColor;
        }

        private void SetStaticIconValue(int current, int max)
        {
            int bulletCount = Mathf.Max(0, current);
            int visibleCount = Mathf.Min(bulletCount, GetStaticBulletImageCount());
            currentIconColor = ResolveIconColor(current, max);
            int slotIndex = 0;

            for (int i = 0; i < staticBulletImages.Count; i++)
            {
                Image image = staticBulletImages[i];
                if (image == null)
                {
                    continue;
                }

                bool visible = slotIndex < visibleCount;
                image.gameObject.SetActive(visible);
                if (visible)
                {
                    image.raycastTarget = false;
                    image.color = currentIconColor;
                }

                slotIndex++;
            }

            UpdateOverflowText(bulletCount - visibleCount);
            displayedBulletCount = bulletCount;
            displayedVisibleCount = visibleCount;
        }

        private void SetStaticBulletImagesVisible(bool visible)
        {
            if (staticBulletImages == null)
            {
                return;
            }

            for (int i = 0; i < staticBulletImages.Count; i++)
            {
                if (staticBulletImages[i] != null)
                {
                    staticBulletImages[i].gameObject.SetActive(visible);
                }
            }

            if (!visible)
            {
                UpdateOverflowText(0);
            }
        }

        private bool HasStaticBulletImages()
        {
            return GetStaticBulletImageCount() > 0;
        }

        private int GetStaticBulletImageCount()
        {
            if (staticBulletImages == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < staticBulletImages.Count; i++)
            {
                if (staticBulletImages[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private void PlayOverflowChangeFeedback(bool gained, BulletChangeSource source)
        {
            Color effectColor = ResolveEffectColor(source);
            if (icons.Count > 0)
            {
                BulletIcon icon = icons[icons.Count - 1];
                if (gained)
                {
                    icon.PlayPulse(effectColor);
                    SpawnShards(icon.Rect.anchoredPosition, effectColor, true);
                }
                else
                {
                    icon.PlayHit(effectColor);
                    SpawnShards(icon.Rect.anchoredPosition, effectColor, false);
                }
            }

            if (overflowText != null && overflowText.gameObject.activeSelf)
            {
                overflowText.color = effectColor;
            }
        }

        private void SpawnShards(Vector2 origin, Color color, bool inward)
        {
            if (iconRoot == null)
            {
                return;
            }

            int count = inward ? 5 : 7;
            for (int i = 0; i < count; i++)
            {
                GameObject shardObject = new("BulletIconShard", typeof(RectTransform));
                shardObject.transform.SetParent(iconRoot, false);
                Image image = shardObject.AddComponent<Image>();
                image.sprite = GetBulletIconSprite();
                image.raycastTarget = false;
                image.color = color;

                RectTransform rect = shardObject.GetComponent<RectTransform>();
                float size = Mathf.Max(0.01f, iconHeight * Random.Range(0.16f, 0.32f));
                rect.sizeDelta = new Vector2(size * 0.65f, size);
                rect.anchoredPosition = inward
                    ? origin + Random.insideUnitCircle * iconHeight * 0.55f
                    : origin;

                Vector2 direction = Random.insideUnitCircle.normalized;
                if (direction.sqrMagnitude <= 0.001f)
                {
                    direction = Vector2.up;
                }

                float speed = iconHeight * Random.Range(inward ? 1.2f : 1.8f, inward ? 2.2f : 3.4f);
                Vector2 velocity = direction * speed * (inward ? -1f : 1f);
                shards.Add(new IconShard(rect, image, velocity, Random.Range(-420f, 420f), Random.Range(0.22f, 0.42f)));
            }
        }

        private void TickEffects()
        {
            float deltaTime = Time.unscaledDeltaTime;

            if (displayMode == DisplayMode.Filled)
            {
                if (executionWindowActive)
                {
                    TickExecutionWindowBlink();
                }
                else if (filledGhostImage != null && filledGhostRatio > filledRatio)
                {
                    filledGhostRatio = Mathf.Max(filledRatio, Mathf.Lerp(filledGhostRatio, filledRatio, 1f - Mathf.Exp(-deltaTime * filledGhostShrinkSpeed)));
                    filledGhostImage.fillAmount = filledGhostRatio;
                }

                return;
            }

            TickIconEffects(deltaTime);
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

        private void TickIconEffects(float deltaTime)
        {
            for (int i = 0; i < icons.Count; i++)
            {
                icons[i].Tick(deltaTime);
            }

            for (int i = retiringIcons.Count - 1; i >= 0; i--)
            {
                if (retiringIcons[i].Tick(deltaTime))
                {
                    Destroy(retiringIcons[i].Rect.gameObject);
                    retiringIcons.RemoveAt(i);
                }
            }

            for (int i = shards.Count - 1; i >= 0; i--)
            {
                if (shards[i].Tick(deltaTime))
                {
                    Destroy(shards[i].Rect.gameObject);
                    shards.RemoveAt(i);
                }
            }

            if (overflowText != null && overflowText.gameObject.activeSelf)
            {
                overflowText.color = Color.Lerp(overflowText.color, currentIconColor, 1f - Mathf.Exp(-deltaTime * 16f));
            }
        }

        private Color ResolveIconColor(float current, float max)
        {
            float amount = Mathf.Clamp01(current / Mathf.Max(1f, max));
            return Color.Lerp(emptyColor, normalColor, amount);
        }

        private Color ResolveEffectColor(BulletChangeSource source)
        {
            return source switch
            {
                BulletChangeSource.Parry => parryOutlineColor,
                BulletChangeSource.Hit => hitOutlineColor,
                BulletChangeSource.Attack => attackOutlineColor,
                BulletChangeSource.Execution => parryOutlineColor,
                _ => currentIconColor
            };
        }

        private void ApplyPlayerConfigColors()
        {
            PlayerCombatConfig config = PlayerCombatController.Active != null ? PlayerCombatController.Active.Config : null;
            if (config == null)
            {
                return;
            }

            parryOutlineColor = config.BulletParryOutlineColor;
            hitOutlineColor = config.BulletHitOutlineColor;
            maxVisibleIcons = Mathf.Max(1, config.PlayerBulletUiMaxVisibleIcons);
            iconHeightRatio = Mathf.Clamp(config.PlayerBulletUiIconHeightRatio, 0.1f, 1f);
            iconWidthRatio = Mathf.Clamp(config.PlayerBulletUiIconWidthRatio, 0.1f, 1f);
            iconSpacingRatio = Mathf.Max(0f, config.PlayerBulletUiIconSpacingRatio);
            overflowTextFontSize = Mathf.Max(1f, config.PlayerBulletUiOverflowTextFontSize);
        }

        private void ApplyPlayerConfigColorsIfNeeded()
        {
            if (PlayerCombatController.Active == null || target != PlayerCombatController.Active.Bullets)
            {
                return;
            }

            ApplyPlayerConfigColors();
        }

        private static Sprite GetBulletIconSprite()
        {
            if (bulletIconSprite != null)
            {
                return bulletIconSprite;
            }

            const int width = 24;
            const int height = 32;
            Texture2D texture = new(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            for (int y = 0; y < height; y++)
            {
                float v = (float)y / (height - 1);
                for (int x = 0; x < width; x++)
                {
                    float u = ((float)x / (width - 1) - 0.5f) * 2f;
                    float halfWidth = v > 0.62f ? Mathf.Lerp(0.48f, 0.08f, (v - 0.62f) / 0.38f) : 0.48f;
                    bool body = Mathf.Abs(u) <= halfWidth && v >= 0.06f && v <= 0.94f;
                    bool baseCut = v < 0.14f && Mathf.Abs(u) > 0.36f;
                    float edge = Mathf.InverseLerp(halfWidth + 0.1f, halfWidth - 0.02f, Mathf.Abs(u));
                    float alpha = body && !baseCut ? Mathf.Clamp01(edge) : 0f;
                    float highlight = u < -0.12f && v > 0.2f && v < 0.76f ? 0.32f : 0f;
                    Color color = new(1f, 1f, 1f, alpha * (0.68f + highlight));
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            bulletIconSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), height);
            return bulletIconSprite;
        }

        private sealed class BulletIcon
        {
            private const float SpawnSeconds = 0.24f;
            private const float PulseSeconds = 0.22f;
            private const float TearSeconds = 0.34f;

            private float timer;
            private float duration;
            private float spinDegreesPerSecond;
            private Vector2 velocity;
            private Vector2 spawnOffset;
            private Color effectColor;
            private IconAnimationKind animationKind;

            public BulletIcon(RectTransform rect, Image image)
            {
                Rect = rect;
                Image = image;
                BaseColor = image.color;
            }

            public RectTransform Rect { get; }
            public Image Image { get; }
            public Vector2 TargetPosition { get; set; }
            public Color BaseColor { get; set; }
            public float BaseRotationDegrees { get; set; }

            private Quaternion BaseRotation => Quaternion.Euler(0f, 0f, BaseRotationDegrees);

            public void SetSize(float width, float height)
            {
                Rect.sizeDelta = new Vector2(width, height);
            }

            public void SnapToTarget()
            {
                Rect.anchoredPosition = TargetPosition;
                Rect.localScale = Vector3.one;
                Rect.localRotation = BaseRotation;
                Image.color = BaseColor;
            }

            public void SnapPoseToTarget()
            {
                Rect.anchoredPosition = TargetPosition;
                Rect.localRotation = BaseRotation;
            }

            public void PlaySpawn(Color color, Vector2 offset)
            {
                timer = SpawnSeconds;
                duration = SpawnSeconds;
                effectColor = color;
                spawnOffset = offset;
                animationKind = IconAnimationKind.Spawn;
                Rect.localScale = Vector3.zero;
                Rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-42f, 42f));
                Rect.anchoredPosition = TargetPosition + spawnOffset;
                Image.color = effectColor;
            }

            public void PlayPulse(Color color)
            {
                timer = PulseSeconds;
                duration = PulseSeconds;
                effectColor = color;
                animationKind = IconAnimationKind.Pulse;
            }

            public void PlayHit(Color color)
            {
                timer = PulseSeconds;
                duration = PulseSeconds;
                effectColor = color;
                animationKind = IconAnimationKind.Hit;
            }

            public void PlayTear(Color color)
            {
                timer = TearSeconds;
                duration = TearSeconds;
                effectColor = color;
                animationKind = IconAnimationKind.Tear;
                velocity = Random.insideUnitCircle.normalized * Mathf.Max(Rect.rect.height, 0.01f) * Random.Range(1.3f, 2.4f);
                spinDegreesPerSecond = Random.Range(-620f, 620f);
            }

            public bool Tick(float deltaTime)
            {
                if (animationKind == IconAnimationKind.None)
                {
                    MoveToTarget(deltaTime);
                    Rect.localScale = Vector3.one;
                    Rect.localRotation = BaseRotation;
                    Image.color = BaseColor;
                    return false;
                }

                timer -= deltaTime;
                float t = 1f - Mathf.Clamp01(timer / Mathf.Max(0.001f, duration));

                switch (animationKind)
                {
                    case IconAnimationKind.Spawn:
                        MoveToTarget(deltaTime * 2f);
                        Rect.localScale = Vector3.one * EaseOutBack(t);
                        Rect.localRotation = Quaternion.Lerp(Rect.localRotation, BaseRotation, t);
                        Image.color = Color.Lerp(effectColor, BaseColor, t);
                        break;
                    case IconAnimationKind.Pulse:
                        MoveToTarget(deltaTime);
                        Rect.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.38f);
                        Rect.localRotation = BaseRotation;
                        Image.color = Color.Lerp(effectColor, BaseColor, t);
                        break;
                    case IconAnimationKind.Hit:
                        MoveToTarget(deltaTime);
                        float shake = Mathf.Sin(t * Mathf.PI * 8f) * (1f - t) * 6f;
                        Rect.localScale = Vector3.one * (1f - Mathf.Sin(t * Mathf.PI) * 0.18f);
                        Rect.localRotation = Quaternion.Euler(0f, 0f, BaseRotationDegrees + shake);
                        Image.color = Color.Lerp(effectColor, BaseColor, t);
                        break;
                    case IconAnimationKind.Tear:
                        Rect.anchoredPosition += velocity * deltaTime;
                        Rect.Rotate(0f, 0f, spinDegreesPerSecond * deltaTime);
                        Rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.05f, t);
                        Color color = Color.Lerp(effectColor, BaseColor, t);
                        color.a *= 1f - t;
                        Image.color = color;
                        break;
                }

                if (timer > 0f)
                {
                    return false;
                }

                bool wasTear = animationKind == IconAnimationKind.Tear;
                animationKind = IconAnimationKind.None;
                Rect.localScale = Vector3.one;
                Rect.localRotation = BaseRotation;
                Image.color = BaseColor;
                return wasTear;
            }

            private void MoveToTarget(float deltaTime)
            {
                float lerp = 1f - Mathf.Exp(-deltaTime * 18f);
                Rect.anchoredPosition = Vector2.Lerp(Rect.anchoredPosition, TargetPosition, lerp);
            }

            private static float EaseOutBack(float t)
            {
                const float c1 = 1.70158f;
                const float c3 = c1 + 1f;
                float p = t - 1f;
                return 1f + c3 * p * p * p + c1 * p * p;
            }
        }

        private sealed class IconShard
        {
            private readonly float duration;
            private readonly float spinDegreesPerSecond;
            private readonly Vector2 velocity;
            private float timer;
            private Color baseColor;

            public IconShard(RectTransform rect, Image image, Vector2 velocity, float spinDegreesPerSecond, float duration)
            {
                Rect = rect;
                Image = image;
                this.velocity = velocity;
                this.spinDegreesPerSecond = spinDegreesPerSecond;
                this.duration = duration;
                timer = duration;
                baseColor = image.color;
            }

            public RectTransform Rect { get; }
            public Image Image { get; }

            public bool Tick(float deltaTime)
            {
                timer -= deltaTime;
                float t = 1f - Mathf.Clamp01(timer / Mathf.Max(0.001f, duration));
                Rect.anchoredPosition += velocity * deltaTime;
                Rect.Rotate(0f, 0f, spinDegreesPerSecond * deltaTime);
                Rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.2f, t);
                Color color = baseColor;
                color.a *= 1f - t;
                Image.color = color;
                return timer <= 0f;
            }
        }

        private enum IconAnimationKind
        {
            None,
            Spawn,
            Pulse,
            Hit,
            Tear
        }
    }
}
