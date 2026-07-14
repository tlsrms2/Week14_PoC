using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Audio;

namespace Week14.Enemy
{
    public sealed partial class AssassinBossAI : GraphBossAI
    {
        [Header("Assassin Stealth")]
        [Tooltip("은신 상태에서 사용할 Boss Graph입니다. 통상 상태에서는 GraphBossAI의 기본 Boss Graph를 그대로 사용합니다.")]
        [SerializeField] private BossGraphAsset stealthGraph;
        [Tooltip("은신 상태에서 스폰할 단검 프리팹입니다.")]
        [SerializeField] private AssassinDagger daggerPrefab;
        [Tooltip("은신 상태에서 반투명하게 만들 첫 번째 비주얼 대상입니다.")]
        [SerializeField] private SpriteRenderer stealthVisualTargetA;
        [SerializeField, Range(0, 255)] private int stealthAlphaA = 90;
        [Tooltip("은신 상태에서 반투명하게 만들 두 번째 비주얼 대상입니다.")]
        [SerializeField] private SpriteRenderer stealthVisualTargetB;
        [SerializeField, Range(0, 255)] private int stealthAlphaB = 90;
        [Tooltip("은신 진입/해제 시 알파값이 목표치까지 부드럽게 도달하는 데 걸리는 시간(초)입니다.")]
        [SerializeField, Min(0.01f)] private float stealthAlphaFadeSeconds = 0.3f;
        [Tooltip("은신에 진입한 뒤 첫 은신 패턴이 시작되기까지 대기하는 시간(초)입니다.")]
        [SerializeField, Min(0f)] private float stealthEntryPatternDelaySeconds = 0.5f;
        [SerializeField, Min(1)] private int daggerCountForRecallPattern = 5;
        [SerializeField, Min(1)] private int stealthDamageThresholdForForceRecall = 20;
        [Tooltip("피해 임계치로 강제 회수될 때, 단검 1개가 도착 시 보스 자신에게 주는 데미지입니다.")]
        [SerializeField, Min(1)] private int daggerRecallDamagePerDagger = 3;
        [Tooltip("회수 패턴을 직접 사용했을 때, 비행 중 단검이 플레이어에게 주는 데미지입니다.")]
        [SerializeField, Min(1)] private int daggerRecallPlayerDamage = 5;
        [SerializeField, Min(0.1f)] private float daggerRecallSpeed = 12f;

        [Header("Assassin Clone Volley")]
        [Tooltip("분신 소환에 쓸 프리팹입니다.")]
        [SerializeField] private AssassinClone clonePrefab;
        [Tooltip("분신이 랜덤 소환될 구역입니다. 이 콜라이더 범위 안(모양 무관)에서 랜덤 위치를 뽑습니다.")]
        [SerializeField] private Collider2D cloneSpawnZone;
        [Tooltip("두 분신 소환 지점 사이 최소 거리입니다. 서로 겹쳐 소환되는 것을 방지합니다.")]
        [SerializeField, Min(0f)] private float cloneMinSeparationDistance = 1.5f;
        [Tooltip("플레이어 주변 이 반경 안에는 분신이 소환되지 않습니다.")]
        [SerializeField, Min(0f)] private float cloneMinDistanceFromPlayer = 2f;

        private const int CloneSpawnPositionAttempts = 8;

        private readonly List<AssassinDagger> spawnedDaggers = new();
        private readonly List<(Vector3 position, AssassinClone clone)> cloneShooterQueue = new();
        private bool isStealthed;
        private bool pendingStealthChange;
        private bool pendingStealthValue;
        private bool isRecallInProgress;
        private int stealthDamageAccumulated;
        private float stealthEntryDelayRemaining;
        private bool teleportVisibilityOverrideActive;
        private readonly FacingMirrorCache facingMirrorCacheA = new();
        private readonly FacingMirrorCache facingMirrorCacheB = new();

        protected override bool RotatesBodyToPlayer => false;
        protected override BossGraphAsset GraphAsset => isStealthed ? stealthGraph : base.GraphAsset;

        protected override bool CanStartGraphPattern()
        {
            if (isStealthed && stealthEntryDelayRemaining > 0f)
            {
                return false;
            }

            return base.CanStartGraphPattern();
        }

