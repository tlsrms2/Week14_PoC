using Action = System.Action;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    public sealed partial class HogBossAI
    {
        private IEnumerator SlamPattern4BodyRoot(Pattern4Settings settings)
        {
            yield return bodyRootSlamController.Slam(BodyRoot, transform, settings, IsBossExecutionPaused, Stop);
        }

        private IEnumerator RecoverPattern4BodyRoot(Pattern4Settings settings)
        {
            yield return bodyRootSlamController.Recover(BodyRoot, transform, settings, IsBossExecutionPaused, Stop);
        }

        private void ResetPattern4BodyRoot()
        {
            bodyRootSlamController.Reset(BodyRoot);
        }

        private sealed class BodyRootSlamController
        {
            private bool isMoved;
            private Vector3 baseLocalPosition;

            public IEnumerator Slam(
                Transform target,
                Transform ownerTransform,
                Pattern4Settings settings,
                System.Func<bool> isExecutionPaused,
                Action stop)
            {
                if (target == null || target == ownerTransform || settings == null)
                {
                    yield break;
                }

                if (!isMoved)
                {
                    baseLocalPosition = target.localPosition;
                    isMoved = true;
                }

                Vector3 upPosition = baseLocalPosition + Vector3.up * settings.SlamUpOffset;
                Vector3 downPosition = baseLocalPosition + Vector3.down * settings.SlamDownOffset;
                yield return Move(target, target.localPosition, upPosition, settings.SlamRiseSeconds, isExecutionPaused, stop);
                yield return Move(target, target.localPosition, downPosition, settings.SlamDropSeconds, isExecutionPaused, stop);
                PlayExplosionIfEnabled(settings.Effects, target.position);
                PlayCameraShakeIfEnabled(settings.Effects, Vector2.down);
            }

            public IEnumerator Recover(
                Transform target,
                Transform ownerTransform,
                Pattern4Settings settings,
                System.Func<bool> isExecutionPaused,
                Action stop)
            {
                if (!isMoved || target == null || target == ownerTransform || settings == null)
                {
                    yield break;
                }

                yield return Move(target, target.localPosition, baseLocalPosition, settings.SlamRecoverSeconds, isExecutionPaused, stop);
                Reset(target);
            }

            public void Reset(Transform target)
            {
                if (!isMoved)
                {
                    return;
                }

                if (target != null)
                {
                    target.localPosition = baseLocalPosition;
                }

                isMoved = false;
            }

            private static IEnumerator Move(Transform target, Vector3 from, Vector3 to, float seconds, System.Func<bool> isExecutionPaused, Action stop)
            {
                float duration = Mathf.Max(0.01f, seconds);
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    if (target == null)
                    {
                        yield break;
                    }

                    if (isExecutionPaused?.Invoke() == true)
                    {
                        stop?.Invoke();
                        yield return null;
                        continue;
                    }

                    stop?.Invoke();
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    target.localPosition = Vector3.Lerp(from, to, t);
                    yield return null;
                }

                if (target != null)
                {
                    target.localPosition = to;
                }
            }
        }
    }
}
