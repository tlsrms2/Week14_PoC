using UnityEngine;
using UnityEngine.UI;
using Week14.Combat;

namespace Week14.UI
{
    public sealed class HeatBarView : MonoBehaviour
    {
        private const float OutlineSeconds = 0.5f;
        private const float OutlineSize = 3f;

        [SerializeField] private HeatGauge target;
        [SerializeField] private bool bindPlayerOnStart = true;
        [SerializeField] private Image fillImage;
        [SerializeField] private Text valueText;
        [SerializeField] private string textFormat = "{0:0}/{1:0}";
        [SerializeField] private Color normalColor = new Color(1f, 0.55f, 0.1f);
        [SerializeField] private Color overheatedColor = Color.red;
        [SerializeField] private Color parryOutlineColor = Color.white;
        [SerializeField] private Color defenseOutlineColor = new(0.55f, 0.55f, 0.55f, 1f);
        [SerializeField] private Color hitOutlineColor = Color.yellow;
        [SerializeField] private Color overheatBlinkOutlineColor = Color.yellow;
        [SerializeField] private Color overheatBlinkFillColor = Color.yellow;
        [SerializeField, Min(0.1f)] private float overheatBlinkFrequency = 5f;
        [SerializeField, Range(0f, 1f)] private float overheatBlinkMinAlpha = 0.25f;
        [SerializeField] private bool useChangeOutline = true;
        [SerializeField] private bool showOverheatDrain = true;

        private readonly BarFillChangeFeedback changeFeedback = new();
        private float displayedAmount;
        private float outlineTimer;
        private bool hasDisplayedAmount;
        private Outline heatOutline;

        private void OnEnable()
        {
            changeFeedback.Configure(fillImage);
            EnsureOutline();
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
            changeFeedback.Tick();
            TickOutline();
            UpdateOverheatDrain();

            if (target != null || !bindPlayerOnStart)
            {
                return;
            }

            TryBindPlayer();
            Subscribe();
            Refresh();
        }

        public void SetTarget(HeatGauge nextTarget)
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

        public void Configure(Image image, Text text = null)
        {
            fillImage = image;
            valueText = text;
            changeFeedback.Configure(fillImage);
            EnsureOutline();
            Refresh();
        }

        public void SetBindPlayerOnStart(bool value)
        {
            bindPlayerOnStart = value;
        }

        public void SetChangeOutlineEnabled(bool value)
        {
            useChangeOutline = value;
            if (!useChangeOutline)
            {
                DisableOutline();
            }
        }

        public void SetOverheatDrainEnabled(bool value)
        {
            showOverheatDrain = value;
        }

        public void SetColors(Color normal, Color overheated)
        {
            normalColor = normal;
            overheatedColor = overheated;
            Refresh();
        }

        private void TryBindPlayer()
        {
            if (!bindPlayerOnStart || target != null || PlayerCombatController.Active == null)
            {
                return;
            }

            ApplyPlayerConfigColors();
            target = PlayerCombatController.Active.Heat;
        }

        private void Subscribe()
        {
            if (target == null)
            {
                return;
            }

            target.Changed += HandleChanged;
            target.Overheated += HandleStateChanged;
            target.Recovered += HandleStateChanged;
        }

        private void Unsubscribe()
        {
            if (target == null)
            {
                return;
            }

            target.Changed -= HandleChanged;
            target.Overheated -= HandleStateChanged;
            target.Recovered -= HandleStateChanged;
        }

        private void HandleChanged(float current, float max)
        {
            SetValue(current, max, true);
        }

        private void HandleStateChanged(HeatGauge heatGauge)
        {
            SetColor(heatGauge != null && heatGauge.IsOverheated);
            if (heatGauge == null || !heatGauge.IsOverheated)
            {
                DisableOutline();
            }
        }

        private void Refresh()
        {
            if (target == null)
            {
                SetValue(0f, 1f, false);
                SetColor(false);
                return;
            }

            ApplyPlayerConfigColorsIfNeeded();
            SetValue(target.CurrentHeat, target.MaxHeat, false);
            SetColor(target.IsOverheated);
        }

        private void SetValue(float current, float max, bool animate)
        {
            float safeMax = Mathf.Max(1f, max);

            if (fillImage != null)
            {
                float nextAmount = Mathf.Clamp01(current / safeMax);
                bool shouldAnimate = animate && hasDisplayedAmount && nextAmount > displayedAmount;
                HeatChangeSource source = target != null ? target.LastChangeSource : HeatChangeSource.None;
                changeFeedback.SetAmount(nextAmount, shouldAnimate);
                if (shouldAnimate)
                {
                    PlayOutline(source);
                }

                displayedAmount = nextAmount;
                hasDisplayedAmount = true;
                SetColor(target != null && target.IsOverheated);
            }

            if (valueText != null)
            {
                valueText.text = string.Format(textFormat, current, safeMax);
            }
        }

        private void SetColor(bool overheated)
        {
            if (fillImage != null)
            {
                fillImage.color = overheated ? overheatedColor : Color.Lerp(normalColor, overheatedColor, displayedAmount);
            }
        }