        internal bool IsStealthed => isStealthed;
        internal bool HasEnoughDaggersForRecallPattern => spawnedDaggers.Count >= daggerCountForRecallPattern;

        protected override void OnCombatStarted()
        {
            SoundManager.PlayBgm("AssassinBgm");
        }

        protected override void OnBossDied()
        {
            ClearAssassinDaggers();
            ClearCloneShooterQueue();
            base.OnBossDied();
        }

        protected override void OnDisable()
        {
            ClearAssassinDaggers();
            ClearCloneShooterQueue();
            base.OnDisable();
        }

        protected override void OnPlayerHitAfterDamage(int bulletDamage, bool strongHit, Vector3 hitPosition, Vector2 hitDirection, Color hitColor)
        {
            if (!isStealthed || isRecallInProgress)
            {
                return;
            }

            stealthDamageAccumulated += bulletDamage;
            if (stealthDamageAccumulated < stealthDamageThresholdForForceRecall)
            {
                return;
            }

            stealthDamageAccumulated = 0;
            StartCoroutine(RecallAllDaggersRoutine(false));
        }

        private void LateUpdate()
        {
            UpdateFacingSprite();
            ApplyStealthAlpha();

            if (isStealthed && stealthEntryDelayRemaining > 0f)
            {
                stealthEntryDelayRemaining -= EnemyTimeScale.DeltaTime;
            }

            if (!pendingStealthChange)
            {
                return;
            }

            pendingStealthChange = false;
            isStealthed = pendingStealthValue;
            if (!isStealthed)
            {
                stealthDamageAccumulated = 0;
            }
            else
            {
                stealthEntryDelayRemaining = stealthEntryPatternDelaySeconds;
            }

            // 그래프 액션 실행 도중(코루틴 안)에서 바로 StopGraphPattern을 부르면 자기 자신을
            // 끊는 재진입 문제가 생길 수 있어, 상태 전환은 항상 다음 LateUpdate로 미뤄서 처리한다.
            StopGraphPattern();
        }

        internal void RequestStealth(bool enable)
        {
            if (enable == isStealthed)
            {
                return;
            }

            pendingStealthChange = true;
            pendingStealthValue = enable;
        }

        internal AssassinDagger CreateDagger(Vector3 position)
        {
            if (daggerPrefab == null)
            {
                return null;
            }

            AssassinDagger dagger = Instantiate(daggerPrefab, position, Quaternion.identity);
            dagger.Initialize(this, BodyRoot != null ? BodyRoot : transform);
            spawnedDaggers.Add(dagger);
            return dagger;
        }

        internal AssassinClone CreateClone(Vector3 position)
        {
            if (clonePrefab == null)
            {
                return null;
            }

            return Instantiate(clonePrefab, position, Quaternion.identity);
        }

        // 서로 cloneMinSeparationDistance 이상 떨어진 두 지점을 뽑는다(각 지점은 플레이어 최소거리 조건도 만족).
        internal (Vector2 pointA, Vector2 pointB) GetSeparatedCloneSpawnPositions()
        {
            Vector2 pointA = GetRandomCloneSpawnPosition();
            Vector2 pointB = GetRandomCloneSpawnPosition();
            for (int i = 0; i < CloneSpawnPositionAttempts && Vector2.Distance(pointA, pointB) < cloneMinSeparationDistance; i++)
            {
                pointB = GetRandomCloneSpawnPosition();
            }

            return (pointA, pointB);
        }

        // 두 지점(단검/분신 위치 등) 모두로부터 cloneMinSeparationDistance 이상 떨어진 지점을 하나 뽑는다.
        internal Vector2 GetSeparatedCloneSpawnPosition(Vector2 avoidPositionA, Vector2 avoidPositionB)
        {
            Vector2 candidate = GetRandomCloneSpawnPosition();
            for (int i = 0; i < CloneSpawnPositionAttempts
                && (Vector2.Distance(candidate, avoidPositionA) < cloneMinSeparationDistance
                    || Vector2.Distance(candidate, avoidPositionB) < cloneMinSeparationDistance); i++)
            {
                candidate = GetRandomCloneSpawnPosition();
            }

            return candidate;
        }

