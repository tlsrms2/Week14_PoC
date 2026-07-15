using UnityEngine;

namespace Week14.Combat
{
    public partial class EnemyProjectile
    {
        private void ResolveParryLockOnIndicator()
        {
            if (parryLockOnReticle == null && parryLockOnIndicatorRoot != null)
            {
                parryLockOnReticle = parryLockOnIndicatorRoot.GetComponentInChildren<MouseParryReticle>(true);
            }

            if (parryLockOnReticle == null)
            {
                parryLockOnReticle = GetComponentInChildren<MouseParryReticle>(true);
            }

            if (parryLockOnIndicatorRoot != null)
            {
                return;
            }

            Transform authoredRoot = transform.Find(ParryLockOnIndicatorName);
            if (authoredRoot == null)
            {
                authoredRoot = transform.Find(LegacyParryLockOnIndicatorName);
            }

            if (authoredRoot != null)
            {
                parryLockOnIndicatorRoot = authoredRoot.gameObject;
                return;
            }

            if (parryLockOnReticle != null && parryLockOnReticle.gameObject != gameObject)
            {
                parryLockOnIndicatorRoot = parryLockOnReticle.gameObject;
            }
        }

        private void TickParryLockOnIndicator()
        {
            if (!parryLockOnIndicatorVisible || Mathf.Approximately(parryLockOnRotationSpeedDegrees, 0f))
            {
                return;
            }

            Transform targetRoot = GetParryLockOnRotationRoot();
            if (targetRoot != null)
            {
                targetRoot.Rotate(0f, 0f, parryLockOnRotationSpeedDegrees * Time.deltaTime, Space.Self);
            }
        }

        private Transform GetParryLockOnRotationRoot()
        {
            if (parryLockOnRotatingRoot != null)
            {
                return parryLockOnRotatingRoot;
            }

            if (parryLockOnIndicatorRoot != null && parryLockOnIndicatorRoot != gameObject)
            {
                return parryLockOnIndicatorRoot.transform;
            }

            return parryLockOnReticle != null && parryLockOnReticle.gameObject != gameObject
                ? parryLockOnReticle.transform
                : null;
        }

        protected bool IsParryLockOnIndicatorRenderer(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return false;
            }

            ResolveParryLockOnIndicator();
            if (parryLockOnIndicatorRoot != null
                && parryLockOnIndicatorRoot != gameObject
                && renderer.transform.IsChildOf(parryLockOnIndicatorRoot.transform))
            {
                return true;
            }

            return parryLockOnReticle != null
                && parryLockOnReticle.gameObject != gameObject
                && renderer.transform.IsChildOf(parryLockOnReticle.transform);
        }

    }
}
