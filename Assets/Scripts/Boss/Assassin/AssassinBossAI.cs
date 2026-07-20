using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Audio;

namespace Week14.Enemy
{
    public sealed partial class AssassinBossAI : GraphBossAI
    {
        protected override GameObject BossMuzzleFlashVfxPrefab => EffectData != null
            ? EffectData.AssassinMuzzleFlashVfxPrefab
            : null;

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
        [Tooltip("은신 상태에서 함께 반투명하게 만들 그림자 스프라이트입니다.")]
        [SerializeField] private SpriteRenderer stealthVisualTargetShadow;
        [SerializeField, Range(0, 255)] private int stealthAlphaShadow = 90;
        [Tooltip("은신 진입/해제 시 알파값이 목표치까지 부드럽게 도달하는 데 걸리는 시간(초)입니다.")]
        [SerializeField, Min(0.01f)] private float stealthAlphaFadeSeconds = 0.3f;
        [Tooltip("은신에 진입한 뒤 첫 은신 패턴이 시작되기까지 대기하는 시간(초)입니다.")]
        [SerializeField, Min(0f)] private float stealthEntryPatternDelaySeconds = 0.5f;
        [SerializeField, Min(1)] private int daggerCountForRecallPattern = 5;
        [Tooltip("패링 성공으로 단검이 보스 자신에게 회수될 때, 단검 1개가 도착 시 보스 자신에게 주는 데미지입니다.")]
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

        [Header("Assassin Walk")]
        [SerializeField, Min(0f)] private float walkVelocityThreshold = 0.01f;

        [Header("BGM")]
        [Tooltip("전투 시작 시 재생할 BGM의 SoundLibrary ID입니다. 비워두면 재생하지 않습니다.")]
        [BossGraphBgmId]
        [SerializeField] private string bgmId = "AssassinBgm";

        private const int CloneSpawnPositionAttempts = 8;
        private static readonly int IsWalkParameter = Animator.StringToHash("isWalk");

        private readonly List<AssassinDagger> spawnedDaggers = new();
        private readonly List<(Vector3 position, AssassinClone clone)> cloneShooterQueue = new();
        private readonly List<AssassinClone> activeClones = new();
        private bool isStealthed;
        private bool pendingStealthChange;
        private bool pendingStealthValue;
        private bool isRecallInProgress;
        private float stealthEntryDelayRemaining;
        private bool teleportVisibilityOverrideActive;
        private bool stealthVisibilityOverrideActive;
        private bool isHiddenFromMap;
        private Collider2D[] hiddenFromMapColliders;
        private bool[] hiddenFromMapColliderPreviousEnabled;
        private readonly AssassinFacingMirrorCache facingMirrorCacheA = new();
        private readonly AssassinFacingMirrorCache facingMirrorCacheB = new();
        private readonly AssassinFacingMirrorCache facingMirrorCacheShadow = new();
        private Animator[] walkAnimators;
        private bool hasAppliedWalkState;
        private bool lastIsWalking;

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

        // 은신 중 단검이 회수 패턴 발동 개수 이상 쌓이면, 다음 패턴은 무조건 페이즈의
        // Forced Pattern Id(=회수 패턴)를 쓰도록 강제한다.
        protected override bool ShouldUseForcedGraphPattern()
        {
            return isStealthed && HasEnoughDaggersForRecallPattern;
        }

        internal bool IsStealthed => isStealthed;
        internal bool HasEnoughDaggersForRecallPattern => spawnedDaggers.Count >= daggerCountForRecallPattern;

        // AssassinTeleportAroundPlayerAction의 hiddenSeconds 구간처럼 "맵에서 완전히 사라진" 상태일 때
        // false가 되어, 락온/피격 판정에서 이 보스를 완전히 제외시키는 데 쓰인다.
        internal bool IsPlayerTargetable => !isHiddenFromMap;

        protected override void OnCombatStarted()
        {
            if (!string.IsNullOrWhiteSpace(bgmId))
            {
                SoundManager.PlayBgm(bgmId);
            }
        }