        // 보스 스스로를 투명하게 만들었다가(분신과 똑같은 은신 알파 페이드 인프라 재사용) 그 사이에
        // 순간이동시킨다. 다시 나타나는 시점은 ReappearAfterTeleport로 분리해서, 호출하는 쪽이
        // "보스가 다시 나타나는 순간"에 맞춰 분신 등장 연출도 같이 시작할 수 있게 한다.
        internal IEnumerator VanishForTeleport(Vector3 destination)
        {
            teleportVisibilityOverrideActive = true;
            yield return WaitSecondsScaled(stealthAlphaFadeSeconds);

            if (Body != null)
            {
                Body.position = destination;
            }

            transform.position = new Vector3(destination.x, destination.y, transform.position.z);
        }

        internal IEnumerator ReappearAfterTeleport()
        {
            teleportVisibilityOverrideActive = false;
            yield return WaitSecondsScaled(stealthAlphaFadeSeconds);
        }

        private static IEnumerator WaitSecondsScaled(float seconds)
        {
            float remaining = seconds;
            while (remaining > 0f)
            {
                remaining -= EnemyTimeScale.DeltaTime;
                yield return null;
            }
        }

        internal void EnqueueCloneShooter(Vector3 position, AssassinClone clone)
        {
            cloneShooterQueue.Add((position, clone));
        }

        internal void ShuffleCloneShooters()
        {
            for (int i = cloneShooterQueue.Count - 1; i > 0; i--)
            {
                int swapIndex = Random.Range(0, i + 1);
                (cloneShooterQueue[i], cloneShooterQueue[swapIndex]) = (cloneShooterQueue[swapIndex], cloneShooterQueue[i]);
            }
        }

        internal bool TryDequeueCloneShooter(out Vector3 position, out AssassinClone clone)
        {
            if (cloneShooterQueue.Count == 0)
            {
                position = default;
                clone = null;
                return false;
            }

            (position, clone) = cloneShooterQueue[0];
            cloneShooterQueue.RemoveAt(0);
            return true;
        }

        private Vector2 GetRandomCloneSpawnPosition()
        {
            Vector2 candidate = SampleCloneSpawnZonePosition();
            if (Player == null || cloneMinDistanceFromPlayer <= 0f)
            {
                return candidate;
            }

            for (int i = 0; i < CloneSpawnPositionAttempts && Vector2.Distance(candidate, Player.position) < cloneMinDistanceFromPlayer; i++)
            {
                candidate = SampleCloneSpawnZonePosition();
            }

            return candidate;
        }

        // 콜라이더 모양(Box/Circle/Polygon 등 무관)에 상관없이 동작하도록, bounds 안에서
        // 후보를 뽑고 실제로 그 콜라이더 안에 들어오는지(OverlapPoint) 확인하는 리젝션 샘플링 방식.
        private Vector2 SampleCloneSpawnZonePosition()
        {
            if (cloneSpawnZone == null)
            {
                return transform.position;
            }

            Bounds bounds = cloneSpawnZone.bounds;
            for (int i = 0; i < CloneSpawnPositionAttempts; i++)
            {
                Vector2 candidate = new(
                    Random.Range(bounds.min.x, bounds.max.x),
                    Random.Range(bounds.min.y, bounds.max.y));
                if (cloneSpawnZone.OverlapPoint(candidate))
                {
                    return candidate;
                }
            }

            return bounds.center;
        }

        private void ClearCloneShooterQueue()
        {
            for (int i = 0; i < cloneShooterQueue.Count; i++)
            {
                AssassinClone clone = cloneShooterQueue[i].clone;
                if (clone != null)
                {
                    Destroy(clone.gameObject);
                }
            }

            cloneShooterQueue.Clear();
        }

        internal void UnregisterDagger(AssassinDagger dagger)
        {
            spawnedDaggers.Remove(dagger);
        }

