using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Week14.Combat;

namespace Week14.UI
{
    public sealed class PlayerHP : MonoBehaviour
    {
        private const int VisibleSlotCount = 5;
        private const float UntimedBulletLoadedAt = -1f;

        private enum EffectKind
        {
            None,
            AttackSpend,
            Recovery,
            ShiftDown,
            ExpireShift
        }

        [Serializable]
        private sealed class HpSlot
        {
            [SerializeField] private Image bodyImage;

            private static Sprite shineSprite;

            private Image timeoutFillImage;
            private Image ejectedTimeoutFillImage;
            private Image shineImage;
            private Image ejectedBodyImage;
            private EffectKind effectKind;
            private float timer;
            private float duration;
            private float effectDelay;
            private bool targetUsable;
            private bool targetRecovered;
            private Color bodyStartColor;
            private Color bodyTargetColor;
            private Color baseBodyColor = Color.white;
            private bool hasBaseBodyColor;
            private TransformState bodyTransform;
            private TransformState shineTransform;
            private TransformState ejectedBodyTransform;
            private Vector2 shiftBodyStartOffset;
            private float lastTimeoutFillAmount;
            private Color lastTimeoutFillColor;

            public HpSlot()
            {
            }

            public void ApplyInstant(bool usable, bool recovered)
            {
                FinishEffect();
                targetUsable = usable;
                targetRecovered = recovered;
                ApplyFinal();
            }

            public void PlayHitSpend(float effectSeconds)
            {
                PlayEjectSpend(effectSeconds);
            }

            public void PlayAttackSpend(float effectSeconds)
            {
                PlayEjectSpend(effectSeconds);
            }

            private void PlayEjectSpend(float effectSeconds)
            {
                EnsureEjectedImages();
                BeginEffect(EffectKind.AttackSpend, true, false, effectSeconds);
                PrepareEjectedImages();
            }

            public void PlayRecovery(float effectSeconds)
            {
                EnsureShineImage();
                BeginEffect(EffectKind.Recovery, true, true, effectSeconds);
                SetTransientImageVisible(shineImage, true);
            }

            public void PlayShiftDown(float effectSeconds, HpSlot sourceSlot)
            {
                BeginShiftEffect(EffectKind.ShiftDown, effectSeconds, sourceSlot);
            }

            public void PlayExpiredShift(float effectSeconds, HpSlot sourceSlot)
            {
                EnsureEjectedImages();
                BeginShiftEffect(EffectKind.ExpireShift, effectSeconds, sourceSlot);
                PrepareEjectedImages();
            }

            public void Tick(
                float deltaTime,
                float shineAlpha,
                float attackEjectDistance,
                float attackEjectRise,
                float attackEjectSpinDegrees)
            {
                if (effectKind == EffectKind.None)
                {
                    return;
                }

                timer += deltaTime;
                float t = Mathf.Clamp01(timer / duration);

                switch (effectKind)
                {
                    case EffectKind.AttackSpend:
                        TickAttackSpend(t, attackEjectDistance, attackEjectRise, attackEjectSpinDegrees);
                        break;
                    case EffectKind.Recovery:
                        TickRecovery(t, shineAlpha);
                        break;
                    case EffectKind.ShiftDown:
                        TickShiftDown(t);
                        break;
                    case EffectKind.ExpireShift:
                        TickExpiredShift(t, attackEjectDistance, attackEjectRise, attackEjectSpinDegrees);
                        break;
                }

                if (timer >= duration)
                {
                    FinishEffect();
                }
            }

            private void BeginEffect(
                EffectKind nextEffectKind,
                bool usable,
                bool recovered,
                float effectSeconds,
                float delaySeconds = 0f)
            {
                FinishEffect();
                targetUsable = usable;
                targetRecovered = recovered;
                effectKind = nextEffectKind;
                timer = 0f;
                effectDelay = Mathf.Max(0f, delaySeconds);
                duration = effectDelay + Mathf.Max(0.01f, effectSeconds);

                CaptureTransforms();
                bodyStartColor = GetColor(bodyImage, GetBodyColor(usable, recovered));
                bodyTargetColor = GetBodyColor(usable, recovered);
                shiftBodyStartOffset = Vector2.zero;
            }

