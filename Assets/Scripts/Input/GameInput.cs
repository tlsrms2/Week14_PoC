using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Week14.Input
{
    public enum GameplayControlMode
    {
        KeyboardMouse,
        Gamepad
    }

    public static class GameInput
    {
        private const string PlayerMapName = "Player";
        private const string MoveActionName = "Move";
        private const string LookActionName = "Look";
        private const string LeftAttackActionName = "LeftAttack";
        private const string RightAttackActionName = "RightAttack";
        private const string DashActionName = "Dash";
        private const string LockOnActionName = "LockOn";
        private const string HelpActionName = "Help";
        private const string LeftActionName = "Left";
        private const string RightActionName = "Right";
        private const string UpActionName = "Up";
        private const string DownActionName = "Down";
        private const string ChoiceActionName = "Choice";
        public const float MinGamepadLookSensitivity = 0.25f;
        public const float MaxGamepadLookSensitivity = 2f;
        public const float DefaultGamepadLookSensitivity = 1f;

#if ENABLE_INPUT_SYSTEM
        private static PlayerInput playerInput;
        private static InputAction moveAction;
        private static InputAction lookAction;
        private static InputAction leftAttackAction;
        private static InputAction rightAttackAction;
        private static InputAction dashAction;
        private static InputAction lockOnAction;
        private static InputAction helpAction;
        private static InputAction leftAction;
        private static InputAction rightAction;
        private static InputAction upAction;
        private static InputAction downAction;
        private static InputAction choiceAction;
#else
        private static readonly object moveAction = null;
        private static readonly object leftAttackAction = null;
        private static readonly object rightAttackAction = null;
        private static readonly object dashAction = null;
        private static readonly object lockOnAction = null;
        private static readonly object helpAction = null;
        private static readonly object leftAction = null;
        private static readonly object rightAction = null;
        private static readonly object upAction = null;
        private static readonly object downAction = null;
        private static readonly object choiceAction = null;
#endif

#if ENABLE_INPUT_SYSTEM
        public static void Bind(PlayerInput input)
        {
            if (input == null || playerInput == input)
            {
                return;
            }

            playerInput = input;
            ResolveActions();
            EnableActions();
        }

        public static void Unbind(PlayerInput input)
        {
            if (playerInput != input)
            {
                return;
            }

            playerInput = null;
            moveAction = null;
            lookAction = null;
            leftAttackAction = null;
            rightAttackAction = null;
            dashAction = null;
            lockOnAction = null;
            helpAction = null;
            leftAction = null;
            rightAction = null;
            upAction = null;
            downAction = null;
            choiceAction = null;
        }
#endif

        private static float gamepadLookSensitivity = DefaultGamepadLookSensitivity;
        private static GameplayControlMode controlMode = GameplayControlMode.KeyboardMouse;

        public static GameplayControlMode ControlMode => controlMode;
        public static bool IsGamepadMode => controlMode == GameplayControlMode.Gamepad;
        public static Vector2 Move => ReadVector2(moveAction);
        public static bool DashDown => WasPressed(dashAction);
        public static bool LeftAttackDown => WasPressed(IsGamepadMode ? rightAttackAction : leftAttackAction);
        public static bool RightAttackDown => WasPressed(IsGamepadMode ? leftAttackAction : rightAttackAction);
        public static bool LockOnDown => WasPressed(lockOnAction);
        public static bool HelpDown => WasPressed(helpAction);
        public static bool UiLeftDown => WasPressed(leftAction);
        public static bool UiRightDown => WasPressed(rightAction);
        public static bool UiUpDown => WasPressed(upAction);
        public static bool UiDownDown => WasPressed(downAction);
        public static bool UiChoiceDown => WasPressed(choiceAction);
        public static bool IsGamepadInputActive => IsActiveDeviceGamepad();
        public static float GamepadLookSensitivity
        {
            get => gamepadLookSensitivity;
            set => gamepadLookSensitivity = Mathf.Clamp(value, MinGamepadLookSensitivity, MaxGamepadLookSensitivity);
        }

        public static void SelectControlMode(GameplayControlMode mode)
        {
            controlMode = mode;
            Cursor.visible = mode != GameplayControlMode.Gamepad;
            Cursor.lockState = CursorLockMode.None;
        }

        public static Vector2 MouseScreenPosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (IsGamepadMode)
                {
                    return Vector2.zero;
                }

                Pointer pointer = GetActivePointer();
                return pointer != null ? pointer.position.ReadValue() : Vector2.zero;
#else
                return Vector2.zero;
#endif
            }
        }

        public static bool TryGetLookDirection(out Vector2 direction)
        {
            direction = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            if (lookAction == null)
            {
                return false;
            }

            if (IsGamepadMode && TryReadCurrentGamepadLook(out direction))
            {
                return true;
            }

            if (IsGamepadMode && IsPointerLook())
            {
                return false;
            }

            if (!IsGamepadMode && IsPointerLook())
            {
                return false;
            }

            Vector2 look = lookAction.ReadValue<Vector2>();
            if (look.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            direction = Vector2.ClampMagnitude(look, 1f);
            return true;
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static void ResolveActions()
        {
            moveAction = FindPlayerAction(MoveActionName);
            lookAction = FindPlayerAction(LookActionName);
            leftAttackAction = FindPlayerAction(LeftAttackActionName);
            rightAttackAction = FindPlayerAction(RightAttackActionName);
            dashAction = FindPlayerAction(DashActionName);
            lockOnAction = FindPlayerAction(LockOnActionName);
            helpAction = FindPlayerAction(HelpActionName);
            leftAction = FindPlayerAction(LeftActionName);
            rightAction = FindPlayerAction(RightActionName);
            upAction = FindPlayerAction(UpActionName);
            downAction = FindPlayerAction(DownActionName);
            choiceAction = FindPlayerAction(ChoiceActionName);
        }

        private static InputAction FindPlayerAction(string actionName)
        {
            InputActionAsset actions = playerInput != null ? playerInput.actions : null;
            if (actions == null)
            {
                return null;
            }

            return actions.FindAction($"{PlayerMapName}/{actionName}", false)
                ?? actions.FindAction(actionName, false);
        }

        private static void EnableActions()
        {
            EnableAction(moveAction);
            EnableAction(lookAction);
            EnableAction(leftAttackAction);
            EnableAction(rightAttackAction);
            EnableAction(dashAction);
            EnableAction(lockOnAction);
            EnableAction(helpAction);
            EnableAction(leftAction);
            EnableAction(rightAction);
            EnableAction(upAction);
            EnableAction(downAction);
            EnableAction(choiceAction);
        }

        private static void EnableAction(InputAction action)
        {
            if (action != null && !action.enabled)
            {
                action.Enable();
            }
        }

        private static Vector2 ReadVector2(InputAction action)
        {
            return action != null ? Vector2.ClampMagnitude(action.ReadValue<Vector2>(), 1f) : Vector2.zero;
        }

        private static bool WasPressed(InputAction action)
        {
            return action != null && action.WasPressedThisFrame();
        }

        private static bool IsPointerLook()
        {
            InputControl activeControl = lookAction != null ? lookAction.activeControl : null;
            return activeControl != null && activeControl.device is Pointer;
        }

        private static bool IsGamepadLook()
        {
            InputControl activeControl = lookAction != null ? lookAction.activeControl : null;
            return activeControl != null && activeControl.device is Gamepad;
        }

        private static bool TryReadCurrentGamepadLook(out Vector2 direction)
        {
            direction = Vector2.zero;
            Gamepad gamepad = Gamepad.current;
            if (gamepad == null)
            {
                return false;
            }

            Vector2 look = gamepad.rightStick.ReadValue();
            if (look.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            direction = Vector2.ClampMagnitude(look, 1f);
            return true;
        }

        private static Pointer GetActivePointer()
        {
            InputControl activeControl = lookAction != null ? lookAction.activeControl : null;
            if (activeControl != null && activeControl.device is Pointer pointer)
            {
                return pointer;
            }

            return Pointer.current;
        }

        private static bool IsActiveDeviceGamepad()
        {
            InputControl activeControl = lookAction != null && lookAction.activeControl != null
                ? lookAction.activeControl
                : moveAction != null ? moveAction.activeControl : null;
            return activeControl != null && activeControl.device is Gamepad;
        }
#else
        private static Vector2 ReadVector2(object _) => Vector2.zero;
        private static bool WasPressed(object _) => false;
        private static bool IsActiveDeviceGamepad() => false;
#endif
    }
}
