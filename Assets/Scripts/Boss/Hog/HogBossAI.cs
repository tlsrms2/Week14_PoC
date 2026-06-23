using Action = System.Action;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Week14.Audio;
using Week14.Bootstrap;
using Week14.Combat;
using Week14.Save;
using Week14.UI;

namespace Week14.Enemy
{
    public sealed partial class HogBossAI : BossAI
    {
        [Header("Hog Patterns")]
        [SerializeField, Tooltip("플레이어 방향 기준 사방 탄환을 발사하는 패턴 설정입니다.")] private Pattern1Settings pattern1 = new();
        [SerializeField, Tooltip("느려진 상태로 플레이어를 향해 머신건처럼 발사하는 패턴 설정입니다.")] private Pattern2Settings pattern2 = new();
        [SerializeField, Tooltip("벽에 부딪히면 분열하는 거대 특수 탄환 패턴 설정입니다.")] private Pattern3Settings pattern3 = new();
        [SerializeField, Tooltip("360도 전방위 탄환을 발사하는 패턴 설정입니다.")] private Pattern4Settings pattern4 = new();
        [SerializeField, Tooltip("제자리에서 기를 모은 뒤 미니건처럼 다수의 탄환을 발사하는 패턴 설정입니다.")] private Pattern5Settings pattern5 = new();
        [SerializeField, Tooltip("패턴4와 동일한 360도 전방위 탄환 패턴 설정입니다. 인스펙터 수치를 다르게 조절해 변형 패턴으로 사용합니다.")] private Pattern4Settings pattern6 = new();
        [SerializeField, Tooltip("발사 직전 플레이어 방향으로 고정한 뒤 전방 세 갈래 탄환을 발사하는 패턴 설정입니다.")] private Pattern7Settings pattern7 = new();
        [Header("Hog Projectiles")]
        [SerializeField, Tooltip("패턴에서 선택해 사용할 공용 투사체 목록입니다. 인스펙터에서는 A, B, C 순서로 표시됩니다.")]
        private List<ProjectileSettings> projectiles = new()
        {
            new ProjectileSettings(),
            new ProjectileSettings()
        };
        [SerializeField, Tooltip("페이즈별로 포함할 패턴 목록입니다. 1번 요소가 페이즈 1, 2번 요소가 페이즈 2입니다.")]
        private List<PhasePatternSet> phasePatterns = new()
        {
            new PhasePatternSet { Phase = 1 },
            new PhasePatternSet { Phase = 2 },
            new PhasePatternSet { Phase = 3 }
        };
        [SerializeField, FormerlySerializedAs("patternRecoverySeconds"), Min(0f), Tooltip("패턴 하나가 끝난 뒤 다음 패턴 전까지 쉬는 최소 시간입니다.")] private float minPatternRecoverySeconds = 0.5f;
        [SerializeField, Min(0f), Tooltip("패턴 하나가 끝난 뒤 다음 패턴 전까지 쉬는 최대 시간입니다.")] private float maxPatternRecoverySeconds = 0.9f;
        [Header("Pattern Preview UI")]
        [SerializeField, Tooltip("켜면 다음 패턴의 발사 탄환 수를 대기 시간 동안 LineRenderer 총알 줄로 표시합니다.")] private bool showPatternBulletPreview = true;
        [SerializeField, Min(0f), Tooltip("총알이 모두 찬 뒤 패턴 시작 전 유지할 시간입니다.")] private float patternBulletPreviewFullHoldSeconds = 0.18f;
        [SerializeField, Range(0f, 0.95f), Tooltip("한 번에 모든 총알을 쓰는 패턴은 대기 시간 중 이 비율만큼을 다 찬 상태로 유지합니다.")]
        private float patternBulletPreviewSingleGroupFullHoldRatio = 0.55f;
        [SerializeField, Tooltip("켜면 패턴을 순서대로 쓰지 않고 무작위로 선택합니다.")] private bool randomizePatterns;
        [SerializeField, Tooltip("랜덤 패턴 선택 시 직전에 실행한 패턴을 다시 고르지 않습니다. 후보가 1개면 같은 패턴을 허용합니다.")] private bool preventRandomRepeatPattern = true;

        [Header("Debug")]
        [SerializeField, Tooltip("켜면 아래에서 고른 패턴만 반복 실행합니다.")] private bool debugUseFixedPattern;
        [SerializeField, Tooltip("디버그용으로 고정 실행할 패턴입니다.")] private PatternKind debugPattern = PatternKind.Pattern1;

