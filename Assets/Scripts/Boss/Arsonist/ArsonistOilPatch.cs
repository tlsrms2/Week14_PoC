using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [AddComponentMenu("")]
    internal sealed class ArsonistOilPatch : MonoBehaviour
    {
        private readonly Dictionary<PlayerCombatController, float> nextDamageAtByPlayer = new();
        private ArsonistBossAI owner;
        private float radius;
        private float expiresAt;
        private Color oilColor;
        private Color fireColor;
        private bool initialized;
        private bool ignited;

        public float Radius => radius;
        public bool IsIgnited => ignited;
        public bool CanIgnite => initialized && !ignited;
        public Color FireColor => fireColor;

        public void Initialize(ArsonistBossAI nextOwner, float nextRadius, float duration, Color nextOilColor)
        {
            owner = nextOwner;
            radius = Mathf.Max(0.05f, nextRadius);
            expiresAt = Time.time + Mathf.Max(0.05f, duration);
            oilColor = nextOilColor;
            fireColor = Color.white;
            initialized = true;
            ignited = false;
            ArsonistHazardVisual.ConfigureCircle(gameObject, radius, oilColor, false);
        }

        public void IgniteLocal(float duration, Color nextFireColor)
        {
            ignited = true;
            expiresAt = Time.time + Mathf.Max(0.05f, duration);
            fireColor = nextFireColor;
            ArsonistHazardVisual.ConfigureCircle(gameObject, radius, fireColor, true);
        }

        private void Update()
        {
            if (initialized && Time.time >= expiresAt)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            HandleContact(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            HandleContact(other);
        }

        private void OnDestroy()
        {
            owner?.UnregisterOilPatch(this);
        }

        private void HandleContact(Collider2D other)
        {
            if (owner == null || other == null)
            {
                return;
            }

            ArsonistFireArea fireArea = other.GetComponentInParent<ArsonistFireArea>();
            if (fireArea != null)
            {
                owner.IgniteOilNetwork(this, fireArea.FireColor);
                return;
            }

            PlayerCombatController player = other.GetComponentInParent<PlayerCombatController>();
            if (player == null)
            {
                return;
            }

            if (ignited)
            {
                owner.ApplyFireContact(player, transform.position, nextDamageAtByPlayer);
                return;
            }

            owner.ApplyOilSoaked(player, oilColor);
        }
    }
}
