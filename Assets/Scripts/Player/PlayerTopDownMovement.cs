using UnityEngine;
using Week14.Enemy;
using Week14.Input;

namespace Week14.Combat
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerTopDownMovement : MonoBehaviour
    {
        private const string WallLayerName = "Wall";

        [SerializeField] private PlayerCombatController combat;

        private Rigidbody2D body;
        private Vector2 moveInput;
        private int wallLayerMask;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            int wallLayer = LayerMask.NameToLayer(WallLayerName);
            wallLayerMask = wallLayer >= 0 ? 1 << wallLayer : 0;

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
            HackerWireGrab wireGrab = GetComponent<HackerWireGrab>();
            if (wireGrab != null && wireGrab.TryPull(body))
            {
                moveInput = Vector2.zero;
                return;
            }

            if (combat != null && !combat.CanMove)
            {
                moveInput = Vector2.zero;
                if (combat.ShouldStopMovementWhenBlocked)
                {
                    body.linearVelocity = Vector2.zero;
                }
                else
                {
                    body.linearVelocity = ClampWallVelocity(body.linearVelocity);
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

            Vector2 velocity = moveInput * config.MoveSpeed * combat.MoveSpeedMultiplier;
            body.linearVelocity = ClampWallVelocity(GroundMovementConstraint.ClampVelocity(body, velocity));
        }

        private Vector2 ClampWallVelocity(Vector2 velocity)
        {
            Vector2 wallClampedVelocity = GroundMovementConstraint.ClampVelocityAgainstLayer(body, velocity, wallLayerMask);
            return GroundMovementConstraint.ClampVelocityAgainstPlayerOnlyBarriers(body, wallClampedVelocity);
        }
    }
}
