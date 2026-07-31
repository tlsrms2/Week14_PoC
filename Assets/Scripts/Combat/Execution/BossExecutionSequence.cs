using System.Collections;
using UnityEngine;
using Week14.Audio;
using Week14.Enemy;

namespace Week14.Combat
{
    public abstract class BossExecutionSequence : MonoBehaviour
    {
        private System.Action<float> finalChargeStarted;

        public abstract bool CanPlay { get; }
        public abstract Transform InitialCameraFocus { get; }
        public abstract float WideCameraFocusWeight { get; }
        public abstract float WideCameraZoomMultiplier { get; }
        public abstract float WideCameraBlendSmoothTime { get; }
        public virtual float FinalBlackoutTimeMultiplier => 1f;
        public virtual bool UseParryColorForFinalShotLine => false;
        public virtual bool BeginFinalShotSlowMotionBeforeImpact => false;
        public virtual float FinalShotLineWidthMultiplier => 1f;
        public virtual string FinalChargeSfxId => GameplaySfxIds.ExecuteCharge;
        public virtual float FinalChargeSfxLeadSeconds => 1.1f;
        public virtual bool PlayFinalChargeSfxOnChargeStart => false;
        public virtual float FinalChargeSfxDelayAfterChargeStartSeconds => 0f;
        public abstract float ExpectedDurationSeconds { get; }

        public abstract bool SupportsBoss(BossAI boss);

        internal abstract bool Prepare(
            PlayerCombatController player,
            BossAI boss);

        internal abstract IEnumerator PlayPrelude();
        internal void SetFinalChargeStartedHandler(
            System.Action<float> handler)
        {
            finalChargeStarted = handler;
        }

        protected void NotifyFinalChargeStarted(
            float remainingPreludeSeconds)
        {
            System.Action<float> handler = finalChargeStarted;
            finalChargeStarted = null;
            handler?.Invoke(Mathf.Max(0f, remainingPreludeSeconds));
        }

        internal virtual void OnFinalShotPreparationStarted(
            BossAI boss) { }
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
