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
            moveInput = combat == null || combat.CanMove ? GameInput.Move : Vector2.zero;
        }

        private void FixedUpdate()
        {
            if (combat != null && !combat.CanMove)
            {
                moveInput = Vector2.zero;
                if (combat.ShouldStopMovementWhenBlocked)
                {
                    body.linearVelocity = Vector2.zero;
                }

                return;
            }

            moveInput = GameInput.Move;
            PlayerCombatConfig config = combat != null ? combat.Config : null;
            if (config == null)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 velocity = moveInput * config.MoveSpeed;
            body.linearVelocity = GroundMovementConstraint.ClampVelocity(body, velocity);
        }
    }
}
