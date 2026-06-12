using UnityEngine;
using UnityEngine.UI;
using Week14.Combat;

namespace Week14.UI
{
    public sealed class HeatBarView : MonoBehaviour
    {
        [SerializeField] private HeatGauge target;
        [SerializeField] private bool bindPlayerOnStart = true;
        [SerializeField] private Image fillImage;
        [SerializeField] private Text valueText;
        [SerializeField] private string textFormat = "{0:0}/{1:0}";
        [SerializeField] private Color normalColor = new Color(1f, 0.55f, 0.1f);
        [SerializeField] private Color overheatedColor = Color.red;

        private void OnEnable()
        {
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
            Refresh();
        }

        public void SetBindPlayerOnStart(bool value)
        {
            bindPlayerOnStart = value;
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
            SetValue(current, max);
        }

        private void HandleStateChanged(HeatGauge heatGauge)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (target == null)
            {
                SetValue(0f, 1f);
                SetColor(false);
                return;
            }

            SetValue(target.CurrentHeat, target.MaxHeat);
            SetColor(target.IsOverheated);
        }

        private void SetValue(float current, float max)
        {
            float safeMax = Mathf.Max(1f, max);

            if (fillImage != null)
            {
                fillImage.fillAmount = Mathf.Clamp01(current / safeMax);
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
                fillImage.color = overheated ? overheatedColor : normalColor;
            }
        }
    }
}