        // damagesPlayer: 회수 패턴을 직접 써서 회수하는 경우(true, 플레이어를 노리는 공격) /
        // 은신 중 피해 임계치를 넘겨 강제로 회수되는 경우(false, 보스 자신에게 자해 데미지)를 구분한다.
        internal IEnumerator RecallAllDaggersRoutine(bool damagesPlayer)
        {
            if (isRecallInProgress)
            {
                yield break;
            }

            isRecallInProgress = true;

            List<AssassinDagger> daggersToRecall = new(spawnedDaggers);
            int flyingCount = daggersToRecall.Count;
            for (int i = 0; i < daggersToRecall.Count; i++)
            {
                AssassinDagger dagger = daggersToRecall[i];
                if (dagger == null)
                {
                    flyingCount--;
                    continue;
                }

                StartCoroutine(FlyDaggerHomeRoutine(dagger, damagesPlayer, () => flyingCount--));
            }

            while (flyingCount > 0)
            {
                yield return null;
            }

            isRecallInProgress = false;
            RequestStealth(false);
        }

        private IEnumerator FlyDaggerHomeRoutine(AssassinDagger dagger, bool damagesPlayer, System.Action onComplete)
        {
            Transform target = BodyRoot != null ? BodyRoot : transform;
            yield return dagger.FlyToBoss(target, daggerRecallSpeed, damagesPlayer ? daggerRecallPlayerDamage : 0);
            if (!damagesPlayer)
            {
                ReceivePlayerHit(daggerRecallDamagePerDagger, false, transform.position, Vector2.zero, Color.white);
            }

            onComplete?.Invoke();
        }

        private void UpdateFacingSprite()
        {
            if (Player == null || GraphContext?.IsFacingLocked == true)
            {
                return;
            }

            bool flip = Player.position.x > transform.position.x;
            ApplyFacing(stealthVisualTargetA, flip, facingMirrorCacheA);
            ApplyFacing(stealthVisualTargetB, flip, facingMirrorCacheB);
        }

        private static void ApplyFacing(SpriteRenderer renderer, bool flip, FacingMirrorCache mirrorCache)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.flipX = flip;
            mirrorCache.Apply(renderer.transform, flip);
        }

        private sealed class FacingMirrorCache
        {
            private Transform[] children;
            private Vector3[] baseLocalPositions;
            private bool cached;

            public void Apply(Transform root, bool flip)
            {
                if (root == null)
                {
                    return;
                }

                if (!cached)
                {
                    cached = true;
                    int childCount = root.childCount;
                    children = new Transform[childCount];
                    baseLocalPositions = new Vector3[childCount];
                    for (int i = 0; i < childCount; i++)
                    {
                        Transform child = root.GetChild(i);
                        children[i] = child;
                        baseLocalPositions[i] = child.localPosition;
                    }
                }

                for (int i = 0; i < children.Length; i++)
                {
                    Transform child = children[i];
                    if (child == null)
                    {
                        continue;
                    }

                    Vector3 basePosition = baseLocalPositions[i];
                    child.localPosition = new Vector3(flip ? -basePosition.x : basePosition.x, basePosition.y, basePosition.z);
                }
            }
        }

        private void ApplyStealthAlpha()
        {
            float targetAlphaA = teleportVisibilityOverrideActive ? 0f : isStealthed ? stealthAlphaA / 255f : 1f;
            float targetAlphaB = teleportVisibilityOverrideActive ? 0f : isStealthed ? stealthAlphaB / 255f : 1f;
            FadeAlphaTowards(stealthVisualTargetA, targetAlphaA);
            FadeAlphaTowards(stealthVisualTargetB, targetAlphaB);
        }

        private void FadeAlphaTowards(SpriteRenderer renderer, float targetAlpha)
        {
            if (renderer == null)
            {
                return;
            }

            Color color = renderer.color;
            float maxDelta = EnemyTimeScale.DeltaTime / stealthAlphaFadeSeconds;
            color.a = Mathf.MoveTowards(color.a, targetAlpha, maxDelta);
            renderer.color = color;
        }

        private void ClearAssassinDaggers()
        {
            for (int i = spawnedDaggers.Count - 1; i >= 0; i--)
            {
                if (spawnedDaggers[i] != null)
                {
                    Destroy(spawnedDaggers[i].gameObject);
                }
            }

            spawnedDaggers.Clear();
        }

        private void OnDrawGizmosSelected()
        {
            if (cloneSpawnZone == null)
            {
                return;
            }

            Bounds bounds = cloneSpawnZone.bounds;
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}