            private void BeginShiftEffect(EffectKind nextEffectKind, float effectSeconds, HpSlot sourceSlot)
            {
                BeginEffect(nextEffectKind, true, true, effectSeconds);
                shiftBodyStartOffset = GetOffsetFrom(sourceSlot?.bodyImage, bodyImage);
            }

            private void TickAttackSpend(float t, float distance, float rise, float spinDegrees)
            {
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                float x = Mathf.Round(distance * eased);
                float y = Mathf.Round(Mathf.Sin(t * Mathf.PI) * rise - rise * 0.35f * t);
                float rotation = spinDegrees * eased;
                float fade = Mathf.Lerp(0.3f, 1f, Mathf.SmoothStep(0f, 1f, t));
                Vector2 offset = new(x, y);

                ApplyOffset(ejectedBodyImage, ejectedBodyTransform, offset, rotation, 1f);
                ApplyColor(ejectedBodyImage, WithAlpha(bodyStartColor, bodyStartColor.a * (1f - fade)));

                float colorT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.28f, t));
                ApplyColor(bodyImage, Color.Lerp(bodyStartColor, bodyTargetColor, colorT));
            }

            private void TickRecovery(float t, float shineAlpha)
            {
                float eased = Mathf.SmoothStep(0f, 1f, t);
                float pulse = Mathf.Sin(t * Mathf.PI);
                float scale = 1f + pulse * 0.08f;

                ApplyOffset(bodyImage, bodyTransform, Vector2.zero, 0f, scale);
                ApplyColor(bodyImage, Color.Lerp(bodyStartColor, bodyTargetColor, eased));

                if (shineImage == null)
                {
                    return;
                }

                RectTransform shineRect = shineImage.rectTransform;
                float move = shineRect.rect.width * Mathf.Lerp(-0.35f, 0.35f, eased);
                move = Mathf.Round(move);
                ApplyOffset(shineImage, shineTransform, new Vector2(move, 0f), 0f, 1f);
                ApplyColor(shineImage, new Color(1f, 1f, 1f, pulse * shineAlpha));
            }

            private void TickShiftDown(float t)
            {
                float eased = Mathf.SmoothStep(0f, 1f, t);
                ApplyShift(eased);
            }

            private void TickExpiredShift(float t, float distance, float rise, float spinDegrees)
            {
                TickAttackSpend(t, distance, rise, spinDegrees);
                ApplyShift(Mathf.SmoothStep(0f, 1f, t));
            }

            private void ApplyShift(float t)
            {
                Vector2 bodyOffset = Vector2.Lerp(shiftBodyStartOffset, Vector2.zero, t);
                ApplyOffset(bodyImage, bodyTransform, bodyOffset, 0f, 1f);
                ApplyColor(bodyImage, bodyTargetColor);
            }

            private void FinishEffect()
            {
                if (effectKind == EffectKind.None)
                {
                    return;
                }

                RestoreTransforms();
                effectKind = EffectKind.None;
                effectDelay = 0f;
                SetTransientImageVisible(shineImage, false);
                SetTransientImageVisible(ejectedBodyImage, false);
                SetTransientImageVisible(ejectedTimeoutFillImage, false);
                ApplyFinal();
            }

            private void ApplyFinal()
            {
                SetTransientImageVisible(shineImage, false);
                SetTransientImageVisible(ejectedBodyImage, false);
                SetTransientImageVisible(ejectedTimeoutFillImage, false);

                ApplyColor(bodyImage, GetBodyColor(targetUsable, targetRecovered));
            }

            private static void ApplyColor(Image image, Color color)
            {
                if (image == null)
                {
                    return;
                }

                image.raycastTarget = false;
                image.color = color;
            }

            private static void ApplyOffset(Image image, TransformState state, Vector2 offset, float rotationDegrees, float scale)
            {
                if (image == null || !state.IsValid)
                {
                    return;
                }

                RectTransform rect = image.rectTransform;
                rect.anchoredPosition = state.AnchoredPosition + offset;
                rect.localRotation = state.LocalRotation * Quaternion.Euler(0f, 0f, rotationDegrees);
                rect.localScale = state.LocalScale * scale;
            }

            private static Vector2 GetOffsetFrom(Image source, Image target)
            {
                if (source == null || target == null)
                {
                    return Vector2.zero;
                }

                RectTransform targetRect = target.rectTransform;
                Vector3 worldDelta = source.rectTransform.position - targetRect.position;
                if (targetRect.parent is RectTransform parent)
                {
                    return parent.InverseTransformVector(worldDelta);
                }

                return worldDelta;
            }

