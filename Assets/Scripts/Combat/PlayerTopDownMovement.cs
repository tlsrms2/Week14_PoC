using UnityEngine;
using Week14.Input;

namespace Week14.Combat
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerTopDownMovement : MonoBehaviour
    {
        [SerializeField] private PlayerCombatController combat;

        private Rigidbody2D body;
        private Vector2 moveInput;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.gravityScale = 0f;
            body.freezeRotation = true;

            if (combat == null)
            {
                combat = GetComponent<PlayerCombatController>();
            }
        }

        private void Update()
        {
            moveInput = GameInput.Move;
        }

        private void FixedUpdate()
        {
            moveInput = GameInput.Move;

            if (combat != null && !combat.CanMove)
            {
                if (!combat.IsBodyContactStaggered)
                {
                    body.linearVelocity = Vector2.zero;
                }

                return;
            }

            PlayerCombatConfig config = combat != null ? combat.Config : null;
            if (config == null)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            body.linearVelocity = moveInput * config.MoveSpeed;
        }
    }
}
