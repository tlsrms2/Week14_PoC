using System.Collections;
using UnityEngine;

namespace Week14.Combat
{
    public sealed class GunRecoilMotion : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 localOffset = new(-0.08f, 0f, 0f);
        [SerializeField, Min(0.01f)] private float kickSeconds = 0.035f;
        [SerializeField, Min(0.01f)] private float returnSeconds = 0.09f;
        [SerializeField] private bool useShotDirection;
        [SerializeField, Min(0f)] private float shotDirectionDistance = 0.08f;
        [SerializeField] private AnimationCurve kickCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve returnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private Coroutine routine;
        private Vector3 baseLocalPosition;
        private bool hasBaseLocalPosition;

        private Transform Target => target != null ? target : transform;

        private void OnDisable()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            Transform activeTarget = Target;
            if (hasBaseLocalPosition && activeTarget != null)
            {
                activeTarget.localPosition = baseLocalPosition;
            }
        }

        public void Play()
        {
            Play(Vector2.zero);
        }

        public void Play(Vector2 shotDirection)
        {
            Transform activeTarget = Target;
            if (activeTarget == null)
            {
                return;
            }

            if (!hasBaseLocalPosition)
            {
                baseLocalPosition = activeTarget.localPosition;
                hasBaseLocalPosition = true;
            }

            if (routine != null)
            {
                StopCoroutine(routine);
                activeTarget.localPosition = baseLocalPosition;
            }

            Vector3 offset = ResolveOffset(activeTarget, shotDirection);
            routine = StartCoroutine(PlayRoutine(activeTarget, offset));
        }

        public void PlayKick(Vector2 shotDirection, float seconds)
        {
            Transform activeTarget = Target;
            if (activeTarget == null)
            {
                return;
            }

            if (!hasBaseLocalPosition)
            {
                baseLocalPosition = activeTarget.localPosition;
                hasBaseLocalPosition = true;
            }

            if (routine != null)
            {
                StopCoroutine(routine);
                activeTarget.localPosition = baseLocalPosition;
            }

            Vector3 offset = ResolveOffset(activeTarget, shotDirection);
            routine = StartCoroutine(PlayKickRoutine(activeTarget, offset, seconds));
        }

        public void ReturnToBase(float seconds)
        {
            Transform activeTarget = Target;
            if (activeTarget == null || !hasBaseLocalPosition)
            {
                return;
            }

            if (routine != null)
            {
                StopCoroutine(routine);
            }

            routine = StartCoroutine(ReturnToBaseRoutine(activeTarget, seconds));
        }

        private Vector3 ResolveOffset(Transform activeTarget, Vector2 shotDirection)
        {
            if (!useShotDirection || shotDirection.sqrMagnitude <= 0.0001f)
            {
                return localOffset;
            }

            Vector3 worldOffset = -(Vector3)shotDirection.normalized * shotDirectionDistance;
            Transform parent = activeTarget.parent;
            return parent != null ? parent.InverseTransformVector(worldOffset) : worldOffset;
        }

        private IEnumerator PlayRoutine(Transform activeTarget, Vector3 offset)
        {
            Vector3 kickedPosition = baseLocalPosition + offset;
            yield return Move(activeTarget, baseLocalPosition, kickedPosition, kickSeconds, kickCurve);
            yield return Move(activeTarget, kickedPosition, baseLocalPosition, returnSeconds, returnCurve);
            routine = null;
        }

        private IEnumerator PlayKickRoutine(Transform activeTarget, Vector3 offset, float seconds)
        {
            Vector3 kickedPosition = baseLocalPosition + offset;
            yield return Move(activeTarget, baseLocalPosition, kickedPosition, seconds, kickCurve);
            routine = null;
        }

        private IEnumerator ReturnToBaseRoutine(Transform activeTarget, float seconds)
        {
            yield return Move(activeTarget, activeTarget.localPosition, baseLocalPosition, seconds, returnCurve);
            routine = null;
        }

        private static IEnumerator Move(Transform target, Vector3 from, Vector3 to, float seconds, AnimationCurve curve)
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, seconds);
            while (elapsed < duration && target != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = curve != null ? curve.Evaluate(t) : t;
                target.localPosition = Vector3.LerpUnclamped(from, to, eased);
                yield return null;
            }

            if (target != null)
            {
                target.localPosition = to;
            }
        }
    }
}