            public void SetTimeoutVisual(bool usable, bool recovered, float fillAmount, Color fillColor, float iconAlpha)
            {
                lastTimeoutFillAmount = Mathf.Clamp01(fillAmount);
                lastTimeoutFillColor = fillColor;

                bool showFill = usable && recovered && lastTimeoutFillAmount > 0f;
                if (showFill)
                {
                    EnsureTimeoutFillImage();
                }

                ApplyTimeoutFill(timeoutFillImage, bodyImage, showFill, lastTimeoutFillAmount, fillColor);
                float alpha = usable && recovered ? Mathf.Clamp01(iconAlpha) : 0f;
                SetImageAlpha(bodyImage, alpha);
                SetImageAlpha(timeoutFillImage, showFill ? alpha : 0f);
            }

            private void EnsureTimeoutFillImage()
            {
                timeoutFillImage = EnsureTimeoutFillChild(timeoutFillImage, bodyImage, "TimeoutFill");
            }

            private static Image EnsureTimeoutFillChild(Image current, Image parentImage, string objectName)
            {
                if (current != null || parentImage == null)
                {
                    return current;
                }

                GameObject fillObject = new(objectName, typeof(RectTransform));
                fillObject.transform.SetParent(parentImage.transform, false);
                fillObject.transform.SetAsLastSibling();

                RectTransform rect = fillObject.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;

                Image fill = fillObject.AddComponent<Image>();
                fill.raycastTarget = false;
                fill.preserveAspect = parentImage.preserveAspect;
                fill.sprite = parentImage.sprite;
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Left;
                fill.fillAmount = 0f;
                fill.color = new Color(1f, 1f, 1f, 0f);
                return fill;
            }

            private static void ApplyTimeoutFill(Image fill, Image source, bool visible, float fillAmount, Color fillColor)
            {
                if (fill == null)
                {
                    return;
                }

                if (source != null)
                {
                    fill.sprite = source.sprite;
                    fill.preserveAspect = source.preserveAspect;
                }

                fill.raycastTarget = false;
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Left;
                fill.fillAmount = visible ? Mathf.Clamp01(fillAmount) : 0f;
                fill.color = WithAlpha(fillColor, visible ? fillColor.a : 0f);
                fill.gameObject.SetActive(visible);
            }

            private void EnsureShineImage()
            {
                if (shineImage != null)
                {
                    return;
                }

                RectTransform parent = bodyImage != null
                    ? bodyImage.rectTransform
                    : null;
                if (parent == null)
                {
                    return;
                }

                GameObject shineObject = new("RecoveryShine", typeof(RectTransform));
                shineObject.transform.SetParent(parent, false);
                shineObject.transform.SetAsLastSibling();

                RectTransform rect = shineObject.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;

                shineImage = shineObject.AddComponent<Image>();
                shineImage.sprite = GetShineSprite();
                shineImage.raycastTarget = false;
                shineImage.preserveAspect = false;
                shineImage.color = new Color(1f, 1f, 1f, 0f);
                shineImage.gameObject.SetActive(false);
            }

            private void EnsureEjectedImages()
            {
                EnsureTimeoutFillImage();
                ejectedBodyImage = EnsureEjectedImage(ejectedBodyImage, bodyImage, "EjectedBody");
                ejectedTimeoutFillImage = EnsureTimeoutFillChild(ejectedTimeoutFillImage, ejectedBodyImage, "EjectedTimeoutFill");
            }

            private static Image EnsureEjectedImage(Image current, Image source, string objectName)
            {
                if (current != null || source == null)
                {
                    return current;
                }

                RectTransform sourceRect = source.rectTransform;
                Transform parent = sourceRect.parent;
                if (parent == null)
                {
                    return null;
                }

                GameObject cloneObject = new(objectName, typeof(RectTransform));
                cloneObject.transform.SetParent(parent, false);
                cloneObject.transform.SetAsLastSibling();

                Image clone = cloneObject.AddComponent<Image>();
                clone.raycastTarget = false;
                clone.preserveAspect = source.preserveAspect;
                clone.type = source.type;
                clone.sprite = source.sprite;
                clone.material = source.material;
                CopyRect(sourceRect, clone.rectTransform);
                clone.gameObject.SetActive(false);
                return clone;
            }

