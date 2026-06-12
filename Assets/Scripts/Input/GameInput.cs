using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Week14.Input
{
    public static class GameInput
    {
        public static Vector2 Move
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Vector2 move = Vector2.zero;
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    move.x += keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f;
                    move.x -= keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f;
                    move.y += keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f;
                    move.y -= keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f;
                }

                return Vector2.ClampMagnitude(move, 1f);
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Vector2.ClampMagnitude(new Vector2(UnityEngine.Input.GetAxisRaw("Horizontal"), UnityEngine.Input.GetAxisRaw("Vertical")), 1f);
#else
                return Vector2.zero;
#endif
            }
        }

        public static Vector2 MouseScreenPosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return UnityEngine.Input.mousePosition;
#else
                return Vector2.zero;
#endif
            }
        }

        public static bool Sprint
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Keyboard keyboard = Keyboard.current;
                return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
#elif ENABLE_LEGACY_INPUT_MANAGER
                return UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
#else
                return false;
#endif
            }
        }

        public static bool GetMouseButton(int button)
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }

            return button switch
            {
                0 => mouse.leftButton.isPressed,
                1 => mouse.rightButton.isPressed,
                2 => mouse.middleButton.isPressed,
                _ => false
            };
#elif ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetMouseButton(button);
#else
            return false;
#endif
        }

        public static bool GetMouseButtonDown(int button)
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }

            return button switch
            {
                0 => mouse.leftButton.wasPressedThisFrame,
                1 => mouse.rightButton.wasPressedThisFrame,
                2 => mouse.middleButton.wasPressedThisFrame,
                _ => false
            };
#elif ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetMouseButtonDown(button);
#else
            return false;
#endif
        }

        public static bool GetKeyDown(KeyCode keyCode)
        {
            if (keyCode == KeyCode.Mouse0)
            {
                return GetMouseButtonDown(0);
            }

            if (keyCode == KeyCode.Mouse1)
            {
                return GetMouseButtonDown(1);
            }

            if (keyCode == KeyCode.Mouse2)
            {
                return GetMouseButtonDown(2);
            }

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            return keyCode switch
            {
                KeyCode.Space => keyboard.spaceKey.wasPressedThisFrame,
                KeyCode.E => keyboard.eKey.wasPressedThisFrame,
                KeyCode.Q => keyboard.qKey.wasPressedThisFrame,
                KeyCode.R => keyboard.rKey.wasPressedThisFrame,
                KeyCode.F => keyboard.fKey.wasPressedThisFrame,
                _ => false
            };
#elif ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetKeyDown(keyCode);
#else
            return false;
#endif
        }
    }
}