        private Coroutine patternRoutine;
        private readonly HogPatternSelector patternSelector = new();
        private readonly PatternPreviewPresenter patternPreviewPresenter = new();
        private readonly Pattern7GuideView pattern7GuideView = new();
        private readonly BodyRootSlamController bodyRootSlamController = new();
        private readonly BossPatternRecovery patternRecovery = new();
        private readonly BossPatternMovement patternMovement = new();
        private readonly List<int> patternBulletPreviewGroups = new();

        protected override bool RotatesBodyToPlayer => false;

        private void OnValidate()
        {
            EnsureProjectiles();
            patternSelector.EnsurePhasePatterns(ref phasePatterns, MaxLives);
            pattern3?.EnsureMuzzleFlashDefault();
            pattern5?.EnsureBulletShakeDefault();
        }

        protected override void OnBossStarted()
        {
            DeactivatePatternFirePoints();
            HidePatternBulletPreview();
            HidePattern7GuideLines();
        }

        protected override void OnBossDied()
        {
            DeactivatePatternFirePoints();
            HidePatternBulletPreview();
            HidePattern7GuideLines();
            BossProgressManager.ClearBoss("1");
            BossProgressManager.UnlockBoss("2");
        }

        protected override void OnCombatStarted()
        {
            SoundManager.PlayBgm("HogBgm");
        }

        protected override void OnBossTick()
        {
            if (patternRoutine != null || !IsPlayerDetected())
            {
                return;
            }

            patternRoutine = StartCoroutine(RunPatternLoop());
        }

        protected override void CancelBossAction()
        {
            if (patternRoutine != null)
            {
                StopCoroutine(patternRoutine);
                patternRoutine = null;
            }

            ResetPattern4BodyRoot();
            DeactivatePatternFirePoints();
            HidePatternBulletPreview();
            HidePattern7GuideLines();
        }

        private IEnumerator RunPattern1()
        {
            yield return HogPattern1Runner.Run(pattern1, CreatePatternContext());
        }

        private IEnumerator RunPattern2()
        {
            yield return HogPattern2Runner.Run(pattern2, CreatePatternContext());
        }

        private IEnumerator RunPattern3()
        {
            yield return HogPattern3Runner.Run(pattern3, CreatePatternContext());
        }

        private IEnumerator RunPattern4()
        {
            yield return RunPattern4Like(pattern4, PatternKind.Pattern4);
        }

        private IEnumerator RunPattern6()
        {
            yield return RunPattern4Like(pattern6, PatternKind.Pattern6);
        }

        private IEnumerator RunPattern4Like(Pattern4Settings settings, PatternKind patternKind)
        {
            yield return HogPattern4Runner.Run(
                settings,
                patternKind,
                CreatePatternContext());
        }

        private IEnumerator RunPattern5()
        {
            yield return HogPattern5Runner.Run(
                pattern5,
                CreatePatternContext());
        }

        private IEnumerator RunPattern7()
        {
            yield return HogPattern7Runner.Run(
                pattern7,
                CreatePatternContext());
        }

        private HogPatternContext CreatePatternContext()
        {
            return new HogPatternContext(
                Stop,
                IsBossExecutionPaused,
                WaitWhileExecutionPaused,
                WaitPatternSeconds,
                GetProjectile,
                MoveTowardPlayer,
                FirePattern4Wave,
                FireMachinegunBullet,
                FirePattern5Bullet,
                FirePattern7NormalVolley,
                FirePattern7SecondaryProjectiles,
                SlamPattern4BodyRoot,
                RecoverPattern4BodyRoot,
                ReloadPatternWavePreview,
                ResetPattern4BodyRoot,
                AdvancePatternBulletPreviewGroup,
                SetFirePointActive,
                RotateFirePointToPlayer,
                RotateFirePoint,
                GetFirePointProjectilePosition,
                GetFirePointProjectileTransform,
                GetDirectionToPlayer,
                AngleToDirection,
                GetPattern1SpawnPosition,
                GetPattern3Direction,
                GetPattern7NormalProjectilePosition,
                UpdatePattern7GuideLines,
                HidePattern7GuideLines,
                FireConfiguredProjectileWithoutPlayerAim,
                FireConfiguredProjectileWithPlayerLaunchAim,
                (HogPatternContext.ProjectileFire)FireConfiguredProjectile,
                (HogPatternContext.ConfiguredProjectileFire)FireConfiguredProjectile);
        }

        protected override bool ShouldIgnoreBodyStateRenderer(SpriteRenderer renderer)
        {
            return renderer != null
                && (pattern3.FirePoint.Contains(renderer.transform, BodyRoot)
                    || pattern5.FirePoint.Contains(renderer.transform, BodyRoot)
                    || pattern7.FirePoint.Contains(renderer.transform, BodyRoot));
        }

    }
}