            private void PrepareEjectedImages()
            {
                PrepareEjectedImage(ejectedBodyImage, bodyImage);
                PrepareEjectedTimeoutFill();
                ejectedBodyTransform = TransformState.Capture(ejectedBodyImage);
            }

            private static void PrepareEjectedImage(Image clone, Image source)
            {
                if (clone == null || source == null)
                {
                    return;
                }

                CopyRect(source.rectTransform, clone.rectTransform);
                clone.sprite = source.sprite;
                clone.color = WithAlpha(source.color, source.color.a * 0.7f);
                clone.preserveAspect = source.preserveAspect;
                clone.type = source.type;
                clone.material = source.material;
                clone.gameObject.SetActive(true);
            }

            private void PrepareEjectedTimeoutFill()
            {
                if (ejectedTimeoutFillImage == null)
                {
                    return;
                }

                ApplyTimeoutFill(ejectedTimeoutFillImage, ejectedBodyImage, lastTimeoutFillAmount > 0f, lastTimeoutFillAmount, lastTimeoutFillColor);
                ejectedTimeoutFillImage.gameObject.SetActive(lastTimeoutFillAmount > 0f);
            }

            private static void CopyRect(RectTransform source, RectTransform target)
            {
                target.anchorMin = source.anchorMin;
                target.anchorMax = source.anchorMax;
                target.pivot = source.pivot;
                target.anchoredPosition = source.anchoredPosition;
                target.sizeDelta = source.sizeDelta;
                target.offsetMin = source.offsetMin;
                target.offsetMax = source.offsetMax;
                target.localRotation = source.localRotation;
                target.localScale = source.localScale;
            }

