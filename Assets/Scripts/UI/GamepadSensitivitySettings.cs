using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Week14.Input;

namespace Week14.UI
{
    public sealed class GamepadSensitivitySettings : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private TMP_Text sensitivityValueText;

        public Selectable Selectable => sensitivitySlider;

        private void Awake()
        {
            root ??= gameObject;
            ConfigureSlider();
            RefreshText();
        }

        private void OnEnable()
        {
            if (sensitivitySlider != null)
            {
                sensitivitySlider.onValueChanged.AddListener(SetGamepadLookSensitivity);
            }
        }

        private void OnDisable()
        {
            if (sensitivitySlider != null)
            {
                sensitivitySlider.onValueChanged.RemoveListener(SetGamepadLookSensitivity);
            }
        }

        public void SetVisible(bool visible)
        {
            if (root != null)
            {
                root.SetActive(visible);
            }

            if (visible)
            {
                ConfigureSlider();
                RefreshText();
            }
        }

        public void TickGamepadInput(bool hasFocus)
        {
            if (!hasFocus || !GameInput.IsGamepadInputActive)
            {
                return;
            }

            float horizontal = GameInput.Move.x;
            if (Mathf.Abs(horizontal) < 0.25f)
            {
                return;
            }

            GameInput.GamepadLookSensitivity += horizontal * 1.25f * Time.unscaledDeltaTime;
            RefreshText();
        }

        private void ConfigureSlider()
        {
            if (sensitivitySlider == null)
            {
                return;
            }

            sensitivitySlider.minValue = GameInput.MinGamepadLookSensitivity;
            sensitivitySlider.maxValue = GameInput.MaxGamepadLookSensitivity;
            sensitivitySlider.wholeNumbers = false;
            sensitivitySlider.SetValueWithoutNotify(GameInput.GamepadLookSensitivity);
        }

        private void SetGamepadLookSensitivity(float value)
        {
            GameInput.GamepadLookSensitivity = value;
            RefreshText();
        }

        private void RefreshText()
        {
            if (sensitivitySlider != null)
            {
                sensitivitySlider.SetValueWithoutNotify(GameInput.GamepadLookSensitivity);
            }

            if (sensitivityValueText != null)
            {
                sensitivityValueText.text = $"{GameInput.GamepadLookSensitivity:0.00}x";
            }
        }
    }
}
