using UnityEngine;
using UnityEngine.UI;
using Week14.Combat;

namespace Week14.UI
{
    public sealed class DurabilityBarView : MonoBehaviour
    {
        [SerializeField] private Health target;
        [SerializeField] private bool bindPlayerOnStart = true;
        [SerializeField] private Image fillImage;
        [SerializeField] private Text valueText;
        [SerializeField] private string textFormat = "{0:0}/{1:0}";

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

        public void SetTarget(Health nextTarget)
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

        private void TryBindPlayer()
        {
            if (!bindPlayerOnStart || target != null || PlayerCombatController.Active == null)
            {
                return;
            }

            target = PlayerCombatController.Active.Health;
        }

        private void Subscribe()
        {
            if (target != null)
            {
                target.Changed += HandleChanged;
            }
        }

        private void Unsubscribe()
        {
            if (target != null)
            {
                target.Changed -= HandleChanged;
            }
        }

        private void HandleChanged(float current, float max)
        {
            SetValue(current, max);
        }

        private void Refresh()
        {
            if (target == null)
            {
                SetValue(0f, 1f);
                return;
            }

            SetValue(target.CurrentDurability, target.MaxDurability);
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
    }
}