            private static Sprite GetShineSprite()
            {
                if (shineSprite != null)
                {
                    return shineSprite;
                }

                const int width = 16;
                const int height = 24;
                Texture2D texture = new(width, height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };

                for (int y = 0; y < height; y++)
                {
                    float v = y / (float)(height - 1);
                    int centerX = Mathf.RoundToInt((0.5f + (v - 0.5f) * 0.22f) * (width - 1));

                    for (int x = 0; x < width; x++)
                    {
                        int distance = Mathf.Abs(x - centerX);
                        float edgeFade = y == 0 || y == height - 1 ? 0.35f : 1f;
                        float alpha = distance switch
                        {
                            0 => 0.95f,
                            1 => 0.72f,
                            2 when (x + y) % 2 == 0 => 0.38f,
                            _ => 0f
                        };

                        if ((x + y) % 7 == 0)
                        {
                            alpha *= 0.55f;
                        }

                        alpha *= edgeFade;
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }

                texture.Apply();
                shineSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), height);
                return shineSprite;
            }

            private void CaptureTransforms()
            {
                bodyTransform = TransformState.Capture(bodyImage);
                shineTransform = TransformState.Capture(shineImage);
                ejectedBodyTransform = TransformState.Capture(ejectedBodyImage);
            }

            private void RestoreTransforms()
            {
                bodyTransform.Restore(bodyImage);
                shineTransform.Restore(shineImage);
                ejectedBodyTransform.Restore(ejectedBodyImage);
            }

            private static Color GetColor(Image image, Color fallback)
            {
                return image != null ? image.color : fallback;
            }

            private Color GetBodyColor(bool usable, bool recovered)
            {
                Color color = GetBaseBodyColor();
                color.a = usable && recovered ? color.a : 0f;
                return color;
            }

            private Color GetBaseBodyColor()
            {
                if (!hasBaseBodyColor)
                {
                    baseBodyColor = GetColor(bodyImage, Color.white);
                    hasBaseBodyColor = true;
                }

                return baseBodyColor;
            }

            private static void SetImageAlpha(Image image, float alpha)
            {
                if (image == null)
                {
                    return;
                }

                Color color = image.color;
                color.a = alpha;
                ApplyColor(image, color);
            }

            private static Color WithAlpha(Color color, float alpha)
            {
                color.a = alpha;
                return color;
            }

            private static void SetTransientImageVisible(Image image, bool visible)
            {
                if (image == null)
                {
                    return;
                }

                image.raycastTarget = false;
                image.gameObject.SetActive(visible);
                if (!visible)
                {
                    SetImageAlpha(image, 0f);
                }
            }

            private readonly struct TransformState
            {
                public readonly bool IsValid;
                public readonly Vector2 AnchoredPosition;
                public readonly Quaternion LocalRotation;
                public readonly Vector3 LocalScale;

                private TransformState(RectTransform rect)
                {
                    IsValid = rect != null;
                    AnchoredPosition = IsValid ? rect.anchoredPosition : Vector2.zero;
                    LocalRotation = IsValid ? rect.localRotation : Quaternion.identity;
                    LocalScale = IsValid ? rect.localScale : Vector3.one;
                }

                public static TransformState Capture(Image image)
                {
                    return new TransformState(image != null ? image.rectTransform : null);
                }

                public void Restore(Image image)
                {
                    if (image == null || !IsValid)
                    {
                        return;
                    }

                    RectTransform rect = image.rectTransform;
                    rect.anchoredPosition = AnchoredPosition;
                    rect.localRotation = LocalRotation;
                    rect.localScale = LocalScale;
                }
            }
        }

        [SerializeField] private bool bindPlayerOnEnable = true;
        [SerializeField] private BulletGauge target;
        [Header("Effects")]
        [SerializeField, Min(0.01f)] private float hitSpendSeconds = 0.28f;
        [SerializeField, Min(0.01f)] private float attackSpendSeconds = 0.42f;
        [SerializeField, Min(0.01f)] private float recoverySeconds = 0.34f;
        [SerializeField, Min(0.01f)] private float executionRecoverySeconds = 1f;
        [SerializeField, Min(0f)] private float attackEjectDistance = 52f;
        [SerializeField, Min(0f)] private float attackEjectRise = 18f;
        [SerializeField] private float attackEjectSpinDegrees = 520f;
        [SerializeField, Range(0f, 1f)] private float shineMaxAlpha = 0.85f;
        [Header("Hit Shake")]
        [SerializeField] private RectTransform rotationRoot;
        [SerializeField, Min(0f)] private float hitShakeSeconds = 0.18f;
        [SerializeField, Min(0f)] private float hitShakeRotationDegrees = 12f;
        [SerializeField, Min(0f)] private float hitShakeScaleAmount = 0.15f;
        [SerializeField, Min(0f)] private float hitShakeFrequency = 40f;
        [Header("Bullet Timeout")]
        [SerializeField, Min(0.01f)] private float bulletLifetimeSeconds = 10f;
        [SerializeField, Range(0.01f, 1f)] private float timeoutGaugeFullRatio = 0.8f;
        [SerializeField] private Color timeoutGaugeColor = new(1f, 0.12f, 0.08f, 1f);
        [SerializeField, Min(0f)] private float timeoutShiftSeconds = 0.18f;
        [SerializeField, Min(0f)] private float warningBlinkFrequency = 7f;
        [SerializeField, Range(0f, 1f)] private float warningBlinkMinAlpha = 0.35f;
        [Header("Top To Bottom")]
        [SerializeField] private HpSlot hp5 = new();
        [SerializeField] private HpSlot hp4 = new();
        [SerializeField] private HpSlot hp3 = new();
        [SerializeField] private HpSlot hp2 = new();
        [SerializeField] private HpSlot hp1 = new();

        public float ExecutionRecoveryEffectSeconds => Mathf.Max(0.01f, executionRecoverySeconds);

        private bool hasSnapshot;
        private int previousCurrent;
        private int previousMax;
        private Quaternion baseRotationRootLocalRotation;
        private Vector3 baseRotationRootLocalScale;
        private bool hasBaseRotationRootTransform;
        private float hitShakeStartedAt;
        private float hitShakeEndsAt;
        private readonly List<float> bulletLoadedTimes = new(VisibleSlotCount);
        private int pendingExpiredBulletIndex = -1;
        private int lastExpiredBulletIndex = -1;

        private void OnEnable()
        {
            hasSnapshot = false;
            CacheRotationRoot();
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
            TickBulletTimeouts();
            UpdateTimeoutVisuals();

            if (!bindPlayerOnEnable || PlayerCombatController.Active == null)
            {
                return;
            }

            BulletGauge playerBullets = PlayerCombatController.Active.Bullets;
            if (target == playerBullets)
            {
                return;
            }

            SetTarget(playerBullets);
        }

        public void SetTarget(BulletGauge nextTarget)
        {
            if (target == nextTarget)
            {
                return;
            }

            Unsubscribe();
            target = nextTarget;
            hasSnapshot = false;
            Subscribe();
            Refresh();
        }

        public void SetExecutionVisible(bool visible)
        {
            if (gameObject.activeSelf == visible)
            {
                if (visible)
                {
                    Refresh();
                }

                return;
            }

            gameObject.SetActive(visible);
        }

        private void TryBindPlayer()
        {
            if (!bindPlayerOnEnable || PlayerCombatController.Active == null)
            {
                return;
            }

            target = PlayerCombatController.Active.Bullets;
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
            Apply(current, max, target != null ? target.LastChangeSource : BulletChangeSource.None);
        }

        private void Refresh()
        {
            if (target == null)
            {
                Apply(0, 0, BulletChangeSource.None);
                return;
            }

            Apply(target.CurrentBullets, target.MaxBullets, target.LastChangeSource);
        }

        private void Apply(int current, int max, BulletChangeSource source)
        {
            if (hasSnapshot && source == BulletChangeSource.Hit && current < previousCurrent)
            {
                PlayHitShake();
            }

            bool expired = hasSnapshot
                && source == BulletChangeSource.Expired
                && current < previousCurrent;

            SyncBulletTimers(current, source);
            if (expired)
            {
                ApplyExpiredSlots(current, max);
            }
            else
            {
                ApplySlot(hp5, 5, current, max, source);
                ApplySlot(hp4, 4, current, max, source);
                ApplySlot(hp3, 3, current, max, source);
                ApplySlot(hp2, 2, current, max, source);
                ApplySlot(hp1, 1, current, max, source);
            }

            previousCurrent = current;
            previousMax = max;
            hasSnapshot = true;
            UpdateTimeoutVisuals();
        }

        private void ApplySlot(HpSlot slot, int hp, int current, int max, BulletChangeSource source)
        {
            if (slot == null)
            {
                return;
            }

            bool usable = max >= hp;
            bool recovered = usable && current >= hp;

            if (!hasSnapshot)
            {
                slot.ApplyInstant(usable, recovered);
                return;
            }

            bool wasUsable = previousMax >= hp;
            bool wasRecovered = wasUsable && previousCurrent >= hp;

            if (wasUsable && !usable)
            {
                slot.ApplyInstant(usable, false);
            }
            else if (!wasUsable && usable)
            {
                if (recovered)
                {
                    slot.PlayRecovery(GetRecoverySeconds(source));
                }
                else
                {
                    slot.ApplyInstant(usable, false);
                }
            }
            else if (wasRecovered && !recovered)
            {
                if (source == BulletChangeSource.Attack)
                {
                    slot.PlayAttackSpend(attackSpendSeconds);
                }
                else
                {
                    slot.PlayHitSpend(hitSpendSeconds);
                }
            }
            else if (!wasRecovered && recovered)
            {
                slot.PlayRecovery(GetRecoverySeconds(source));
            }
            else
            {
                slot.ApplyInstant(usable, recovered);
            }
        }

        private void ApplyExpiredSlots(int current, int max)
        {
            int expiredHp = lastExpiredBulletIndex + 1;
            if (expiredHp < 1 || expiredHp > VisibleSlotCount)
            {
                ApplySlot(hp5, 5, current, max, BulletChangeSource.None);
                ApplySlot(hp4, 4, current, max, BulletChangeSource.None);
                ApplySlot(hp3, 3, current, max, BulletChangeSource.None);
                ApplySlot(hp2, 2, current, max, BulletChangeSource.None);
                ApplySlot(hp1, 1, current, max, BulletChangeSource.None);
                return;
            }

            float shiftSeconds = Mathf.Max(0.01f, timeoutShiftSeconds);
            float expireSeconds = Mathf.Max(hitSpendSeconds, shiftSeconds);

            for (int hp = 1; hp <= VisibleSlotCount; hp++)
            {
                HpSlot slot = GetSlot(hp);
                if (slot == null)
                {
                    continue;
                }

                bool usable = max >= hp;

                if (hp < expiredHp)
                {
                    slot.ApplyInstant(usable, current >= hp);
                }
                else if (hp == expiredHp)
                {
                    if (current >= hp)
                    {
                        slot.PlayExpiredShift(expireSeconds, GetSlot(hp + 1));
                    }
                    else
                    {
                        slot.PlayHitSpend(hitSpendSeconds);
                    }
                }
                else if (hp <= current)
                {
                    slot.PlayShiftDown(shiftSeconds, GetSlot(hp + 1));
                }
                else
                {
                    slot.ApplyInstant(usable, false);
                }
            }
        }

        private void SyncBulletTimers(int current, BulletChangeSource source)
        {
            int targetCount = Mathf.Max(0, current);
            float now = Time.time;
            lastExpiredBulletIndex = -1;

            if (source == BulletChangeSource.CombatStart)
            {
                ResetBulletTimers(targetCount, true, now);
                return;
            }

            if (!hasSnapshot || source == BulletChangeSource.WeaponSwitch)
            {
                ResetBulletTimers(targetCount, false, now);
                return;
            }

            while (bulletLoadedTimes.Count > targetCount)
            {
                int removeIndex = bulletLoadedTimes.Count - 1;
                if (source == BulletChangeSource.Expired)
                {
                    removeIndex = ResolveExpiredBulletIndex();
                    lastExpiredBulletIndex = removeIndex;
                    pendingExpiredBulletIndex = -1;
                }

                bulletLoadedTimes.RemoveAt(removeIndex);
            }

            float loadedAt = source == BulletChangeSource.Parry ? now : UntimedBulletLoadedAt;
            while (bulletLoadedTimes.Count < targetCount)
            {
                bulletLoadedTimes.Add(loadedAt);
            }
        }

        private int ResolveExpiredBulletIndex()
        {
            if (pendingExpiredBulletIndex >= 0 && pendingExpiredBulletIndex < bulletLoadedTimes.Count)
            {
                return pendingExpiredBulletIndex;
            }

            int expiredIndex = FindExpiredTimedBulletIndex(Time.time);
            return expiredIndex >= 0 ? expiredIndex : 0;
        }

        private void ResetBulletTimers(int count, bool timed, float loadedAt)
        {
            bulletLoadedTimes.Clear();
            float value = timed ? loadedAt : UntimedBulletLoadedAt;
            for (int i = 0; i < count; i++)
            {
                bulletLoadedTimes.Add(value);
            }
        }

        private void TickBulletTimeouts()
        {
            if (target == null || bulletLifetimeSeconds <= 0f || bulletLoadedTimes.Count <= 0)
            {
                return;
            }

            if (GameModalState.BlocksGameplayInput)
            {
                return;
            }

            int current = Mathf.Max(0, target.CurrentBullets);
            if (bulletLoadedTimes.Count != current)
            {
                SyncBulletTimers(current, target.LastChangeSource);
            }

            if (bulletLoadedTimes.Count <= 0 || current <= 0)
            {
                return;
            }

            int expiredIndex = FindExpiredTimedBulletIndex(Time.time);
            if (expiredIndex < 0)
            {
                return;
            }

            pendingExpiredBulletIndex = expiredIndex;
            if (!target.TrySpend(1, BulletChangeSource.Expired))
            {
                pendingExpiredBulletIndex = -1;
            }
        }

        private int FindExpiredTimedBulletIndex(float now)
        {
            for (int i = 0; i < bulletLoadedTimes.Count; i++)
            {
                if (!IsTimedBullet(i))
                {
                    continue;
                }

                if (now - bulletLoadedTimes[i] >= bulletLifetimeSeconds)
                {
                    return i;
                }
            }

            return -1;
        }

        private void UpdateTimeoutVisuals()
        {
            float now = Time.time;
            for (int hp = 1; hp <= VisibleSlotCount; hp++)
            {
                HpSlot slot = GetSlot(hp);
                if (slot == null)
                {
                    continue;
                }

                bool usable = target != null && target.MaxBullets >= hp;
                bool recovered = usable && hp - 1 < bulletLoadedTimes.Count;
                bool timed = recovered && IsTimedBullet(hp - 1);
                float age = timed ? Mathf.Max(0f, now - bulletLoadedTimes[hp - 1]) : 0f;
                float fillAmount = GetTimeoutFillAmount(age);
                float iconAlpha = timed && IsInTimeoutWarning(age) ? GetWarningBlinkAlpha() : 1f;
                slot.SetTimeoutVisual(usable, recovered, fillAmount, timeoutGaugeColor, iconAlpha);
            }
        }

        private bool IsTimedBullet(int index)
        {
            return index >= 0
                && index < bulletLoadedTimes.Count
                && bulletLoadedTimes[index] >= 0f;
        }

        private float GetTimeoutFillAmount(float age)
        {
            if (bulletLifetimeSeconds <= 0f)
            {
                return 0f;
            }

            float fullSeconds = bulletLifetimeSeconds * Mathf.Clamp(timeoutGaugeFullRatio, 0.01f, 1f);
            return Mathf.Clamp01(age / fullSeconds);
        }

        private bool IsInTimeoutWarning(float age)
        {
            return bulletLifetimeSeconds > 0f
                && age >= bulletLifetimeSeconds * Mathf.Clamp(timeoutGaugeFullRatio, 0.01f, 1f);
        }

        private float GetWarningBlinkAlpha()
        {
            if (warningBlinkFrequency <= 0f)
            {
                return 1f;
            }

            float wave = Mathf.Sin(Time.unscaledTime * warningBlinkFrequency * Mathf.PI * 2f) * 0.5f + 0.5f;
            return Mathf.Lerp(warningBlinkMinAlpha, 1f, wave);
        }

        private HpSlot GetSlot(int hp)
        {
            return hp switch
            {
                1 => hp1,
                2 => hp2,
                3 => hp3,
                4 => hp4,
                5 => hp5,
                _ => null
            };
        }

        private void TickEffects()
        {
            float deltaTime = Time.unscaledDeltaTime;
            hp5?.Tick(deltaTime, shineMaxAlpha, attackEjectDistance, attackEjectRise, attackEjectSpinDegrees);
            hp4?.Tick(deltaTime, shineMaxAlpha, attackEjectDistance, attackEjectRise, attackEjectSpinDegrees);
            hp3?.Tick(deltaTime, shineMaxAlpha, attackEjectDistance, attackEjectRise, attackEjectSpinDegrees);
            hp2?.Tick(deltaTime, shineMaxAlpha, attackEjectDistance, attackEjectRise, attackEjectSpinDegrees);
            hp1?.Tick(deltaTime, shineMaxAlpha, attackEjectDistance, attackEjectRise, attackEjectSpinDegrees);
            ApplyHitShakeOffset();
        }

        private void CacheRotationRoot()
        {
            if (hasBaseRotationRootTransform)
            {
                return;
            }

            if (rotationRoot == null)
            {
                rotationRoot = transform.Find("HPRotationRoot") as RectTransform;
            }

            if (rotationRoot != null)
            {
                baseRotationRootLocalRotation = rotationRoot.localRotation;
                baseRotationRootLocalScale = rotationRoot.localScale;
                hasBaseRotationRootTransform = true;
            }
        }

        private void PlayHitShake()
        {
            CacheRotationRoot();
            float now = Time.unscaledTime;
            hitShakeStartedAt = now;
            hitShakeEndsAt = now + hitShakeSeconds;
        }

        private void ApplyHitShakeOffset()
        {
            if (!hasBaseRotationRootTransform || rotationRoot == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            rotationRoot.localRotation = baseRotationRootLocalRotation * Quaternion.Euler(0f, 0f, GetHitShakeRotationDegrees(now));
            rotationRoot.localScale = baseRotationRootLocalScale * GetHitShakeScaleMultiplier(now);
        }

        private float GetHitShakeRotationDegrees(float now)
        {
            if (now >= hitShakeEndsAt || hitShakeSeconds <= 0f || hitShakeRotationDegrees <= 0f)
            {
                return 0f;
            }

            float elapsed = Mathf.Max(0f, now - hitShakeStartedAt);
            float normalized = Mathf.Clamp01(elapsed / hitShakeSeconds);
            float damping = 1f - normalized;
            float wave = Mathf.Sin(elapsed * hitShakeFrequency);
            return hitShakeRotationDegrees * damping * wave;
        }

        private float GetHitShakeScaleMultiplier(float now)
        {
            if (now >= hitShakeEndsAt || hitShakeSeconds <= 0f || hitShakeScaleAmount <= 0f)
            {
                return 1f;
            }

            float elapsed = Mathf.Max(0f, now - hitShakeStartedAt);
            float normalized = Mathf.Clamp01(elapsed / hitShakeSeconds);
            float pulse = Mathf.Sin(normalized * Mathf.PI);
            return 1f + pulse * hitShakeScaleAmount;
        }

        private float GetRecoverySeconds(BulletChangeSource source)
        {
            return source == BulletChangeSource.Execution
                ? executionRecoverySeconds
                : recoverySeconds;
        }

    }
}
