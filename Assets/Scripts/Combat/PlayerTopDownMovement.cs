using System.Collections.Generic;
using UnityEngine;
using Week14.Input;

namespace Week14.Combat
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerTopDownMovement : MonoBehaviour
    {
        private const float MoveInputThresholdSqr = 0.0001f;

        [SerializeField] private PlayerCombatController combat;

        private readonly List<DashAfterimage> dashAfterimages = new();
        private Rigidbody2D body;
        private Vector2 moveInput;
        private Vector2 lastMoveDirection;
        private Vector2 dashDirection;
        private bool dashRequested;
        private bool isDashing;
        private float dashSpeed;
        private float dashEndsAt;
        private float nextDashReadyAt;
        private float nextAfterimageAt;
        private SpriteRenderer[] cachedRenderers;

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

            CacheSpriteRenderers();
        }

        private void OnDisable()
        {
            isDashing = false;
            ClearDashAfterimages();
        }

        private void Update()
        {
            ReadMoveInput();
            if (GameInput.DashDown)
            {
                dashRequested = true;
            }

            TickDashAfterimages();
            SpawnDashAfterimageIfNeeded();
        }

        private void FixedUpdate()
        {
            ReadMoveInput();

            if (combat != null && !combat.CanMove)
            {
                StopDash();
                dashRequested = false;
                if (!combat.IsBodyContactStaggered)
                {
                    body.linearVelocity = Vector2.zero;
                }

                return;
            }

            if (isDashing && Time.time >= dashEndsAt)
            {
                StopDash();
            }

            if (!isDashing && dashRequested)
            {
                TryStartDash();
            }

            dashRequested = false;

            PlayerCombatConfig config = combat != null ? combat.Config : null;
            if (config == null)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            if (isDashing)
            {
                body.linearVelocity = dashDirection * dashSpeed;
                return;
            }

            float speed = config.MoveSpeed;
            body.linearVelocity = moveInput * speed;
        }

        private void ReadMoveInput()
        {
            moveInput = GameInput.Move;
            if (moveInput.sqrMagnitude > MoveInputThresholdSqr)
            {
                lastMoveDirection = moveInput.normalized;
            }
        }

        private void TryStartDash()
        {
            PlayerCombatConfig config = combat != null ? combat.Config : null;
            if (config == null || Time.time < nextDashReadyAt)
            {
                return;
            }

            Vector2 direction = GetDashDirection();
            if (direction.sqrMagnitude <= MoveInputThresholdSqr)
            {
                return;
            }

            float dashDistance = Mathf.Max(0f, config.DashDistance);
            float dashSeconds = Mathf.Max(0.01f, config.DashSeconds);
            if (dashDistance <= 0f)
            {
                return;
            }

            isDashing = true;
            dashDirection = direction.normalized;
            dashSpeed = dashDistance / dashSeconds;
            dashEndsAt = Time.time + dashSeconds;
            nextDashReadyAt = Time.time + Mathf.Max(dashSeconds, config.DashCooldownSeconds);
            combat.GrantDashInvincibility(config.DashInvincibleSeconds);
            SpawnDashAfterimage();
            nextAfterimageAt = Time.time + Mathf.Max(0.01f, config.DashAfterimageInterval);
        }

        private Vector2 GetDashDirection()
        {
            if (moveInput.sqrMagnitude > MoveInputThresholdSqr)
            {
                return moveInput.normalized;
            }

            return lastMoveDirection;
        }

        private void StopDash()
        {
            isDashing = false;
            dashSpeed = 0f;
        }

        private void CacheSpriteRenderers()
        {
            cachedRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        private void SpawnDashAfterimageIfNeeded()
        {
            if (!isDashing || combat == null || combat.Config == null || Time.time < nextAfterimageAt)
            {
                return;
            }

            SpawnDashAfterimage();
            nextAfterimageAt = Time.time + Mathf.Max(0.01f, combat.Config.DashAfterimageInterval);
        }

        private void SpawnDashAfterimage()
        {
            PlayerCombatConfig config = combat != null ? combat.Config : null;
            if (config == null || config.DashAfterimageSeconds <= 0f)
            {
                return;
            }

            if (cachedRenderers == null || cachedRenderers.Length == 0)
            {
                CacheSpriteRenderers();
            }

            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                SpriteRenderer source = cachedRenderers[i];
                if (source == null || !source.enabled || source.sprite == null || !source.gameObject.activeInHierarchy)
                {
                    continue;
                }

                GameObject imageObject = new("DashAfterimage");
                imageObject.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                imageObject.transform.localScale = source.transform.lossyScale;

                SpriteRenderer image = imageObject.AddComponent<SpriteRenderer>();
                image.sprite = source.sprite;
                image.flipX = source.flipX;
                image.flipY = source.flipY;
                image.sortingLayerID = source.sortingLayerID;
                image.sortingOrder = source.sortingOrder - 1;

                Color color = config.DashAfterimageColor;
                color.a *= source.color.a;
                image.color = color;
                dashAfterimages.Add(new DashAfterimage(image, Time.time, config.DashAfterimageSeconds, color));
            }
        }

        private void TickDashAfterimages()
        {
            float now = Time.time;
            for (int i = dashAfterimages.Count - 1; i >= 0; i--)
            {
                if (dashAfterimages[i].Tick(now))
                {
                    dashAfterimages[i].Destroy();
                    dashAfterimages.RemoveAt(i);
                }
            }
        }

        private void ClearDashAfterimages()
        {
            for (int i = 0; i < dashAfterimages.Count; i++)
            {
                dashAfterimages[i].Destroy();
            }

            dashAfterimages.Clear();
        }

        private sealed class DashAfterimage
        {
            private readonly SpriteRenderer renderer;
            private readonly float createdAt;
            private readonly float seconds;
            private readonly Color baseColor;

            public DashAfterimage(SpriteRenderer renderer, float createdAt, float seconds, Color baseColor)
            {
                this.renderer = renderer;
                this.createdAt = createdAt;
                this.seconds = Mathf.Max(0.01f, seconds);
                this.baseColor = baseColor;
            }

            public bool Tick(float now)
            {
                if (renderer == null)
                {
                    return true;
                }

                float t = Mathf.Clamp01((now - createdAt) / seconds);
                Color color = baseColor;
                color.a = baseColor.a * (1f - t);
                renderer.color = color;
                return t >= 1f;
            }

            public void Destroy()
            {
                if (renderer != null)
                {
                    Object.Destroy(renderer.gameObject);
                }
            }
        }
    }
}
