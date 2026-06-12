using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Week14.Map;

namespace Week14.UI
{
    public sealed class ShuffleTimerView : MonoBehaviour
    {
        [SerializeField] private RoomGridManager gridManager;
        [SerializeField] private Image fillImage;
        [SerializeField] private TextMeshProUGUI valueText;
        [SerializeField] private string textFormat = "{0:0.0}s";
        [SerializeField] private bool hideWhenInactive = true;

        private void Update()
        {
            if (gridManager == null)
            {
                SetVisible(!hideWhenInactive);
                return;
            }

            SetVisible(true);

            float interval = Mathf.Max(0.01f, gridManager.ShuffleIntervalSeconds);
            float remaining = gridManager.SecondsUntilShuffle;

            if (fillImage != null)
            {
                fillImage.fillAmount = Mathf.Clamp01(remaining / interval);
            }

            if (valueText != null)
            {
                valueText.text = string.Format(textFormat, remaining);
            }
        }

        public void Configure(RoomGridManager manager, Image image, TextMeshProUGUI text = null)
        {
            gridManager = manager;
            fillImage = image;
            valueText = text;
        }

        private void SetVisible(bool visible)
        {
            if (fillImage != null)
            {
                fillImage.enabled = visible;
            }

            if (valueText != null)
            {
                valueText.enabled = visible;
            }
        }
    }
}
