using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Week14.Input
{
    public static class GameInput
    {
        private const string PlayerMapName = "Player";
        private const string MoveActionName = "Move";
        private const string LookActionName = "Look";
        private const string LeftAttackActionName = "LeftAttack";
        private const string RightAttackActionName = "RightAttack";
        private const string HelpActionName = "Help";
        private const string UseSkillActionName = "UseSkill";
#if ENABLE_INPUT_SYSTEM
        private static PlayerInput playerInput;
        private static InputAction moveAction;
        private static InputAction lookAction;
        private static InputAction leftAttackAction;
        private static InputAction rightAttackAction;
        private static InputAction helpAction;
        private static InputAction useSkillAction;
#else
        private static readonly object moveAction = null;
        private static readonly object leftAttackAction = null;
        private static readonly object rightAttackAction = null;
        private static readonly object helpAction = null;
        private static readonly object useSkillAction = null;
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
            helpAction = null;
            useSkillAction = null;
        }
#endif

        public static Vector2 Move => ReadVector2(moveAction);
        public static bool LeftAttackDown => WasPressed(leftAttackAction);
        public static bool LeftAttackHeld => IsHeld(leftAttackAction);
        public static bool LeftAttackUp => WasReleased(leftAttackAction);
        public static bool RightAttackDown => WasPressed(rightAttackAction);
        public static bool HelpDown => WasPressed(helpAction);
        public static bool UseSkillDown => WasPressed(useSkillAction);

        public static Vector2 MouseScreenPosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Pointer pointer = GetActivePointer();
                return pointer != null ? pointer.position.ReadValue() : Vector2.zero;
#else
                return Vector2.zero;
#endif
            }
        }

#if ENABLE_INPUT_SYSTEM
        private static void ResolveActions()
        {
            moveAction = FindPlayerAction(MoveActionName);
            lookAction = FindPlayerAction(LookActionName);
            leftAttackAction = FindPlayerAction(LeftAttackActionName);
            rightAttackAction = FindPlayerAction(RightAttackActionName);
            helpAction = FindPlayerAction(HelpActionName);
            useSkillAction = FindPlayerAction(UseSkillActionName);
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
            EnableAction(helpAction);
            EnableAction(useSkillAction);
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
            return action != null && IsKeyboardMouseAction(action)
                ? Vector2.ClampMagnitude(action.ReadValue<Vector2>(), 1f)
                : Vector2.zero;
        }

        private static bool WasPressed(InputAction action)
        {
            return action != null && action.WasPressedThisFrame() && IsKeyboardMouseAction(action);
        }

        private static bool IsHeld(InputAction action)
        {
            return action != null && action.IsPressed() && IsKeyboardMouseAction(action);
        }

        private static bool WasReleased(InputAction action)
        {
            return action != null && action.WasReleasedThisFrame() && IsKeyboardMouseAction(action);
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

        private static bool IsKeyboardMouseAction(InputAction action)
        {
            InputControl activeControl = action.activeControl;
            return activeControl == null
                || activeControl.device is Keyboard
                || activeControl.device is Pointer;
        }
#else
        private static Vector2 ReadVector2(object _) => Vector2.zero;
        private static bool WasPressed(object _) => false;
        private static bool IsHeld(object _) => false;
        private static bool WasReleased(object _) => false;
#endif
    }
}