        // 페이즈가 넘어가면(처형 성공으로 목숨 소모) 은신 중이었더라도 강제로 해제하고, 바닥에 남아있던
        // 단검과 아직 발사 대기열에 남아있는 분신도 전부 정리한다 — 다음 페이즈를 은신 상태/단검/분신이
        // 남아있는 채로 시작하지 않도록 하기 위함. OnBossDied/OnDisable이 하는 정리와 동일한 묶음이다.
        protected override void OnBossPhaseChanged(int phaseIndex, int phaseNumber)
        {
            RequestStealth(false);
            ClearAssassinDaggers();
            ClearActiveClones();
            base.OnBossPhaseChanged(phaseIndex, phaseNumber);
        }

        // 체력이 바닥나 처형 판정 구간(HP Empty)에 들어가는 순간 호출된다. 페이즈 전환 때 쓰는
        // RequestStealth와 달리 다음 LateUpdate까지 미루지 않고 그 자리에서 즉시 은신을 풀고 알파도
        // 바로 완전히 보이게 만든다 — 실행 판정 구간에서는 페이드 연출 없이 즉시 노출돼야 하기 때문.
        protected override void OnHpEmptyBegan()
        {
            ForceExitStealthImmediate();
            PlayGroggyStunVisual();
            base.OnHpEmptyBegan();
        }

        // 처형 실패(시간 초과)로 체력이 회복되어 처형 가능 상태를 벗어날 때, 그로기(스턴) 비주얼도
        // 함께 원상복구한다. 처형에 성공한 경우에는 OnBossPhaseChanged가 은신/단검/분신과 함께
        // 전체 정리를 담당하므로 이 훅이 따로 필요 없다.
        protected override void OnHpEmptyRecovered()
        {
            PlayGroggyEndStunVisual();
            base.OnHpEmptyRecovered();
        }

        // 그래프 코루틴 실행 도중이 아니라 보스 상태 전이 코드(BeginHpEmptyForState)에서 호출되므로,
        // StopGraphPattern을 바로 불러도 재진입 문제가 없다(은신 전환을 다음 프레임으로 미루는 이유는
        // 그래프 액션의 Execute() 코루틴 안에서 호출될 수 있는 RequestStealth에만 해당한다).
        private void ForceExitStealthImmediate()
        {
            pendingStealthChange = false;
            teleportVisibilityOverrideActive = false;
            stealthVisibilityOverrideActive = false;
            SetHiddenFromMap(false);

            if (!isStealthed)
            {
                return;
            }

            isStealthed = false;
            SnapStealthAlphaToVisible();
            StopGraphPattern(true);
        }

        private void SnapStealthAlphaToVisible()
        {
            SetAlphaImmediate(stealthVisualTargetA, 1f);
            SetAlphaImmediate(stealthVisualTargetB, 1f);
            SetAlphaImmediate(stealthVisualTargetShadow, 1f);
        }

        private static void SetAlphaImmediate(SpriteRenderer renderer, float alpha)
        {
            if (renderer == null)
            {
                return;
            }

            Color color = renderer.color;
            color.a = alpha;
            renderer.color = color;
        }

        protected override void OnBossDied()
        {
            ClearAssassinDaggers();
            ClearActiveClones();
            ApplyWalkState(false, true);
            SetHiddenFromMap(false);
            base.OnBossDied();
        }

        protected override void OnDisable()
        {
            ClearAssassinDaggers();
            ClearActiveClones();
            ApplyWalkState(false, true);
            SetHiddenFromMap(false);
            base.OnDisable();
        }

