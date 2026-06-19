using System;
using UnityEngine;
using UnityEngine.UI;
using Week14.Combat;

namespace Week14.UI
{
    public sealed class PlayerHP : MonoBehaviour
    {
        private static readonly Color UsedBodyColor = new Color32(0x64, 0x64, 0x64, 0xff);
        private static readonly Color UsedOutlineColor = Color.black;
        private static readonly Color RecoveredBodyColor = Color.white;

        private enum EffectKind
        {
            None,
            AttackSpend,
            Recovery,
            EnrageFade
        }

        [Serializable]
        private sealed class HpSlot
        {
            [SerializeField] private Image bodyImage;
            [SerializeField] private Image outlineImage;
            [SerializeField] private Color recoveredOutlineColor = Color.white;

            private static Sprite shineSprite;

            private Image shineImage;
            private Image ejectedBodyImage;
            private Image ejectedOutlineImage;
            private EffectKind effectKind;
            private float timer;
            private float duration;
            private bool targetUsable;
            private bool targetRecovered;
            private Color targetOutlineColor = Color.white;
            private Color bodyStartColor;
            private Color bodyTargetColor;
            private Color outlineStartColor;
            private Color outlineTargetColor;
            private TransformState bodyTransform;
            private TransformState outlineTransform;
            private TransformState shineTransform;
            private TransformState ejectedBodyTransform;
            private TransformState ejectedOutlineTransform;

            public HpSlot()
            {
            }

            public HpSlot(Color defaultOutlineColor)
            {
                recoveredOutlineColor = defaultOutlineColor;
            }

            public Color RecoveredOutlineColor => recoveredOutlineColor;

            public void ApplyInstant(bool usable, bool recovered, Color outlineColor)
            {
                FinishEffect();
                targetUsable = usable;
                targetRecovered = recovered;
                targetOutlineColor = outlineColor;
                ApplyFinal();
            }

            public void PlayHitSpend(float effectSeconds, Color outlineColor)
            {
                PlayEjectSpend(effectSeconds, outlineColor);
            }

            public void PlayAttackSpend(float effectSeconds, Color outlineColor)
            {
                PlayEjectSpend(effectSeconds, outlineColor);
            }

            private void PlayEjectSpend(float effectSeconds, Color outlineColor)
            {
                EnsureEjectedImages();
                BeginEffect(EffectKind.AttackSpend, true, false, effectSeconds, outlineColor);
                PrepareEjectedImages();
            }

            public void PlayRecovery(float effectSeconds, Color outlineColor)
            {
                EnsureShineImage();
                BeginEffect(EffectKind.Recovery, true, true, effectSeconds, outlineColor);
                SetTransientImageVisible(shineImage, true);
            }

            public void PlayEnrageFade(float effectSeconds, Color outlineColor)
            {
                BeginEffect(EffectKind.EnrageFade, false, false, effectSeconds, outlineColor);
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
                    case EffectKind.EnrageFade:
                        TickEnrageFade(t);
                        break;
                }

                if (timer >= duration)
                {
                    FinishEffect();
                }
            }

