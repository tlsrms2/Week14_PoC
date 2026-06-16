using System;
using UnityEngine;
using UnityEngine.UI;
using Week14.Input;

namespace Week14.UI
{
    public sealed class StartControlSelect : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Button keyboardButton;
        [SerializeField] private Button gamepadButton;
        [SerializeField] private Color normalColor = new(0.12f, 0.16f, 0.2f, 0.96f);
        [SerializeField] private Color selectedColor = new(0.22f, 0.38f, 0.52f, 1f);
        [SerializeField] private int defaultSelectionIndex = 1;

        private const float MoveSelectThreshold = 0.5f;

        private int selectionIndex;
        private bool moveSelectHeld;

        public event Action<GameplayControlMode> Selected;

        private void Awake()
        {
            root ??= gameObject;
            selectionIndex = Mathf.Clamp(defaultSelectionIndex, 0, 1);
            RefreshSelection();
        }

        private void OnEnable()
        {
            keyboardButton?.onClick.AddListener(SelectKeyboard);
            gamepadButton?.onClick.AddListener(SelectGamepad);
        }

        private void OnDisable()
        {
            keyboardButton?.onClick.RemoveListener(SelectKeyboard);
            gamepadButton?.onClick.RemoveListener(SelectGamepad);
        }

        public void SetVisible(bool visible)
        {
            if (root != null)
            {
                root.SetActive(visible);
            }

            if (visible)
            {
                selectionIndex = Mathf.Clamp(defaultSelectionIndex, 0, 1);
                RefreshSelection();
            }
        }

        public void TickGamepadInput()
        {
            float horizontal = GameInput.Move.x;
            bool moveLeftDown = horizontal <= -MoveSelectThreshold && !moveSelectHeld;
            bool moveRightDown = horizontal >= MoveSelectThreshold && !moveSelectHeld;

            if (Mathf.Abs(horizontal) < MoveSelectThreshold)
            {
                moveSelectHeld = false;
            }
            else
            {
                moveSelectHeld = true;
            }

            if (GameInput.UiLeftDown || moveLeftDown)
            {
                selectionIndex = 0;
                RefreshSelection();
            }

            if (GameInput.UiRightDown || moveRightDown)
            {
                selectionIndex = 1;
                RefreshSelection();
            }

            if (!GameInput.UiChoiceDown)
            {
                return;
            }

            if (selectionIndex == 0)
            {
                SelectKeyboard();
            }
            else
            {
                SelectGamepad();
            }
        }

        public void SelectKeyboard()
        {
            selectionIndex = 0;
            RefreshSelection();
            Selected?.Invoke(GameplayControlMode.KeyboardMouse);
        }

        public void SelectGamepad()
        {
            selectionIndex = 1;
            RefreshSelection();
            Selected?.Invoke(GameplayControlMode.Gamepad);
        }

        private void RefreshSelection()
        {
            SetButtonColor(keyboardButton, selectionIndex == 0);
            SetButtonColor(gamepadButton, selectionIndex == 1);
        }

        private void SetButtonColor(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            Graphic graphic = button.targetGraphic != null ? button.targetGraphic : button.GetComponent<Graphic>();
            if (graphic != null)
            {
                graphic.color = selected ? selectedColor : normalColor;
            }
        }
    }
}
