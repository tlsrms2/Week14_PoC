using UnityEngine;
using Week14.Enemy;

namespace Week14.Combat
{
    [AddComponentMenu("Week14/Combat/Boss Execution Stage")]
    public sealed class BossExecutionStage : MonoBehaviour
    {
        [Header("Actor Anchors")]
        [SerializeField] private Transform playerStart;
        [SerializeField] private Transform bossPosition;
        [SerializeField] private Transform playerRollEnd;

        [Header("Camera Anchors")]
        [SerializeField] private Transform wideCameraFocus;
        [SerializeField] private Transform projectileFocusProxy;

        public Transform PlayerStart => playerStart;
        public Transform BossPosition => bossPosition;
        public Transform PlayerRollEnd => playerRollEnd;
        public Transform WideCameraFocus => wideCameraFocus;
        public Transform ProjectileFocusProxy => projectileFocusProxy;

        public bool HasRequiredAnchors =>
            playerStart != null
            && bossPosition != null
            && playerRollEnd != null
            && projectileFocusProxy != null;

        internal bool PlaceActors(PlayerCombatController player, BossAI boss)
        {
            if (player == null || boss == null || !HasRequiredAnchors)
            {
                return false;
            }

            MoveActor(player.transform, player.GetComponent<Rigidbody2D>(), playerStart.position);
            MoveActor(boss.transform, boss.Body, bossPosition.position);
            return true;
        }

        private static void MoveActor(Transform actor, Rigidbody2D body, Vector3 destination)
        {
            if (actor == null)
            {
                return;
            }

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.position = destination;
                return;
            }

            Vector3 position = actor.position;
            position.x = destination.x;
            position.y = destination.y;
            actor.position = position;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            DrawAnchor(playerStart, new Color(0.2f, 0.75f, 1f), "Player Start");
            DrawAnchor(bossPosition, new Color(1f, 0.25f, 0.15f), "Boss Position");
            DrawAnchor(playerRollEnd, new Color(0.2f, 1f, 0.4f), "Roll End");
            DrawAnchor(wideCameraFocus, new Color(1f, 0.85f, 0.2f), "Camera Focus");
            DrawAnchor(projectileFocusProxy, new Color(0.85f, 0.3f, 1f), "Projectile Focus");

            if (playerStart != null && bossPosition != null)
            {
                Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.65f);
                Gizmos.DrawLine(playerStart.position, bossPosition.position);
            }
        }

        private static void DrawAnchor(Transform anchor, Color color, string label)
        {
            if (anchor == null)
            {
                return;
            }

            Gizmos.color = color;
            Gizmos.DrawWireSphere(anchor.position, 0.18f);
            UnityEditor.Handles.Label(anchor.position + Vector3.up * 0.22f, label);
        }
#endif
    }
}