        private void LateUpdate()
        {
            UpdateFacingSprite();
            ApplyStealthAlpha();
            UpdateWalkState();

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
            if (isStealthed)
            {
                stealthEntryDelayRemaining = stealthEntryPatternDelaySeconds;
                // 새 은신 세션은 항상 반투명 상태로 시작한다 — 이전 세션에서 켜뒀던
                // 알파 노출(Reveal)은 여기서 초기화된다.
                stealthVisibilityOverrideActive = false;
            }

            // 그래프 액션 실행 도중(코루틴 안)에서 바로 StopGraphPattern을 부르면 자기 자신을
            // 끊는 재진입 문제가 생길 수 있어, 상태 전환은 항상 다음 LateUpdate로 미뤄서 처리한다.
            // 은신↔일반은 GraphAsset 자체가 stealthGraph/base.GraphAsset로 바뀌는 완전히 다른
            // BossGraphAsset 전환이라, 쿨다운/Min Patterns Played 히스토리를 그대로 넘기면 서로
            // 다른 그래프의 같은 페이즈 인덱스끼리 기록이 섞인다. 그래서 전체 초기화를 쓴다.
            StopGraphPattern(true);
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

        // 은신 상태(isStealthed)는 그대로 유지한 채, 시각적으로만 보스를 완전히 보이게(또는 다시
        // 반투명하게) 만든다. 그래프 패턴/쿨다운 등 게임플레이 로직에는 영향을 주지 않는다.
        internal void SetStealthVisibilityOverride(bool visible)
        {
            stealthVisibilityOverrideActive = visible;
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

            AssassinClone clone = Instantiate(clonePrefab, position, Quaternion.identity);
            activeClones.Add(clone);
            return clone;
        }

        // 분신 소환 전용 간격/플레이어 최소거리 설정(cloneMinSeparationDistance/cloneMinDistanceFromPlayer)을
        // 그대로 써서 count개의 서로 떨어진 지점을 뽑는다. 분신 소환(및 그 자리에 포함되는 보스 순간이동
        // 목적지)이 공통으로 쓰는 진입점.
        internal List<Vector2> GetSeparatedCloneZonePositions(int count)
        {
            return GetSeparatedRandomZonePositions(count, cloneMinSeparationDistance, cloneMinDistanceFromPlayer);
        }

        // 지정한 개수만큼, 서로 minSeparationDistance 이상 떨어지고(플레이어 최소거리 조건도 만족하는)
        // 지점들을 분신 스폰 구역 안에서 뽑는다. 구역 자체는 분신 소환과 공유하지만, 간격/플레이어
        // 최소거리는 호출하는 쪽이 원하는 값을 넘길 수 있다 — 분신과 다른 간격을 쓰고 싶은 액션(예: 폭탄
        // 스폰)이 같은 구역을 재사용하면서도 자기만의 거리 규칙을 쓸 수 있게 하기 위함이다.
        internal List<Vector2> GetSeparatedRandomZonePositions(int count, float minSeparationDistance, float minDistanceFromPlayer)
        {
            List<Vector2> positions = new(Mathf.Max(0, count));
            for (int i = 0; i < count; i++)
            {
                Vector2 candidate = GetRandomCloneSpawnPosition(minDistanceFromPlayer);
                for (int attempt = 0; attempt < CloneSpawnPositionAttempts && IsTooCloseToAny(candidate, positions, minSeparationDistance); attempt++)
                {
                    candidate = GetRandomCloneSpawnPosition(minDistanceFromPlayer);
                }

                positions.Add(candidate);
            }

            return positions;
        }

        private static bool IsTooCloseToAny(Vector2 candidate, List<Vector2> positions, float minSeparationDistance)
        {
            for (int i = 0; i < positions.Count; i++)
            {
                if (Vector2.Distance(candidate, positions[i]) < minSeparationDistance)
                {
                    return true;
                }
            }

            return false;
        }

        // 보스 스스로를 투명하게 만들었다가(분신과 똑같은 은신 알파 페이드 인프라 재사용) 그 사이에
        // 순간이동시킨다. 다시 나타나는 시점은 ReappearAfterTeleport로 분리해서, 호출하는 쪽이
        // "보스가 다시 나타나는 순간"에 맞춰 분신 등장 연출도 같이 시작할 수 있게 한다.
        internal IEnumerator VanishForTeleport(Vector3 destination)
        {
            yield return VanishForTeleport(destination, stealthAlphaFadeSeconds);
        }

        // fadeSeconds를 직접 지정하고 싶은 호출부(예: AssassinTeleportAroundPlayerAction)를 위한 오버로드.
        internal IEnumerator VanishForTeleport(Vector3 destination, float fadeSeconds)
        {
            yield return VanishForTeleportInPlace(fadeSeconds);
            TeleportImmediate(destination);
        }

        // 위치는 그대로 두고 투명해지기만 한다 — 순간이동 시점을 직접 제어하고 싶은 호출부
        // (예: hiddenSeconds가 다 지난 뒤에야 이동하고 싶은 AssassinTeleportAroundPlayerAction)를 위함.
        internal IEnumerator VanishForTeleportInPlace(float fadeSeconds)
        {
            teleportVisibilityOverrideActive = true;
            yield return WaitSecondsScaled(fadeSeconds);
        }

        internal void TeleportImmediate(Vector3 destination)
        {
            if (Body != null)
            {
                Body.position = destination;
            }

            transform.position = new Vector3(destination.x, destination.y, transform.position.z);
        }

        internal IEnumerator ReappearAfterTeleport()
        {
            yield return ReappearAfterTeleport(stealthAlphaFadeSeconds);
        }

        internal IEnumerator ReappearAfterTeleport(float fadeSeconds)
        {
            teleportVisibilityOverrideActive = false;
            yield return WaitSecondsScaled(fadeSeconds);
        }

        // 콜라이더를 전부 꺼서 총알/근접공격이 아예 맞지 않게 만든다(락온 제외는 IsPlayerTargetable로 처리).
        // 꺼두기 전 각 콜라이더의 enabled 상태를 저장해뒀다가 그대로 복원하므로, 다른 이유로 이미
        // 꺼져 있던 콜라이더를 실수로 켜버리는 일은 없다. 코루틴이 중간에 끊겨도(사망, 비활성화, 처형
        // 진입 등) 절대 콜라이더가 계속 꺼진 채로 남지 않도록 OnBossDied/OnDisable/
        // ForceExitStealthImmediate에서도 강제로 false 호출해 복원한다.
        internal void SetHiddenFromMap(bool hidden)
        {
            if (hidden == isHiddenFromMap)
            {
                return;
            }

            isHiddenFromMap = hidden;

            if (hidden)
            {
                hiddenFromMapColliders = GetComponentsInChildren<Collider2D>(true);
                hiddenFromMapColliderPreviousEnabled = new bool[hiddenFromMapColliders.Length];
                for (int i = 0; i < hiddenFromMapColliders.Length; i++)
                {
                    hiddenFromMapColliderPreviousEnabled[i] = hiddenFromMapColliders[i].enabled;
                    hiddenFromMapColliders[i].enabled = false;
                }

                return;
            }

            if (hiddenFromMapColliders == null)
            {
                return;
            }

            for (int i = 0; i < hiddenFromMapColliders.Length; i++)
            {
                if (hiddenFromMapColliders[i] != null)
                {
                    hiddenFromMapColliders[i].enabled = hiddenFromMapColliderPreviousEnabled[i];
                }
            }

            hiddenFromMapColliders = null;
            hiddenFromMapColliderPreviousEnabled = null;
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
            return GetRandomCloneSpawnPosition(cloneMinDistanceFromPlayer);
        }

        private Vector2 GetRandomCloneSpawnPosition(float minDistanceFromPlayer)
        {
            Vector2 candidate = SampleCloneSpawnZonePosition();
            if (Player == null || minDistanceFromPlayer <= 0f)
            {
                return candidate;
            }

            for (int i = 0; i < CloneSpawnPositionAttempts && Vector2.Distance(candidate, Player.position) < minDistanceFromPlayer; i++)
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

        // 플레이어를 중심으로 반지름 radius인 원 위의 무작위 지점을 고른다. cloneSpawnZone 밖으로는
        // 순간이동하면 안 되므로, 구역 안에 들어오는 지점이 나올 때까지 각도를 다시 뽑고, 그래도
        // 못 찾으면 분신 소환과 동일한 폴백(SampleCloneSpawnZonePosition)으로 구역 안 아무 지점을 쓴다.
        internal Vector2 GetRandomTeleportPositionAroundPlayer(float radius)
        {
            if (Player == null)
            {
                return SampleCloneSpawnZonePosition();
            }

            Vector2 playerPosition = Player.position;
            for (int i = 0; i < CloneSpawnPositionAttempts; i++)
            {
                Vector2 candidate = playerPosition + BossActionContext.AngleToDirection(Random.Range(0f, 360f)) * radius;
                if (cloneSpawnZone == null || cloneSpawnZone.OverlapPoint(candidate))
                {
                    return candidate;
                }
            }

            return SampleCloneSpawnZonePosition();
        }

        // 발사 대기열에 남아있는 분신뿐 아니라, 이미 발사돼 대기열을 벗어나 PlayDespawn으로 페이드
        // 아웃 중인 분신까지 포함해 지금 존재하는 분신을 전부 즉시 제거한다(activeClones가 소환 시점부터
        // 계속 추적하는 전체 목록). 대기열 자체도 같이 비워서 이후 TryDequeueCloneShooter가 파괴된
        // 분신을 다시 참조하지 않게 한다.
        internal void ClearActiveClones()
        {
            cloneShooterQueue.Clear();

            for (int i = activeClones.Count - 1; i >= 0; i--)
            {
                AssassinClone clone = activeClones[i];
                if (clone != null)
                {
                    Destroy(clone.gameObject);
                }
            }

            activeClones.Clear();
        }

        // ClearActiveClones와 정리 대상(대기열 + 발사돼 있는 분신 전부)은 동일하지만, 그 자리에서
        // 즉시 삭제하는 대신 보스 위치로 모여들며 서서히 사라지게 한다. 패링 성공으로 분신을 정리하는
        // 연출용(FireParrySuppressionBait 계열)이며, 그 외의 강제 정리(페이즈 전환/사망/비활성화)는
        // 여전히 즉시 제거하는 ClearActiveClones를 그대로 쓴다.
        internal void ClearActiveClonesGathering(float gatherSeconds)
        {
            cloneShooterQueue.Clear();

            Vector3 gatherTarget = BodyRoot != null ? BodyRoot.position : transform.position;
            for (int i = activeClones.Count - 1; i >= 0; i--)
            {
                AssassinClone clone = activeClones[i];
                if (clone != null)
                {
                    clone.PlayGatherDespawn(gatherTarget, gatherSeconds);
                }
            }

            activeClones.Clear();
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
        }

        private IEnumerator FlyDaggerHomeRoutine(AssassinDagger dagger, bool damagesPlayer, System.Action onComplete)
        {
            Transform target = BodyRoot != null ? BodyRoot : transform;
            // 패링 실패(플레이어를 노리는 정상 회수)일 때만 탄환처럼 궤적을 보여준다.
            // 패링 성공(보스 자해)일 때는 궤적을 표시하지 않는다.
            yield return dagger.FlyToBoss(target, daggerRecallSpeed, damagesPlayer ? daggerRecallPlayerDamage : 0, damagesPlayer);
            // dagger가 도중에(예: 페이즈 전환으로 ClearAssassinDaggers) 외부에서 파괴됐다면 도착한 게
            // 아니므로 자해 데미지를 주지 않는다.
            if (!damagesPlayer && dagger != null)
            {
                ReceivePlayerHit(daggerRecallDamagePerDagger, false, transform.position, Vector2.zero, Color.white);
            }

            onComplete?.Invoke();
        }

        // AssassinRecallDaggersWithParryBaitAction의 패링 실패 처리용. 보스에게로 돌아가는 대신,
        // 각 단검이 자기 위치 기준으로 (호출 시점) 플레이어 방향을 한 번만 계산해 그 방향으로 계속
        // 직진한다(유도 없음) — 발사 이후 플레이어가 움직여도 방향을 다시 잡지 않고, 특정 지점에서
        // 멈추는 게 아니라 flightSeconds가 지나면 그 자리에서 사라진다.
        internal IEnumerator RecallDaggersTowardPlayerRoutine(float flightSeconds)
        {
            if (isRecallInProgress)
            {
                yield break;
            }

            if (Player == null)
            {
                yield break;
            }

            isRecallInProgress = true;

            Vector2 playerPosition = Player.position;
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

                Vector2 direction = playerPosition - (Vector2)dagger.transform.position;
                StartCoroutine(FlyDaggerInDirectionRoutine(dagger, direction, flightSeconds, () => flyingCount--));
            }

            while (flyingCount > 0)
            {
                yield return null;
            }

            isRecallInProgress = false;
        }

        private IEnumerator FlyDaggerInDirectionRoutine(AssassinDagger dagger, Vector2 direction, float flightSeconds, System.Action onComplete)
        {
            yield return dagger.FlyInDirection(direction, daggerRecallSpeed, flightSeconds, daggerRecallPlayerDamage, true);
            onComplete?.Invoke();
        }

        private void UpdateFacingSprite()
        {
            if (Player == null || GraphContext?.IsFacingLocked == true)
            {
                return;
            }

            ApplyFacingSprites(Player.position.x > transform.position.x);
        }

        protected override void ApplyExecutionFacing(Vector2 worldPosition)
        {
            ApplyFacingSprites(worldPosition.x > transform.position.x);
        }

        private void ApplyFacingSprites(bool flip)
        {
            ApplyFacing(stealthVisualTargetA, flip, facingMirrorCacheA);
            ApplyFacing(stealthVisualTargetB, flip, facingMirrorCacheB);
            ApplyFacing(stealthVisualTargetShadow, flip, facingMirrorCacheShadow);
        }

        private static void ApplyFacing(SpriteRenderer renderer, bool flip, AssassinFacingMirrorCache mirrorCache)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.flipX = flip;
            mirrorCache.Apply(renderer.transform, flip);
        }

