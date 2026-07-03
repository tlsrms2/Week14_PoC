using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [AddComponentMenu("")]
    internal sealed class ArsonistOilPatch : MonoBehaviour
    {
        private readonly Dictionary<PlayerCombatController, float> nextDamageAtByPlayer = new();
        private Arsonist owner;
        private float radius;
        private float expiresAt;
        private bool initialized;
        private bool ignited;

        public float Radius => radius;
        public bool IsIgnited => ignited;
        public bool CanIgnite => initialized && !ignited;

        public void Initialize(Arsonist nextOwner, float nextRadius, float duration, Color oilColor, Color fireColor)
        {
            owner = nextOwner;
            radius = Mathf.Max(0.05f, nextRadius);
            expiresAt = Time.time + Mathf.Max(0.05f, duration);
            initialized = true;
            ignited = false;
            ArsonistHazardVisual.ConfigureCircle(gameObject, radius, oilColor, 7);
        }

        public void IgniteLocal(float duration, Color fireColor)
        {
            ignited = true;
            expiresAt = Time.time + Mathf.Max(0.05f, duration);
            ArsonistHazardVisual.ConfigureCircle(gameObject, radius, fireColor, 9);
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

            if (other.GetComponentInParent<ArsonistFireArea>() != null)
            {
                owner.IgniteOilNetwork(this);
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

            owner.ApplyOilSoaked(player);
        }
    }
}
