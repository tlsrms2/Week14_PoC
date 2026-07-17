using UnityEngine;

namespace Week14.Combat
{
    [DisallowMultipleComponent]
    internal sealed class VfxTransformFollower : MonoBehaviour
    {
        private Transform followTarget;
        private Vector3 targetLocalPosition;
        private Quaternion targetLocalRotation;
        private Quaternion fixedWorldRotation;
        private bool followRotation;

        internal void Initialize(Transform target, bool shouldFollowRotation)
        {
            followTarget = target;
            followRotation = shouldFollowRotation;
            targetLocalPosition = target.InverseTransformPoint(transform.position);
            targetLocalRotation = Quaternion.Inverse(target.rotation) * transform.rotation;
            fixedWorldRotation = transform.rotation;
        }

        private void LateUpdate()
        {
            if (followTarget == null)
            {
                enabled = false;
                return;
            }

            transform.position = followTarget.TransformPoint(targetLocalPosition);
            transform.rotation = followRotation
                ? followTarget.rotation * targetLocalRotation
                : fixedWorldRotation;
        }
    }
}
