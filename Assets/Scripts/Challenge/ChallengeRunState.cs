using Week14.Combat;

namespace Week14.Challenge
{
    public abstract class ChallengeRunState
    {
        public ChallengeState State { get; protected set; } = ChallengeState.Active;

        public virtual void OnTick(float elapsedSeconds) { }
        public virtual void OnPlayerHit(int totalHitsThisRun) { }
        public virtual void OnPhaseReached(int phaseNumber) { }
        public virtual void OnParried(EnemyProjectile projectile) { }
        // ChallengeManager가 EnemyProjectile.AnyDestroyed 중 플레이어가 파괴한 경우(reason == Intercepted)만 걸러서 호출합니다.
        public virtual void OnObjectDestroyed(EnemyProjectile projectile) { }
        // 패링 유도 미끼(ParryBaitRewardProjectile)가 패링당하지 못하고 사라졌을 때 호출됩니다.
        public virtual void OnParryFailed(ParryBaitRewardProjectile bait) { }

        public abstract bool TryFinalize(bool victory, string challengeId);
    }
}
