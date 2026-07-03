using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [AddComponentMenu("")]
    internal sealed class ArsonistFireArea : MonoBehaviour
    {
        private readonly Dictionary<PlayerCombatController, float> nextDamageAtByPlayer = new();
        private Arsonist owner;
        private float radius;
        private float expiresAt;
        private bool initialized;

        public void Initialize(Arsonist nextOwner, float nextRadius, float duration, Color fireColor)
        {
            owner = nextOwner;
            radius = Mathf.Max(0.05f, nextRadius);
            expiresAt = Time.time + Mathf.Max(0.05f, duration);
            initialized = true;
            ArsonistHazardVisual.ConfigureCircle(gameObject, radius, fireColor, 10);
            owner?.TryIgniteOilAt(transform.position, radius);
        }

        public bool CanIgniteOilAt(Vector3 position, float oilRadius)
        {
            float maxDistance = radius + Mathf.Max(0f, oilRadius);
            return Vector2.SqrMagnitude((Vector2)transform.position - (Vector2)position) <= maxDistance * maxDistance;
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
            owner?.UnregisterFireArea(this);
        }

        private void HandleContact(Collider2D other)
        {
            if (owner == null || other == null)
            {
                return;
            }

            ArsonistOilPatch oilPatch = other.GetComponentInParent<ArsonistOilPatch>();
            if (oilPatch != null)
            {
                owner.IgniteOilNetwork(oilPatch);
                return;
            }

            PlayerCombatController player = other.GetComponentInParent<PlayerCombatController>();
            if (player != null)
            {
                owner.ApplyFireContact(player, transform.position, nextDamageAtByPlayer);
            }
        }
    }
}