            private void BeginEffect(EffectKind nextEffectKind, bool usable, bool recovered, float effectSeconds, Color outlineColor)
            {
                FinishEffect();
                targetUsable = usable;
                targetRecovered = recovered;
                targetOutlineColor = outlineColor;
                effectKind = nextEffectKind;
                timer = 0f;
                duration = Mathf.Max(0.01f, effectSeconds);

                CaptureTransforms();
                bodyStartColor = GetColor(bodyImage, usable && recovered ? RecoveredBodyColor : UsedBodyColor);
                outlineStartColor = GetColor(outlineImage, GetOutlineColor(usable, recovered));
                bodyTargetColor = GetBodyColor(usable, recovered);
                outlineTargetColor = GetOutlineColor(usable, recovered);
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
                ApplyOffset(ejectedOutlineImage, ejectedOutlineTransform, offset, rotation, 1f);
                ApplyColor(ejectedBodyImage, WithAlpha(bodyStartColor, bodyStartColor.a * (1f - fade)));
                ApplyColor(ejectedOutlineImage, WithAlpha(outlineStartColor, outlineStartColor.a * (1f - fade)));

                float colorT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.28f, t));
                ApplyColor(bodyImage, Color.Lerp(bodyStartColor, bodyTargetColor, colorT));
                ApplyColor(outlineImage, Color.Lerp(outlineStartColor, outlineTargetColor, colorT));
            }

            private void TickRecovery(float t, float shineAlpha)
            {
                float eased = Mathf.SmoothStep(0f, 1f, t);
                float pulse = Mathf.Sin(t * Mathf.PI);
                float scale = 1f + pulse * 0.08f;

                ApplyOffset(bodyImage, bodyTransform, Vector2.zero, 0f, scale);
                ApplyOffset(outlineImage, outlineTransform, Vector2.zero, 0f, scale);
                ApplyColor(bodyImage, Color.Lerp(bodyStartColor, bodyTargetColor, eased));
                ApplyColor(outlineImage, Color.Lerp(outlineStartColor, outlineTargetColor, eased));

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

            private void TickEnrageFade(float t)
            {
                float eased = Mathf.SmoothStep(0f, 1f, t);
                ApplyColor(bodyImage, Color.Lerp(bodyStartColor, bodyTargetColor, eased));
                ApplyColor(outlineImage, Color.Lerp(outlineStartColor, outlineTargetColor, eased));
            }

            private void FinishEffect()
            {
                if (effectKind == EffectKind.None)
                {
                    return;
                }

                RestoreTransforms();
                effectKind = EffectKind.None;
                SetTransientImageVisible(shineImage, false);
                SetTransientImageVisible(ejectedBodyImage, false);
                SetTransientImageVisible(ejectedOutlineImage, false);
                ApplyFinal();
            }

            private void ApplyFinal()
            {
                SetTransientImageVisible(shineImage, false);
                SetTransientImageVisible(ejectedBodyImage, false);
                SetTransientImageVisible(ejectedOutlineImage, false);

                bool usable = targetUsable;
                bool recovered = targetRecovered;
                if (!usable)
                {
                    SetAlpha(bodyImage, 0f);
                    SetAlpha(outlineImage, 0f);
                    return;
                }

                ApplyColor(bodyImage, GetBodyColor(usable, recovered));
                ApplyColor(outlineImage, GetOutlineColor(usable, recovered));
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

            private void EnsureShineImage()
            {
                if (shineImage != null)
                {
                    return;
                }

                RectTransform parent = bodyImage != null
                    ? bodyImage.rectTransform
                    : outlineImage != null ? outlineImage.rectTransform : null;
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
                ejectedBodyImage = EnsureEjectedImage(ejectedBodyImage, bodyImage, "EjectedBody");
                ejectedOutlineImage = EnsureEjectedImage(ejectedOutlineImage, outlineImage, "EjectedOutline");
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
                PrepareEjectedImage(ejectedOutlineImage, outlineImage);
                ejectedBodyTransform = TransformState.Capture(ejectedBodyImage);
                ejectedOutlineTransform = TransformState.Capture(ejectedOutlineImage);
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
                outlineTransform = TransformState.Capture(outlineImage);
                shineTransform = TransformState.Capture(shineImage);
                ejectedBodyTransform = TransformState.Capture(ejectedBodyImage);
                ejectedOutlineTransform = TransformState.Capture(ejectedOutlineImage);
            }

            private void RestoreTransforms()
            {
                bodyTransform.Restore(bodyImage);
                outlineTransform.Restore(outlineImage);
                shineTransform.Restore(shineImage);
                ejectedBodyTransform.Restore(ejectedBodyImage);
                ejectedOutlineTransform.Restore(ejectedOutlineImage);
            }

            private static Color GetColor(Image image, Color fallback)
            {
                return image != null ? image.color : fallback;
            }

            private Color GetBodyColor(bool usable, bool recovered)
            {
                if (!usable)
                {
                    Color color = GetColor(bodyImage, UsedBodyColor);
                    color.a = 0f;
                    return color;
                }

                return recovered ? RecoveredBodyColor : UsedBodyColor;
            }

            private Color GetOutlineColor(bool usable, bool recovered)
            {
                Color color = recovered ? targetOutlineColor : UsedOutlineColor;
                color.a = usable ? color.a : 0f;
                return color;
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

            private static void SetAlpha(Image image, float alpha)
            {
                if (image == null)
                {
                    return;
                }

                Color color = image.color;
                color.a = alpha;
                image.raycastTarget = false;
                image.color = color;
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
        [SerializeField, Min(0.01f)] private float enrageFadeSeconds = 0.65f;
        [SerializeField, Min(0f)] private float attackEjectDistance = 52f;
        [SerializeField, Min(0f)] private float attackEjectRise = 18f;
        [SerializeField] private float attackEjectSpinDegrees = 520f;
        [SerializeField, Range(0f, 1f)] private float shineMaxAlpha = 0.85f;
        [Header("Top To Bottom")]
        [SerializeField] private HpSlot hp5 = new(new Color32(0x8b, 0xff, 0x7a, 0xff));
        [SerializeField] private HpSlot hp4 = new(new Color32(0xb6, 0xff, 0x6f, 0xff));
        [SerializeField] private HpSlot hp3 = new(new Color32(0xff, 0xe9, 0x66, 0xff));
        [SerializeField] private HpSlot hp2 = new(new Color32(0xff, 0xae, 0x54, 0xff));
        [SerializeField] private HpSlot hp1 = new(new Color32(0xff, 0x63, 0x63, 0xff));

        public float ExecutionRecoveryEffectSeconds => Mathf.Max(0.01f, executionRecoverySeconds);

        private bool hasSnapshot;
        private int previousCurrent;
        private int previousMax;

        private void OnEnable()
        {
            hasSnapshot = false;
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
            Color outlineColor = ResolveOutlineColor(current);
            ApplySlot(hp5, 5, current, max, source, outlineColor);
            ApplySlot(hp4, 4, current, max, source, outlineColor);
            ApplySlot(hp3, 3, current, max, source, outlineColor);
            ApplySlot(hp2, 2, current, max, source, outlineColor);
            ApplySlot(hp1, 1, current, max, source, outlineColor);

            previousCurrent = current;
            previousMax = max;
            hasSnapshot = true;
        }

        private void ApplySlot(HpSlot slot, int hp, int current, int max, BulletChangeSource source, Color outlineColor)
        {
            if (slot == null)
            {
                return;
            }

            bool usable = max >= hp;
            bool recovered = usable && current >= hp;
            if (!hasSnapshot)
            {
                slot.ApplyInstant(usable, recovered, outlineColor);
                return;
            }

            bool wasUsable = previousMax >= hp;
            bool wasRecovered = wasUsable && previousCurrent >= hp;

            if (wasUsable && !usable)
            {
                slot.PlayEnrageFade(enrageFadeSeconds, outlineColor);
            }
            else if (!wasUsable && usable)
            {
                if (recovered)
                {
                    slot.PlayRecovery(GetRecoverySeconds(source), outlineColor);
                }
                else
                {
                    slot.ApplyInstant(usable, false, outlineColor);
                }
            }
            else if (wasRecovered && !recovered)
            {
                if (source == BulletChangeSource.Attack)
                {
                    slot.PlayAttackSpend(attackSpendSeconds, outlineColor);
                }
                else
                {
                    slot.PlayHitSpend(hitSpendSeconds, outlineColor);
                }
            }
            else if (!wasRecovered && recovered)
            {
                slot.PlayRecovery(GetRecoverySeconds(source), outlineColor);
            }
            else
            {
                slot.ApplyInstant(usable, recovered, outlineColor);
            }
        }

        private void TickEffects()
        {
            float deltaTime = Time.unscaledDeltaTime;
            hp5?.Tick(deltaTime, shineMaxAlpha, attackEjectDistance, attackEjectRise, attackEjectSpinDegrees);
            hp4?.Tick(deltaTime, shineMaxAlpha, attackEjectDistance, attackEjectRise, attackEjectSpinDegrees);
            hp3?.Tick(deltaTime, shineMaxAlpha, attackEjectDistance, attackEjectRise, attackEjectSpinDegrees);
            hp2?.Tick(deltaTime, shineMaxAlpha, attackEjectDistance, attackEjectRise, attackEjectSpinDegrees);
            hp1?.Tick(deltaTime, shineMaxAlpha, attackEjectDistance, attackEjectRise, attackEjectSpinDegrees);
        }

        private float GetRecoverySeconds(BulletChangeSource source)
        {
            return source == BulletChangeSource.Execution
                ? executionRecoverySeconds
                : recoverySeconds;
        }

        private Color ResolveOutlineColor(int current)
        {
            if (current <= 0)
            {
                return UsedOutlineColor;
            }

            return Mathf.Clamp(current, 1, 5) switch
            {
                5 => hp5 != null ? hp5.RecoveredOutlineColor : UsedOutlineColor,
                4 => hp4 != null ? hp4.RecoveredOutlineColor : UsedOutlineColor,
                3 => hp3 != null ? hp3.RecoveredOutlineColor : UsedOutlineColor,
                2 => hp2 != null ? hp2.RecoveredOutlineColor : UsedOutlineColor,
                _ => hp1 != null ? hp1.RecoveredOutlineColor : UsedOutlineColor
            };
        }
    }
}