        private void ApplyStealthAlpha()
        {
            bool forceVisible = stealthVisibilityOverrideActive && !teleportVisibilityOverrideActive;
            float targetAlphaA = teleportVisibilityOverrideActive ? 0f : forceVisible || !isStealthed ? 1f : stealthAlphaA / 255f;
            float targetAlphaB = teleportVisibilityOverrideActive ? 0f : forceVisible || !isStealthed ? 1f : stealthAlphaB / 255f;
            float targetAlphaShadow = teleportVisibilityOverrideActive ? 0f : forceVisible || !isStealthed ? 1f : stealthAlphaShadow / 255f;
            FadeAlphaTowards(stealthVisualTargetA, targetAlphaA);
            FadeAlphaTowards(stealthVisualTargetB, targetAlphaB);
            FadeAlphaTowards(stealthVisualTargetShadow, targetAlphaShadow);
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

        // Body.linearVelocity는 SetMovementVelocity에서 이미 EnemyTimeScale이 곱해진 값이라,
        // 여기서 별도로 시간 배율을 다시 반영할 필요가 없다.
        private void UpdateWalkState()
        {
            if (Body == null)
            {
                ApplyWalkState(false, false);
                return;
            }

            float threshold = Mathf.Max(0f, walkVelocityThreshold);
            bool isWalking = Body.linearVelocity.sqrMagnitude > threshold * threshold;
            ApplyWalkState(isWalking, false);
        }

        private void ApplyWalkState(bool isWalking, bool force)
        {
            if (!force && hasAppliedWalkState && lastIsWalking == isWalking)
            {
                return;
            }

            Animator[] targets = GetWalkAnimators();
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null)
                {
                    targets[i].SetBool(IsWalkParameter, isWalking);
                }
            }

            lastIsWalking = isWalking;
            hasAppliedWalkState = true;
        }

        // 애니메이터가 두 개(boss-04-glow / boss-04-char)라 attack/groggy/death 트리거와 동일하게
        // BodyRoot 밑의 Animator 전부를 찾아 broadcast한다.
        private Animator[] GetWalkAnimators()
        {
            if (walkAnimators != null)
            {
                return walkAnimators;
            }

            walkAnimators = BodyRoot != null
                ? BodyRoot.GetComponentsInChildren<Animator>(true)
                : GetComponentsInChildren<Animator>(true);
            return walkAnimators;
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
