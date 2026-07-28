using System.Collections;
using UnityEngine;
using Week14.Enemy;

namespace Week14.Combat
{
    public abstract class BossExecutionSequence : MonoBehaviour
    {
        public abstract bool CanPlay { get; }
        public abstract Transform InitialCameraFocus { get; }
        public abstract float WideCameraFocusWeight { get; }
        public abstract float WideCameraZoomMultiplier { get; }
        public abstract float WideCameraBlendSmoothTime { get; }
        public virtual float FinalBlackoutTimeMultiplier => 1f;
        public virtual bool UseParryColorForFinalShotLine => false;
        public abstract float ExpectedDurationSeconds { get; }

        public abstract bool SupportsBoss(BossAI boss);

        internal abstract bool Prepare(
            PlayerCombatController player,
            BossAI boss);

        internal abstract IEnumerator PlayPrelude();
        internal virtual void OnFinalBlackoutStarted(BossAI boss) { }
        internal virtual void OnFinalBlackoutFadeOutStarted(
            BossAI boss,
            float durationSeconds) { }
        internal virtual void OnFinalShotImpact(BossAI boss) { }
        internal virtual void OnFinalBlackoutEnded(BossAI boss) { }
        internal virtual void OnFinalDeathSequenceComplete(BossAI boss) { }
        internal abstract void Cancel();
    }
}
