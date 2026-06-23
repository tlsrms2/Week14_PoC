using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    public sealed class BossAnimationEventBridge : MonoBehaviour
    {
        private readonly Dictionary<string, int> eventVersions = new();

        public void RaiseAnimationEvent(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId))
            {
                return;
            }

            eventVersions.TryGetValue(eventId, out int version);
            eventVersions[eventId] = version + 1;
        }

        public void OnBossAnimationEvent(string eventId)
        {
            RaiseAnimationEvent(eventId);
        }

        public void RaiseAnimationEventFromEvent(AnimationEvent animationEvent)
        {
            RaiseAnimationEvent(animationEvent != null ? animationEvent.stringParameter : string.Empty);
        }

        public void OnBossAnimationEventFromEvent(AnimationEvent animationEvent)
        {
            RaiseAnimationEventFromEvent(animationEvent);
        }

        public int GetVersion(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId))
            {
                return 0;
            }

            return eventVersions.TryGetValue(eventId, out int version) ? version : 0;
        }
    }
}
