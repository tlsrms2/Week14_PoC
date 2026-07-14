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
        [SerializeField, Range(0.05f, 1f)] private float stealthAlpha = 0.35f;
        [SerializeField, Min(1)] private int daggerCountForRecallPattern = 5;
        [SerializeField, Min(1)] private int stealthDamageThresholdForForceRecall = 20;
        [Tooltip("피해 임계치로 강제 회수될 때, 단검 1개가 도착 시 보스 자신에게 주는 데미지입니다.")]
        [SerializeField, Min(1)] private int daggerRecallDamagePerDagger = 3;
        [Tooltip("회수 패턴을 직접 사용했을 때, 비행 중 단검이 플레이어에게 주는 데미지입니다.")]
        [SerializeField, Min(1)] private int daggerRecallPlayerDamage = 5;
        [SerializeField, Min(0.1f)] private float daggerRecallSpeed = 12f;

        private readonly List<AssassinDagger> spawnedDaggers = new();
        private bool isStealthed;
        private bool pendingStealthChange;
        private bool pendingStealthValue;
        private bool isRecallInProgress;
        private int stealthDamageAccumulated;

        protected override bool RotatesBodyToPlayer => false;
        protected override BossGraphAsset GraphAsset => isStealthed ? stealthGraph : base.GraphAsset;

        internal bool IsStealthed => isStealthed;
        internal bool HasEnoughDaggersForRecallPattern => spawnedDaggers.Count >= daggerCountForRecallPattern;

        protected override void OnCombatStarted()
        {
            SoundManager.PlayBgm("AssassinBgm");
        }

        protected override void OnBossDied()
        {
            ClearAssassinDaggers();
            base.OnBossDied();
        }

        protected override void OnDisable()
        {
            ClearAssassinDaggers();
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
            ApplyStealthAlpha();

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

        private void ApplyStealthAlpha()
        {
            if (!isStealthed)
            {
                return;
            }

            SpriteRenderer[] bodyRenderers = RenderersForSequence;
            if (bodyRenderers == null)
            {
                return;
            }

            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                SpriteRenderer renderer = bodyRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Color color = renderer.color;
                color.a = stealthAlpha;
                renderer.color = color;
            }
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
    }
}