        private void UpdateOverheatDrain()
        {
            if (!showOverheatDrain || target == null || !target.IsOverheated || fillImage == null)
            {
                return;
            }

            displayedAmount = target.OverheatRemainingRatio;
            fillImage.fillAmount = displayedAmount;
            fillImage.color = GetOverheatBlinkFillColor();
            TickOverheatBlinkOutline();
            if (valueText != null)
            {
                valueText.text = string.Format(textFormat, target.MaxHeat * displayedAmount, target.MaxHeat);
            }
        }

        private void EnsureOutline()
        {
            heatOutline = null;
            if (fillImage == null)
            {
                return;
            }

            if (!useChangeOutline)
            {
                DisableOutline();
                return;
            }

            heatOutline = fillImage.GetComponent<Outline>();
            if (heatOutline == null)
            {
                heatOutline = fillImage.gameObject.AddComponent<Outline>();
            }

            heatOutline.useGraphicAlpha = false;
            heatOutline.enabled = false;
        }

        private void ApplyPlayerConfigColors()
        {
            PlayerCombatConfig config = PlayerCombatController.Active != null ? PlayerCombatController.Active.Config : null;
            if (config == null)
            {
                return;
            }

            parryOutlineColor = config.HeatParryOutlineColor;
            defenseOutlineColor = config.HeatDefenseOutlineColor;
            hitOutlineColor = config.HeatHitOutlineColor;
            overheatBlinkOutlineColor = config.HeatOverheatBlinkOutlineColor;
            overheatBlinkFillColor = config.HeatOverheatBlinkFillColor;
            overheatBlinkFrequency = config.HeatOverheatBlinkFrequency;
            overheatBlinkMinAlpha = config.HeatOverheatBlinkMinAlpha;
        }

        private void ApplyPlayerConfigColorsIfNeeded()
        {
            if (PlayerCombatController.Active == null || target != PlayerCombatController.Active.Heat)
            {
                return;
            }

            ApplyPlayerConfigColors();
        }

        private void PlayOutline(HeatChangeSource source)
        {
            if (!useChangeOutline || heatOutline == null)
            {
                return;
            }

            bool hit = source == HeatChangeSource.Hit;
            bool parry = source == HeatChangeSource.Parry || source == HeatChangeSource.Defense;
            if (!hit && !parry)
            {
                return;
            }

            outlineTimer = OutlineSeconds;
            heatOutline.effectColor = ResolveOutlineColor(source);
            heatOutline.effectDistance = Vector2.one * OutlineSize;
            heatOutline.enabled = true;
        }

        private void TickOutline()
        {
            if (heatOutline == null || !heatOutline.enabled)
            {
                return;
            }

            outlineTimer -= Time.deltaTime;
            if (outlineTimer <= 0f)
            {
                heatOutline.enabled = false;
                heatOutline.effectDistance = Vector2.zero;
                return;
            }

            bool hitOutline = target != null && target.LastChangeSource == HeatChangeSource.Hit;
            if (!hitOutline)
            {
                heatOutline.effectDistance = Vector2.one * OutlineSize;
                return;
            }

            heatOutline.effectDistance = new Vector2(
                Random.Range(-OutlineSize, OutlineSize),
                Random.Range(-OutlineSize, OutlineSize));
        }

        private Color ResolveOutlineColor(HeatChangeSource source)
        {
            return source switch
            {
                HeatChangeSource.Parry => parryOutlineColor,
                HeatChangeSource.Defense => defenseOutlineColor,
                HeatChangeSource.Hit => hitOutlineColor,
                _ => parryOutlineColor
            };
        }

        private void TickOverheatBlinkOutline()
        {
            if (!useChangeOutline || heatOutline == null)
            {
                return;
            }

            float alpha = GetOverheatBlinkAlpha();
            Color color = overheatBlinkOutlineColor;
            color.a *= alpha;
            heatOutline.effectColor = color;
            heatOutline.effectDistance = Vector2.one * OutlineSize;
            heatOutline.enabled = true;
        }

        private Color GetOverheatBlinkFillColor()
        {
            float blink = GetOverheatBlinkRatio();
            Color color = Color.Lerp(overheatedColor, overheatBlinkFillColor, blink);
            color.a *= GetOverheatBlinkAlpha();
            return color;
        }

        private float GetOverheatBlinkAlpha()
        {
            return Mathf.Lerp(overheatBlinkMinAlpha, 1f, GetOverheatBlinkRatio());
        }

        private float GetOverheatBlinkRatio()
        {
            return Mathf.Abs(Mathf.Sin(Time.time * Mathf.Max(0.1f, overheatBlinkFrequency)));
        }

        private void DisableOutline()
        {
            if (heatOutline == null && fillImage != null)
            {
                heatOutline = fillImage.GetComponent<Outline>();
            }

            if (heatOutline == null)
            {
                return;
            }

            heatOutline.enabled = false;
            heatOutline.effectDistance = Vector2.zero;
        }
    }
}
