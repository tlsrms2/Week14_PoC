using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
using Week14.Audio;
using Week14.Bootstrap;
using Week14.Combat;
using Week14.Enemy;
using Week14.GameFlow;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Week14.UI
{
    public sealed class BossEncounterIntroController : MonoBehaviour
    {
        [Header("지역 정보")]
        [SerializeField] private string locationName = "지역명";
        [SerializeField] private TMP_Text locationNameText;
        [Tooltip("이 인트로 자체의 로컬라이징된 지역명입니다. 보스의 BossData에 로컬라이징된 지역명이 있으면 그게 우선 적용되고, " +
            "없으면(튜토리얼처럼 보스가 아예 없는 경우 포함) 이 값을 씁니다. 이것도 비어있으면 위 locationName 문자열을 그대로 씁니다.")]
        [SerializeField] private LocalizedString localizedLocationName;
        [FormerlySerializedAs("flyObjects")]
        [FormerlySerializedAs("bossInfoFlyObjects")]
        [SerializeField] private RectTransform[] locationIntroFlyObjects = System.Array.Empty<RectTransform>();

        [Header("보스 정보")]
        [SerializeField] private TMP_Text bossNameText;
        [SerializeField] private RectTransform bossInfoPanel;

        [Header("머그샷 무대")]
        [Tooltip("Sprite-Lit-Default 재질을 사용하는 머그샷 배경 SpriteRenderer의 루트입니다.")]
        [SerializeField] private Transform mugShotBackgroundRoot;
        [Tooltip("머그샷 배경이 좌우 화면 밖으로 이동할 월드 거리입니다.")]
        [SerializeField, Min(0f)] private float mugShotBackgroundTravelOffsetX = 20f;
        [Tooltip("보스별 카메라 확대 차이에도 배경이 화면을 덮도록 추가할 여백 비율입니다.")]
        [SerializeField, Min(1f)] private float mugShotBackgroundViewportFillMultiplier = 1.05f;
        [FormerlySerializedAs("mugShotBackgroundFadeSeconds")]
        [SerializeField, Min(0f)] private float mugShotBackgroundEnterSeconds = 0.22f;
        [SerializeField, Min(0f)] private float mugShotBackgroundExitSeconds = 0.2f;
        [SerializeField] private AnimationCurve mugShotBackgroundEnterCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve mugShotBackgroundExitCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("머그샷 복제 그림자")]
        [Tooltip("그림자로 복제할 스프라이트를 직접 지정합니다. 비워두면 보스 하위의 모든 SpriteRenderer를 자동으로 찾아서 복제합니다.")]
        [SerializeField] private SpriteRenderer[] bossShadowSourceOverrides = System.Array.Empty<SpriteRenderer>();
        [SerializeField] private Vector2 bossShadowWorldOffset = new(0.22f, -0.12f);
        [SerializeField] private Color bossShadowColor = new(0f, 0f, 0f, 0.55f);
        [SerializeField] private int bossShadowSortingOrderOffset = -100;

        [Header("화면 참조")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform topLetterboxPanel;
        [SerializeField] private RectTransform bottomLetterboxPanel;
        [Tooltip("BossAI의 전투 UI 루트입니다. 비워두면 BossAI에서 자동으로 찾습니다.")]
        [SerializeField] private RectTransform bossCombatUiRect;
        [Tooltip("보스 경과시간(Time) 텍스트의 RectTransform입니다. 자동으로 찾지 않으니 씬마다 직접 연결해야 합니다.")]
        [SerializeField] private RectTransform bossTimeRect;

        [Header("연출 스킵")]
        [Tooltip("ESC로 연출을 스킵할 수 있음을 알리는 텍스트입니다. 연출이 끝나거나 스킵되면 꺼집니다.")]
        [SerializeField] private TMP_Text skipHintText;
        [Tooltip("스킵 안내 텍스트에 사용할 로컬라이징 문자열입니다.")]
        [SerializeField] private LocalizedString localizedSkipHintText;

        [Header("씬 참조")]
        [SerializeField] private BossAI boss;
        [SerializeField] private PlayerCombatController player;
        [SerializeField] private CameraFollow2D cameraFollow;

        [Header("재생")]
        [SerializeField] private bool playOnStart = true;
        [SerializeField, Min(0f)] private float startDelaySeconds = 0.15f;
        [SerializeField, Min(0f)] private float playerWalkSpeed = 3.5f;

        [Header("Sound")]
        [Tooltip("플레이어가 인트로 위치로 걸어가는 동안 반복 재생할 SoundLibrary SFX ID입니다.")]
        [BossGraphSfxId]
        [SerializeField] private string playerWalkSfxId = "Walk";
        [Tooltip("머그샷 배경이 등장할 때 재생할 SoundLibrary SFX ID입니다.")]
        [BossGraphSfxId]
        [SerializeField] private string mugShotBackgroundSfxId = "Whip";
        [Tooltip("머그샷 조명과 셔터 SFX가 재생된 뒤 보스 BGM 페이드인을 시작하기까지의 시간입니다.")]
        [SerializeField, Min(0f)] private float mugShotToBossBgmDelaySeconds = 3f;

        [Header("재시작 연출")]
        [Tooltip("씬 전환이 끝난 뒤 플레이어 이동과 전투 UI 전환까지 걸리는 재시작 연출 시간입니다.")]
        [SerializeField, Min(0f)] private float restartSequenceSeconds = 2.5f;
        [Tooltip("재시작 시 레터박스가 사라지고 보스 체력바가 올라오는 시간입니다.")]
        [SerializeField, Min(0f)] private float restartCombatUiRevealSeconds = 0.35f;

        [Header("보스 카메라")]
        [SerializeField, Range(0f, 1f)] private float bossFocusWeight = 1f;
        [SerializeField, Range(0.5f, 0.98f)] private float bossViewportFillRatio = 0.9f;
        [FormerlySerializedAs("bossZoomMultiplier")]
        [SerializeField, Range(0.1f, 1f)] private float bossFallbackZoomMultiplier = 0.2f;
        [SerializeField, Min(0f)] private float bossFocusSeconds = 0.55f;
        [SerializeField, Min(0f)] private float bossFocusSettleTimeoutSeconds = 1f;

        [Header("지역 인트로 이동")]
        [FormerlySerializedAs("flyOffsetX")]
        [SerializeField, Min(0f)] private float locationFlyOffsetX = 1200f;
        [FormerlySerializedAs("flyInSeconds")]
        [SerializeField, Min(0f)] private float locationFlyInSeconds = 0.35f;
        [FormerlySerializedAs("flyOutSeconds")]
        [SerializeField, Min(0f)] private float locationFlyOutSeconds = 0.32f;
        [FormerlySerializedAs("objectStaggerSeconds")]
        [SerializeField, Min(0f)] private float locationObjectStaggerSeconds = 0.045f;
        [FormerlySerializedAs("flyInCurve")]
        [SerializeField] private AnimationCurve locationFlyInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [FormerlySerializedAs("flyOutCurve")]
        [SerializeField] private AnimationCurve locationFlyOutCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("지역 인트로 텍스트")]
        [FormerlySerializedAs("textFadeInSeconds")]
        [SerializeField, Min(0f)] private float locationTextFadeInSeconds = 0.18f;
        [FormerlySerializedAs("textHoldSeconds")]
        [SerializeField, Min(0f)] private float locationTextHoldSeconds = 0.85f;
        [FormerlySerializedAs("textFadeOutSeconds")]
        [SerializeField, Min(0f)] private float locationTextFadeOutSeconds = 0.45f;

        [Header("보스 정보 패널 이동")]
        [FormerlySerializedAs("bossPanelFlyOffsetX")]
        [SerializeField, Min(0f)] private float bossInfoTravelOffsetY = 520f;
        [FormerlySerializedAs("bossPanelFlyInSeconds")]
        [SerializeField, Min(0f)] private float bossInfoEnterSeconds = 0.35f;
        [FormerlySerializedAs("bossPanelFlyOutSeconds")]
        [SerializeField, Min(0f)] private float bossInfoExitSeconds = 0.32f;
        [FormerlySerializedAs("bossPanelFlyInCurve")]
        [SerializeField] private AnimationCurve bossInfoEnterCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [FormerlySerializedAs("bossPanelFlyOutCurve")]
        [SerializeField] private AnimationCurve bossInfoExitCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("머그샷 조명")]
        [Tooltip("배경 진입 후 머그샷 타이밍 구간에 켜둘 전용 Light2D입니다. 밝기는 Light2D의 Intensity에서 설정합니다.")]
        [FormerlySerializedAs("shutterLight")]
        [SerializeField] private Light2D mugShotLight;
        [Tooltip("머그샷 조명·그림자가 켜지는 순간 재생할 SoundLibrary SFX ID입니다. 비워두면 재생하지 않습니다.")]
        [BossGraphSfxId]
        [SerializeField] private string mugShotLightingSfxId;

        [Header("머그샷 타이밍")]
        [Tooltip("배경 진입이 끝난 뒤 Info가 올라오기까지의 텀입니다.")]
        [FormerlySerializedAs("backgroundToLightDelaySeconds")]
        [SerializeField, Min(0f)] private float backgroundToInfoDelaySeconds = 0.15f;
        [Tooltip("Info 진입이 끝난 뒤 조명과 그림자를 켜기까지의 텀입니다.")]
        [FormerlySerializedAs("lightToInfoDelaySeconds")]
        [SerializeField, Min(0f)] private float infoToLightDelaySeconds = 0.1f;
        [FormerlySerializedAs("postShutterHoldSeconds")]
        [FormerlySerializedAs("mugShotHoldSeconds")]
        [SerializeField, Min(0f)] private float infoHoldSeconds = 0.8f;
        [Tooltip("Info 퇴장이 끝난 뒤 조명과 그림자를 끄기까지의 텀입니다.")]
        [SerializeField, Min(0f)] private float infoExitToLightOffDelaySeconds = 0.1f;
        [Tooltip("조명과 그림자를 끈 뒤 배경이 오른쪽으로 퇴장하기까지의 텀입니다.")]
        [SerializeField, Min(0f)] private float lightOffToBackgroundExitDelaySeconds = 0.1f;

        [Header("전투 UI 전환")]
        [Tooltip("보스 확대가 풀리고 플레이어·보스 전투 구도로 돌아오기를 기다리는 최대 시간입니다.")]
        [SerializeField, Min(0f)] private float combatViewReturnTimeoutSeconds = 1.5f;
        [SerializeField, Min(0f)] private float letterboxExitOffset = 360f;
        [SerializeField, Min(0f)] private float bossCombatUiEnterOffset = 260f;
        [SerializeField, Min(0f)] private float combatUiRevealSeconds = 0.38f;
        [SerializeField] private AnimationCurve combatUiRevealCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private Vector2[] locationIntroTargetPositions = System.Array.Empty<Vector2>();
        private Vector2 bossInfoPanelTargetPosition;
        private Vector3 mugShotBackgroundTargetLocalPosition;
        private Vector3 mugShotBackgroundBaseLocalScale;
        private float currentBossInfoOffsetY;
        private float currentMugShotBackgroundOffsetX;
        private bool hasMugShotBackgroundBaseLocalScale;
        private Vector2 topLetterboxTargetPosition;
        private Vector2 bottomLetterboxTargetPosition;
        private Vector2 bossCombatUiTargetPosition;
        private Vector2 bossTimeUiTargetPosition;
        private Coroutine playRoutine;
        private Coroutine locationIntroRoutine;
        private Coroutine bossBgmDelayRoutine;
        private SoundManager.SfxPlaybackHandle playerWalkSfxHandle;
        private BossData localizedBossData;
        private LocalizedString boundLocationLocalizedString;
        private bool introControlAcquired;
        private bool cameraMouseLookLocked;
        private bool introCursorHidden;
        private bool previousGameplayInputBlocked;
        private bool playFullBossIntro = true;
        private bool cinematicFocusActive;
        private Transform bossShadowRoot;
        private SpriteRenderer[] bossShadowSources = System.Array.Empty<SpriteRenderer>();
        private SpriteRenderer[] bossShadowCopies = System.Array.Empty<SpriteRenderer>();
        private Animator[] bossCinematicAnimators = System.Array.Empty<Animator>();
        private float[] bossAnimatorSpeeds = System.Array.Empty<float>();
        private bool bossAnimationFrozen;
        private bool executionLetterboxActive;
        private float canvasAlphaBeforeExecutionLetterbox;
        private bool skipRequested;
        private bool skipHintLocalizationBound;

        public bool HasExecutionLetterbox => topLetterboxPanel != null && bottomLetterboxPanel != null;

        private void Awake()
        {
            ResolveReferences();
            if (playOnStart && boss != null)
            {
                // 첫 프레임이 그려지기 전에 화면을 덮어 보스 씬 노출을 막습니다.
                SceneTransition.PrepareCoveredEntry();
            }

            ResolveCanvasGroup();
            ClipIntroUiToReferenceFrame();
            CacheTargetPositions();
            CacheTransitionTargetPositions();
            SetVisible(false);
            SetLocationNameText(locationName);
            SetTextAlpha(locationNameText, 0f);
            SetTextAlpha(bossNameText, 1f);
            SetMugShotStageVisible(false);
            BindSkipHintLocalization();
            SetSkipHintVisible(false);
        }

        private void Update()
        {
            if (playRoutine == null || !playFullBossIntro || skipRequested)
            {
                return;
            }

            if (EscapePressed())
            {
                RequestSkip();
            }
        }

        private void Start()
        {
            if (playOnStart)
            {
                ResolveReferences();
                if (boss != null)
                {
                    SceneTransition.PrepareCoveredEntry();
                }

                Play();
            }
        }

        private void OnDisable()
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }

            if (locationIntroRoutine != null)
            {
                StopCoroutine(locationIntroRoutine);
                locationIntroRoutine = null;
            }

            StopBossBgmDelayRoutine();
            StopPlayerWalkSfx();
            EndPlayerCinematicMovement();
            SetBossAnimationFrozen(false);
            HideExecutionLetterboxImmediate();
            if (cinematicFocusActive && cameraFollow != null)
            {
                cameraFollow.EndCinematicFocus();
                cinematicFocusActive = false;
            }

            ReleaseIntroControl(false);
            SetMugShotStageVisible(false);
            UnbindBossData();
            SetSkipHintVisible(false);
            UnbindSkipHintLocalization();
            skipRequested = false;
        }

        public void SetLocationName(string value)
        {
            locationName = value ?? string.Empty;
            SetLocationNameText(locationName);
        }

        public void Play()
        {
            Play(locationName);
        }

        public void Play(string nextLocationName)
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
            }

            if (locationIntroRoutine != null)
            {
                StopCoroutine(locationIntroRoutine);
                locationIntroRoutine = null;
            }

            StopBossBgmDelayRoutine();
            StopPlayerWalkSfx();
            SetBossAnimationFrozen(false);

            locationName = nextLocationName ?? string.Empty;
            ResolveReferences();
            playFullBossIntro = boss == null || !GameFlowController.ConsumeBossRestartEntry();
            skipRequested = false;
            SetSkipHintVisible(false);
            AcquireIntroControl();
            playRoutine = StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            // startDelay가 0이어도 모든 씬 오브젝트의 Start가 끝난 뒤 연출을 진행한다.
            yield return null;
            yield return WaitUnscaled(startDelaySeconds);

            ResolveReferences();
            AcquireIntroControl();
            ResolveCanvasGroup();
            BindBossData();
            PreparePresentation();

            // 화면이 덮인 상태에서 UI 레이아웃과 첫 프레임을 확정합니다.
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();

            if (boss != null)
            {
                // 준비가 끝난 뒤 전환 리빌과 보스 인트로를 함께 시작합니다.
                yield return SceneTransition.BeginEntryReveal();
            }

            SetSkipHintVisible(playFullBossIntro);

            if (playFullBossIntro && !skipRequested)
            {
                locationIntroRoutine = StartCoroutine(PlayLocationIntro());
            }

            yield return WalkPlayerToIntroPosition();

            if (playFullBossIntro)
            {
                if (skipRequested)
                {
                    // 아직 시작하지 않은 연출은 재생하지 않고 즉시 최종 상태로 정리한다.
                    CancelLocationIntro();
                }
                else
                {
                    if (locationIntroRoutine != null)
                    {
                        yield return locationIntroRoutine;
                        locationIntroRoutine = null;
                    }

                    if (!skipRequested)
                    {
                        yield return PlayBossReveal();
                    }
                    else
                    {
                        CancelBossReveal();
                    }
                }
            }

            if (playFullBossIntro)
            {
                yield return ReturnCameraToCombatView(GetBossFocusTarget());
            }

            SetSkipHintVisible(false);

            boss?.ShowBossCombatUiForIntro();
            yield return AnimateCombatUiReveal();

            FinishPresentation();
            ReleaseIntroControl(boss != null);
            playRoutine = null;
        }

        private IEnumerator ReturnCameraToCombatView(Transform bossFocusTarget)
        {
            if (cameraFollow == null || bossFocusTarget == null)
            {
                yield break;
            }

            cameraFollow.BeginCinematicReturnToCombatView(bossFocusTarget);
            cinematicFocusActive = true;
            for (float elapsed = 0f;
                 elapsed < combatViewReturnTimeoutSeconds
                 && !skipRequested
                 && !cameraFollow.IsCinematicReturnToCombatViewSettled(bossFocusTarget);
                 elapsed += Time.unscaledDeltaTime)
            {
                yield return null;
            }

            cameraFollow.EndCinematicFocus();
            cinematicFocusActive = false;
        }

        private IEnumerator WalkPlayerToIntroPosition()
        {
            BossData data = boss != null ? boss.BossData : null;
            if (player == null || data == null)
            {
                yield break;
            }

            Vector2 startPosition = data.IntroWalkStartPosition;
            Vector2 endPosition = data.IntroWalkEndPosition;
            Vector2 walkDirection = endPosition - startPosition;
            float distance = walkDirection.magnitude;
            if (distance <= 0.001f)
            {
                yield break;
            }

            Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
            SetPlayerPosition(playerBody, startPosition);
            player.Visual?.BeginCinematicMovement(walkDirection);

            float duration = playFullBossIntro
                ? (playerWalkSpeed > 0f ? distance / playerWalkSpeed : 0f)
                : Mathf.Max(0f, restartSequenceSeconds - startDelaySeconds - restartCombatUiRevealSeconds);
            if (duration > 0f)
            {
                StartPlayerWalkSfx();
                for (float elapsed = 0f; elapsed < duration && !skipRequested; elapsed += Time.unscaledDeltaTime)
                {
                    float progress = Mathf.Clamp01(elapsed / duration);
                    SetPlayerPosition(playerBody, Vector2.Lerp(startPosition, endPosition, progress));
                    yield return null;
                }
            }

            SetPlayerPosition(playerBody, endPosition);
            EndPlayerCinematicMovement();
            StopPlayerWalkSfx();
        }

        private void StartPlayerWalkSfx()
        {
            StopPlayerWalkSfx();
            if (!string.IsNullOrEmpty(playerWalkSfxId))
            {
                playerWalkSfxHandle = SoundManager.PlayLoopingSfx(playerWalkSfxId);
            }
        }

        private void StopPlayerWalkSfx()
        {
            SoundManager.StopSfx(playerWalkSfxHandle);
            playerWalkSfxHandle = null;
        }

        private IEnumerator PlayBossReveal()
        {
            Transform bossFocusTarget = GetBossFocusTarget();
            if (cameraFollow != null && bossFocusTarget != null)
            {
                cameraFollow.BeginCinematicFocus(
                    bossFocusTarget,
                    bossFocusWeight,
                    CalculateBossZoomMultiplier(bossFocusTarget));
                cinematicFocusActive = true;
                yield return WaitUnscaled(bossFocusSeconds);

                for (float elapsed = 0f;
                     elapsed < bossFocusSettleTimeoutSeconds && !skipRequested && !cameraFollow.IsCinematicZoomSettled();
                     elapsed += Time.unscaledDeltaTime)
                {
                    yield return null;
                }
            }

            SetMugShotStageVisible(true);
            if (!string.IsNullOrEmpty(mugShotBackgroundSfxId))
            {
                SoundManager.PlaySfx(mugShotBackgroundSfxId);
            }

            FitMugShotBackgroundToCameraViewport();
            yield return AnimateMugShotBackground(
                -mugShotBackgroundTravelOffsetX,
                0f,
                mugShotBackgroundEnterSeconds,
                mugShotBackgroundEnterCurve);
            yield return WaitUnscaled(backgroundToInfoDelaySeconds);
            yield return AnimateBossInfoPanel(
                -bossInfoTravelOffsetY,
                0f,
                bossInfoEnterSeconds,
                bossInfoEnterCurve);
            yield return WaitUnscaled(infoToLightDelaySeconds);
            SetMugShotLighting(true);
            if (!string.IsNullOrEmpty(mugShotLightingSfxId))
            {
                SoundManager.PlaySfx(mugShotLightingSfxId);
            }
            ScheduleBossBgmAfterMugShot();
            SetBossAnimationFrozen(true);
            yield return WaitUnscaled(infoHoldSeconds);
            SetBossAnimationFrozen(false);
            yield return AnimateBossInfoPanel(
                0f,
                -bossInfoTravelOffsetY,
                bossInfoExitSeconds,
                bossInfoExitCurve);
            yield return WaitUnscaled(infoExitToLightOffDelaySeconds);
            SetMugShotLighting(false);
            yield return WaitUnscaled(lightOffToBackgroundExitDelaySeconds);
            yield return AnimateMugShotBackground(
                0f,
                mugShotBackgroundTravelOffsetX,
                mugShotBackgroundExitSeconds,
                mugShotBackgroundExitCurve);
            SetMugShotStageVisible(false);
        }

        private void ScheduleBossBgmAfterMugShot()
        {
            StopBossBgmDelayRoutine();
            if (boss == null)
            {
                return;
            }

            bossBgmDelayRoutine = StartCoroutine(PlayBossBgmAfterMugShotDelay());
        }

        private IEnumerator PlayBossBgmAfterMugShotDelay()
        {
            yield return WaitUnscaled(Mathf.Max(0f, mugShotToBossBgmDelaySeconds));
            boss?.PlayCombatBgmForIntro();
            bossBgmDelayRoutine = null;
        }

        private void StopBossBgmDelayRoutine()
        {
            if (bossBgmDelayRoutine == null)
            {
                return;
            }

            StopCoroutine(bossBgmDelayRoutine);
            bossBgmDelayRoutine = null;
        }

        private IEnumerator PlayLocationIntro()
        {
            SetLocationObjectsActive(true);
            
            yield return AnimateLocationObjects(
                -locationFlyOffsetX,
                0f,
                locationFlyInSeconds,
                locationObjectStaggerSeconds,
                locationFlyInCurve);
            yield return FadeLocationText(0f, 1f, locationTextFadeInSeconds);
            yield return WaitUnscaled(locationTextHoldSeconds);
            yield return FadeLocationText(1f, 0f, locationTextFadeOutSeconds);
            yield return AnimateLocationObjects(
                0f,
                locationFlyOffsetX,
                locationFlyOutSeconds,
                locationObjectStaggerSeconds,
                locationFlyOutCurve);
            
            SetLocationObjectsActive(false);
        }
        
        private void SetLocationObjectsActive(bool isActive)
        {
            if (locationIntroFlyObjects == null)
            {
                return;
            }

            for (int i = 0; i < locationIntroFlyObjects.Length; i++)
            {
                if (locationIntroFlyObjects[i] != null)
                {
                    locationIntroFlyObjects[i].gameObject.SetActive(isActive);
                }
            }
        }

        private IEnumerator AnimateCombatUiReveal()
        {
            float duration = Mathf.Max(
                0f,
                playFullBossIntro ? combatUiRevealSeconds : restartCombatUiRevealSeconds);
            Vector2 topHiddenPosition = topLetterboxTargetPosition + Vector2.up * letterboxExitOffset;
            Vector2 bottomHiddenPosition = bottomLetterboxTargetPosition + Vector2.down * letterboxExitOffset;
            Vector2 combatUiHiddenPosition = bossCombatUiTargetPosition + Vector2.down * bossCombatUiEnterOffset;
            Vector2 timeUiHiddenPosition = bossTimeUiTargetPosition + Vector2.up * bossCombatUiEnterOffset;

            if (duration <= 0f)
            {
                SetAnchoredPosition(topLetterboxPanel, topHiddenPosition);
                SetAnchoredPosition(bottomLetterboxPanel, bottomHiddenPosition);
                SetAnchoredPosition(bossCombatUiRect, bossCombatUiTargetPosition);
                SetAnchoredPosition(bossTimeRect, bossTimeUiTargetPosition);
                yield break;
            }

            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = EvaluateCurve(combatUiRevealCurve, progress);
                SetAnchoredPosition(topLetterboxPanel, Vector2.LerpUnclamped(topLetterboxTargetPosition, topHiddenPosition, eased));
                SetAnchoredPosition(bottomLetterboxPanel, Vector2.LerpUnclamped(bottomLetterboxTargetPosition, bottomHiddenPosition, eased));
                SetAnchoredPosition(bossCombatUiRect, Vector2.LerpUnclamped(combatUiHiddenPosition, bossCombatUiTargetPosition, eased));
                SetAnchoredPosition(bossTimeRect, Vector2.LerpUnclamped(timeUiHiddenPosition, bossTimeUiTargetPosition, eased));
                yield return null;
            }

            SetAnchoredPosition(topLetterboxPanel, topHiddenPosition);
            SetAnchoredPosition(bottomLetterboxPanel, bottomHiddenPosition);
            SetAnchoredPosition(bossCombatUiRect, bossCombatUiTargetPosition);
            SetAnchoredPosition(bossTimeRect, bossTimeUiTargetPosition);
        }

        public IEnumerator ShowExecutionLetterbox(float duration)
        {
            if (!HasExecutionLetterbox)
            {
                yield break;
            }

            ResolveCanvasGroup();
            if (!executionLetterboxActive)
            {
                canvasAlphaBeforeExecutionLetterbox = canvasGroup.alpha;
                executionLetterboxActive = true;
            }

            canvasGroup.alpha = 1f;
            Vector2 topHiddenPosition = topLetterboxTargetPosition + Vector2.up * letterboxExitOffset;
            Vector2 bottomHiddenPosition = bottomLetterboxTargetPosition + Vector2.down * letterboxExitOffset;
            Vector2 combatUiHiddenPosition = bossCombatUiTargetPosition
                + Vector2.down * ResolveExecutionCombatUiOffset();
            float animationDuration = Mathf.Max(0f, duration);

            if (animationDuration <= 0f)
            {
                SetAnchoredPosition(topLetterboxPanel, topLetterboxTargetPosition);
                SetAnchoredPosition(bottomLetterboxPanel, bottomLetterboxTargetPosition);
                SetAnchoredPosition(bossCombatUiRect, combatUiHiddenPosition);
                yield break;
            }

            for (float elapsed = 0f; elapsed < animationDuration; elapsed += Time.unscaledDeltaTime)
            {
                float progress = EvaluateCurve(combatUiRevealCurve, Mathf.Clamp01(elapsed / animationDuration));
                SetAnchoredPosition(
                    topLetterboxPanel,
                    Vector2.LerpUnclamped(topHiddenPosition, topLetterboxTargetPosition, progress));
                SetAnchoredPosition(
                    bottomLetterboxPanel,
                    Vector2.LerpUnclamped(bottomHiddenPosition, bottomLetterboxTargetPosition, progress));
                SetAnchoredPosition(
                    bossCombatUiRect,
                    Vector2.LerpUnclamped(bossCombatUiTargetPosition, combatUiHiddenPosition, progress));
                yield return null;
            }

            SetAnchoredPosition(topLetterboxPanel, topLetterboxTargetPosition);
            SetAnchoredPosition(bottomLetterboxPanel, bottomLetterboxTargetPosition);
            SetAnchoredPosition(bossCombatUiRect, combatUiHiddenPosition);
        }

        public IEnumerator HideExecutionLetterbox(float duration)
        {
            if (!executionLetterboxActive || !HasExecutionLetterbox)
            {
                yield break;
            }

            Vector2 topHiddenPosition = topLetterboxTargetPosition + Vector2.up * letterboxExitOffset;
            Vector2 bottomHiddenPosition = bottomLetterboxTargetPosition + Vector2.down * letterboxExitOffset;
            Vector2 combatUiHiddenPosition = bossCombatUiTargetPosition
                + Vector2.down * ResolveExecutionCombatUiOffset();
            float animationDuration = Mathf.Max(0f, duration);

            if (animationDuration > 0f)
            {
                for (float elapsed = 0f; elapsed < animationDuration; elapsed += Time.unscaledDeltaTime)
                {
                    float progress = EvaluateCurve(combatUiRevealCurve, Mathf.Clamp01(elapsed / animationDuration));
                    SetAnchoredPosition(
                        topLetterboxPanel,
                        Vector2.LerpUnclamped(topLetterboxTargetPosition, topHiddenPosition, progress));
                    SetAnchoredPosition(
                        bottomLetterboxPanel,
                        Vector2.LerpUnclamped(bottomLetterboxTargetPosition, bottomHiddenPosition, progress));
                    SetAnchoredPosition(
                        bossCombatUiRect,
                        Vector2.LerpUnclamped(combatUiHiddenPosition, bossCombatUiTargetPosition, progress));
                    yield return null;
                }
            }

            SetAnchoredPosition(topLetterboxPanel, topHiddenPosition);
            SetAnchoredPosition(bottomLetterboxPanel, bottomHiddenPosition);
            SetAnchoredPosition(bossCombatUiRect, bossCombatUiTargetPosition);
            RestoreExecutionLetterboxCanvasAlpha();
        }

        public void HideExecutionLetterboxImmediate()
        {
            if (!executionLetterboxActive)
            {
                return;
            }

            Vector2 topHiddenPosition = topLetterboxTargetPosition + Vector2.up * letterboxExitOffset;
            Vector2 bottomHiddenPosition = bottomLetterboxTargetPosition + Vector2.down * letterboxExitOffset;
            SetAnchoredPosition(topLetterboxPanel, topHiddenPosition);
            SetAnchoredPosition(bottomLetterboxPanel, bottomHiddenPosition);
            SetAnchoredPosition(bossCombatUiRect, bossCombatUiTargetPosition);
            RestoreExecutionLetterboxCanvasAlpha();
        }

        private void RestoreExecutionLetterboxCanvasAlpha()
        {
            ResolveCanvasGroup();
            canvasGroup.alpha = canvasAlphaBeforeExecutionLetterbox;
            executionLetterboxActive = false;
        }

        private float ResolveExecutionCombatUiOffset()
        {
            return topLetterboxPanel != null
                ? Mathf.Max(0f, topLetterboxPanel.rect.height)
                : 0f;
        }

        private IEnumerator AnimateLocationObjects(
            float fromOffsetX,
            float toOffsetX,
            float duration,
            float staggerSeconds,
            AnimationCurve curve)
        {
            int objectCount = locationIntroFlyObjects != null ? locationIntroFlyObjects.Length : 0;
            if (objectCount == 0)
            {
                yield break;
            }

            float totalSeconds = Mathf.Max(0f, duration) + Mathf.Max(0f, staggerSeconds) * (objectCount - 1);
            if (totalSeconds <= 0f)
            {
                SetLocationObjectsAtOffset(toOffsetX);
                yield break;
            }

            for (float elapsed = 0f; elapsed < totalSeconds && !skipRequested; elapsed += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < objectCount; i++)
                {
                    RectTransform target = locationIntroFlyObjects[i];
                    if (target == null || i >= locationIntroTargetPositions.Length)
                    {
                        continue;
                    }

                    float localElapsed = elapsed - staggerSeconds * i;
                    float progress = duration <= 0f ? 1f : Mathf.Clamp01(localElapsed / duration);
                    float eased = EvaluateCurve(curve, progress);
                    SetObjectOffset(
                        target,
                        locationIntroTargetPositions[i],
                        Mathf.LerpUnclamped(fromOffsetX, toOffsetX, eased));
                }

                yield return null;
            }

            SetLocationObjectsAtOffset(toOffsetX);
        }

        private IEnumerator AnimateBossInfoPanel(
            float fromOffsetY,
            float toOffsetY,
            float duration,
            AnimationCurve curve)
        {
            if (bossInfoPanel == null)
            {
                yield break;
            }

            if (duration <= 0f)
            {
                SetBossInfoPanelOffset(toOffsetY);
                yield break;
            }

            for (float elapsed = 0f; elapsed < duration && !skipRequested; elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = EvaluateCurve(curve, progress);
                SetBossInfoPanelOffset(Mathf.LerpUnclamped(fromOffsetY, toOffsetY, eased));
                yield return null;
            }

            SetBossInfoPanelOffset(toOffsetY);
        }

        private IEnumerator AnimateMugShotBackground(
            float fromOffsetX,
            float toOffsetX,
            float duration,
            AnimationCurve curve)
        {
            if (mugShotBackgroundRoot == null)
            {
                yield break;
            }

            if (duration <= 0f)
            {
                SetMugShotBackgroundOffset(toOffsetX);
                yield break;
            }

            for (float elapsed = 0f; elapsed < duration && !skipRequested; elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = EvaluateCurve(curve, progress);
                SetMugShotBackgroundOffset(Mathf.LerpUnclamped(fromOffsetX, toOffsetX, eased));
                yield return null;
            }

            SetMugShotBackgroundOffset(toOffsetX);
        }

        private IEnumerator FadeLocationText(float fromAlpha, float toAlpha, float duration)
        {
            if (duration <= 0f)
            {
                SetTextAlpha(locationNameText, toAlpha);
                yield break;
            }

            for (float elapsed = 0f; elapsed < duration && !skipRequested; elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.Clamp01(elapsed / duration);
                SetTextAlpha(locationNameText, Mathf.Lerp(fromAlpha, toAlpha, progress));
                yield return null;
            }

            SetTextAlpha(locationNameText, toAlpha);
        }

        private void ResolveReferences()
        {
            boss ??= FindFirstObjectByType<BossAI>();
            player ??= PlayerCombatController.Active ?? FindFirstObjectByType<PlayerCombatController>();
            if (cameraFollow == null && Camera.main != null)
            {
                cameraFollow = Camera.main.GetComponent<CameraFollow2D>();
            }

            cameraFollow ??= FindFirstObjectByType<CameraFollow2D>();
            bossCombatUiRect ??= boss != null ? boss.BossCombatUiRect : null;
        }

        private void AcquireIntroControl()
        {
            if (!introCursorHidden)
            {
                CursorController.PushForceCursorHidden();
                introCursorHidden = true;
            }

            if (!cameraMouseLookLocked && cameraFollow != null)
            {
                cameraFollow.PushMouseLookLock();
                cameraMouseLookLocked = true;
            }

            player?.Visual?.SetLeftArmVisible(false);

            if (introControlAcquired || boss == null)
            {
                return;
            }

            previousGameplayInputBlocked = GameModalState.BlocksGameplayInput;
            GameModalState.BlocksGameplayInput = true;
            boss.PushCombatStartLock();
            player?.PushExternalMovementLock();
            introControlAcquired = true;
        }

        private void ReleaseIntroControl(bool startCombat)
        {
            if (introCursorHidden)
            {
                CursorController.PopForceCursorHidden();
                introCursorHidden = false;
            }

            player?.Visual?.SetLeftArmVisible(true);

            if (introControlAcquired)
            {
                player?.PopExternalMovementLock();
                boss?.PopCombatStartLock();
                GameModalState.BlocksGameplayInput = previousGameplayInputBlocked;
                introControlAcquired = false;
            }

            if (startCombat)
            {
                boss?.TryStartCombatFromIntro();
            }

            if (cameraMouseLookLocked)
            {
                cameraFollow?.PopMouseLookLock();
                cameraMouseLookLocked = false;
            }
        }

        private void EndPlayerCinematicMovement()
        {
            if (player == null || player.Visual == null)
            {
                return;
            }

            Transform bossFocusTarget = GetBossFocusTarget();
            Vector2 facingDirection = bossFocusTarget != null
                ? (Vector2)(bossFocusTarget.position - player.transform.position)
                : Vector2.zero;
            player.Visual.EndCinematicMovement(facingDirection);
        }

        private void PreparePresentation()
        {
            CacheTransitionTargetPositions();
            if (boundLocationLocalizedString == null)
            {
                SetLocationNameText(locationName);
            }
            SetTextAlpha(locationNameText, 0f);
            SetTextAlpha(bossNameText, 1f);
            SetLocationObjectsAtOffset(-locationFlyOffsetX);
            SetBossInfoPanelOffset(-bossInfoTravelOffsetY);
            SetMugShotBackgroundOffset(-mugShotBackgroundTravelOffsetX);
            SetMugShotStageVisible(false);
            SetAnchoredPosition(topLetterboxPanel, topLetterboxTargetPosition);
            SetAnchoredPosition(bottomLetterboxPanel, bottomLetterboxTargetPosition);
            SetAnchoredPosition(
                bossCombatUiRect,
                bossCombatUiTargetPosition + Vector2.down * bossCombatUiEnterOffset);
            SetAnchoredPosition(
                bossTimeRect,
                bossTimeUiTargetPosition + Vector2.up * bossCombatUiEnterOffset);
            SetVisible(true);
        }

        private void FinishPresentation()
        {
            SetMugShotStageVisible(false);
            SetLocationObjectsActive(false);
            bool combatUiSharesRoot = bossCombatUiRect != null
                && canvasGroup != null
                && bossCombatUiRect.IsChildOf(canvasGroup.transform);
            if (!combatUiSharesRoot)
            {
                SetVisible(false);
            }
        }

        private void BindBossData()
        {
            UnbindBossData();
            localizedBossData = boss != null ? boss.BossData : null;
            if (localizedBossData == null)
            {
                SetBossNameText(boss != null ? boss.DisplayName : string.Empty);
                BindLocationLocalization();
                return;
            }

            SetBossNameText(localizedBossData.BossName);
            LoadoutSelectedSkillPanelLocalization.BindLocalizedString(
                localizedBossData.LocalizedBossName,
                localizedBossData.HasLocalizedBossName,
                SetBossNameText);

            BindLocationLocalization();
        }

        private void UnbindBossData()
        {
            if (boundLocationLocalizedString != null)
            {
                LoadoutSelectedSkillPanelLocalization.UnbindLocalizedString(
                    boundLocationLocalizedString,
                    true,
                    SetLocationNameText);
                boundLocationLocalizedString = null;
            }

            if (localizedBossData == null)
            {
                return;
            }

            LoadoutSelectedSkillPanelLocalization.UnbindLocalizedString(
                localizedBossData.LocalizedBossName,
                localizedBossData.HasLocalizedBossName,
                SetBossNameText);
            localizedBossData = null;
        }

        // 보스의 BossData에 로컬라이징된 지역명이 있으면 그걸 우선 쓰고, 없으면(튜토리얼처럼 보스가
        // 없는 경우 포함) 이 인트로 자체의 localizedLocationName으로 폴백한다. 둘 다 없으면 plain
        // locationName 문자열이 그대로 유지된다.
        private void BindLocationLocalization()
        {
            SetLocationNameText(locationName);

            LocalizedString source = null;
            if (localizedBossData != null && localizedBossData.HasLocalizedIntroLocationName)
            {
                source = localizedBossData.LocalizedIntroLocationName;
            }
            else if (LoadoutSelectedSkillPanelLocalization.HasLocalizedString(localizedLocationName))
            {
                source = localizedLocationName;
            }

            if (source == null)
            {
                return;
            }

            LoadoutSelectedSkillPanelLocalization.BindLocalizedString(source, true, SetLocationNameText);
            boundLocationLocalizedString = source;
        }

        private Transform GetBossFocusTarget()
        {
            return boss != null && boss.BodyRoot != null
                ? boss.BodyRoot
                : boss != null ? boss.transform : null;
        }

        private void CacheTargetPositions()
        {
            int objectCount = locationIntroFlyObjects != null ? locationIntroFlyObjects.Length : 0;
            if (locationIntroTargetPositions.Length != objectCount)
            {
                locationIntroTargetPositions = new Vector2[objectCount];
            }

            for (int i = 0; i < objectCount; i++)
            {
                locationIntroTargetPositions[i] = locationIntroFlyObjects[i] != null
                    ? locationIntroFlyObjects[i].anchoredPosition
                    : Vector2.zero;
            }
        }

        private void CacheTransitionTargetPositions()
        {
            if (topLetterboxPanel != null)
            {
                topLetterboxTargetPosition = topLetterboxPanel.anchoredPosition;
            }

            if (bottomLetterboxPanel != null)
            {
                bottomLetterboxTargetPosition = bottomLetterboxPanel.anchoredPosition;
            }

            if (bossCombatUiRect != null)
            {
                bossCombatUiTargetPosition = bossCombatUiRect.anchoredPosition;
            }

            if (bossTimeRect != null)
            {
                bossTimeUiTargetPosition = bossTimeRect.anchoredPosition;
            }

            if (bossInfoPanel != null)
            {
                bossInfoPanelTargetPosition = bossInfoPanel.anchoredPosition
                    - Vector2.up * currentBossInfoOffsetY;
            }

            if (mugShotBackgroundRoot != null)
            {
                mugShotBackgroundTargetLocalPosition = mugShotBackgroundRoot.localPosition
                    - Vector3.right * currentMugShotBackgroundOffsetX;
                if (!hasMugShotBackgroundBaseLocalScale)
                {
                    mugShotBackgroundBaseLocalScale = mugShotBackgroundRoot.localScale;
                    hasMugShotBackgroundBaseLocalScale = true;
                }
            }
        }

        private void SetLocationObjectsAtOffset(float offsetX)
        {
            int objectCount = locationIntroFlyObjects != null ? locationIntroFlyObjects.Length : 0;
            for (int i = 0; i < objectCount; i++)
            {
                RectTransform target = locationIntroFlyObjects[i];
                if (target == null || i >= locationIntroTargetPositions.Length)
                {
                    continue;
                }

                SetObjectOffset(target, locationIntroTargetPositions[i], offsetX);
            }
        }

        private static void SetObjectOffset(RectTransform target, Vector2 origin, float offsetX)
        {
            if (target != null)
            {
                target.anchoredPosition = origin + Vector2.right * offsetX;
            }
        }

        private void SetBossInfoPanelOffset(float offsetY)
        {
            currentBossInfoOffsetY = offsetY;
            if (bossInfoPanel != null)
            {
                bossInfoPanel.anchoredPosition = bossInfoPanelTargetPosition + Vector2.up * offsetY;
            }
        }

        private void SetMugShotBackgroundOffset(float offsetX)
        {
            currentMugShotBackgroundOffsetX = offsetX;
            if (mugShotBackgroundRoot != null)
            {
                mugShotBackgroundRoot.localPosition = mugShotBackgroundTargetLocalPosition
                    + Vector3.right * offsetX;
            }
        }

        private void FitMugShotBackgroundToCameraViewport()
        {
            if (mugShotBackgroundRoot == null)
            {
                return;
            }

            if (!hasMugShotBackgroundBaseLocalScale)
            {
                mugShotBackgroundBaseLocalScale = mugShotBackgroundRoot.localScale;
                hasMugShotBackgroundBaseLocalScale = true;
            }

            Camera activeCamera = cameraFollow != null
                ? cameraFollow.GetComponent<Camera>()
                : null;
            activeCamera ??= Camera.main;
            if (activeCamera == null || !activeCamera.orthographic)
            {
                return;
            }

            mugShotBackgroundRoot.localScale = mugShotBackgroundBaseLocalScale;
            SpriteRenderer[] renderers = mugShotBackgroundRoot.GetComponentsInChildren<SpriteRenderer>(true);
            bool hasBounds = false;
            Bounds combinedBounds = default;
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null || renderer.sprite == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combinedBounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds || combinedBounds.size.x <= 0f || combinedBounds.size.y <= 0f)
            {
                return;
            }

            float viewportHeight = activeCamera.orthographicSize * 2f;
            float viewportWidth = viewportHeight * activeCamera.aspect;
            float fillMultiplier = Mathf.Max(1f, mugShotBackgroundViewportFillMultiplier);
            float requiredScale = Mathf.Max(
                viewportWidth * fillMultiplier / combinedBounds.size.x,
                viewportHeight * fillMultiplier / combinedBounds.size.y);
            mugShotBackgroundRoot.localScale = mugShotBackgroundBaseLocalScale * Mathf.Max(1f, requiredScale);
        }

        private void SetMugShotStageVisible(bool visible)
        {
            if (mugShotBackgroundRoot != null)
            {
                mugShotBackgroundRoot.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                SetMugShotLighting(false);
            }
        }

        private void SetMugShotLighting(bool enabled)
        {
            SetMugShotLight(enabled);
            if (enabled)
            {
                SyncMugShotShadowSnapshot();
                SetMugShotShadowAlpha(bossShadowColor.a);
                return;
            }

            if (bossShadowRoot != null)
            {
                SetMugShotShadowAlpha(0f);
                bossShadowRoot.gameObject.SetActive(false);
            }
        }

        private void SetBossAnimationFrozen(bool frozen)
        {
            if (frozen)
            {
                if (bossAnimationFrozen)
                {
                    return;
                }

                Transform bossFocusTarget = GetBossFocusTarget();
                if (bossFocusTarget == null)
                {
                    return;
                }

                bossCinematicAnimators = bossFocusTarget.GetComponentsInChildren<Animator>(true);
                bossAnimatorSpeeds = new float[bossCinematicAnimators.Length];
                for (int i = 0; i < bossCinematicAnimators.Length; i++)
                {
                    Animator animator = bossCinematicAnimators[i];
                    if (animator == null)
                    {
                        continue;
                    }

                    bossAnimatorSpeeds[i] = animator.speed;
                    animator.speed = 0f;
                }

                bossAnimationFrozen = true;
                return;
            }

            if (!bossAnimationFrozen)
            {
                return;
            }

            int animatorCount = Mathf.Min(bossCinematicAnimators.Length, bossAnimatorSpeeds.Length);
            for (int i = 0; i < animatorCount; i++)
            {
                if (bossCinematicAnimators[i] != null)
                {
                    bossCinematicAnimators[i].speed = bossAnimatorSpeeds[i];
                }
            }

            bossCinematicAnimators = System.Array.Empty<Animator>();
            bossAnimatorSpeeds = System.Array.Empty<float>();
            bossAnimationFrozen = false;
        }

        private void EnsureMugShotShadow()
        {
            if (bossShadowCopies.Length > 0)
            {
                return;
            }

            SpriteRenderer[] overrides = bossShadowSourceOverrides;
            bool hasOverrides = false;
            for (int i = 0; i < overrides.Length; i++)
            {
                if (overrides[i] != null)
                {
                    hasOverrides = true;
                    break;
                }
            }

            if (!hasOverrides)
            {
                Transform sourceRoot = GetBossFocusTarget();
                if (sourceRoot == null)
                {
                    return;
                }

                bossShadowSources = sourceRoot.GetComponentsInChildren<SpriteRenderer>(true);
            }
            else
            {
                bossShadowSources = overrides;
            }

            GameObject shadowObject = new("MugShotBossShadow");
            bossShadowRoot = shadowObject.transform;
            bossShadowRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            bossShadowRoot.localScale = Vector3.one;
            bossShadowCopies = new SpriteRenderer[bossShadowSources.Length];

            for (int i = 0; i < bossShadowSources.Length; i++)
            {
                GameObject copyObject = new($"MugShotShadow_{i}");
                copyObject.transform.SetParent(bossShadowRoot, false);
                bossShadowCopies[i] = copyObject.AddComponent<SpriteRenderer>();
            }
        }

        private void SyncMugShotShadowSnapshot()
        {
            EnsureMugShotShadow();
            if (bossShadowRoot == null)
            {
                return;
            }

            bossShadowRoot.gameObject.SetActive(true);
            int copyCount = Mathf.Min(bossShadowSources.Length, bossShadowCopies.Length);
            for (int i = 0; i < copyCount; i++)
            {
                SpriteRenderer source = bossShadowSources[i];
                SpriteRenderer copy = bossShadowCopies[i];
                if (source == null || copy == null)
                {
                    continue;
                }

                copy.sprite = source.sprite;
                copy.drawMode = source.drawMode;
                copy.size = source.size;
                copy.flipX = source.flipX;
                copy.flipY = source.flipY;
                copy.maskInteraction = source.maskInteraction;
                copy.sortingLayerID = source.sortingLayerID;
                copy.sortingOrder = source.sortingOrder + bossShadowSortingOrderOffset;
                copy.transform.SetPositionAndRotation(
                    source.transform.position + (Vector3)bossShadowWorldOffset,
                    source.transform.rotation);
                copy.transform.localScale = source.transform.lossyScale;
                copy.enabled = source.enabled && source.gameObject.activeInHierarchy && source.sprite != null;
            }
        }

        private void SetMugShotShadowAlpha(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            for (int i = 0; i < bossShadowCopies.Length; i++)
            {
                SpriteRenderer renderer = bossShadowCopies[i];
                if (renderer == null)
                {
                    continue;
                }

                Color color = bossShadowColor;
                color.a = alpha;
                renderer.color = color;
            }
        }

        private void SetMugShotLight(bool enabled)
        {
            if (mugShotLight == null)
            {
                return;
            }

            mugShotLight.enabled = enabled;
        }

        private float CalculateBossZoomMultiplier(Transform bossFocusTarget)
        {
            if (cameraFollow == null || bossFocusTarget == null)
            {
                return bossFallbackZoomMultiplier;
            }

            SpriteRenderer[] renderers = bossFocusTarget.GetComponentsInChildren<SpriteRenderer>(false);
            bool hasBounds = false;
            Bounds combinedBounds = default;
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || renderer.sprite == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combinedBounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds
                ? cameraFollow.CalculateCinematicZoomMultiplier(combinedBounds, bossViewportFillRatio)
                : bossFallbackZoomMultiplier;
        }

        private static void SetAnchoredPosition(RectTransform target, Vector2 position)
        {
            if (target != null)
            {
                target.anchoredPosition = position;
            }
        }

        private void SetVisible(bool visible)
        {
            ResolveCanvasGroup();
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void SetBossNameText(string value)
        {
            if (bossNameText != null)
            {
                bossNameText.text = value ?? string.Empty;
            }
        }

        private void SetLocationNameText(string value)
        {
            if (locationNameText != null)
            {
                locationNameText.text = value ?? string.Empty;
            }
        }

        private static void SetTextAlpha(TMP_Text text, float alpha)
        {
            if (text == null)
            {
                return;
            }

            Color color = text.color;
            color.a = Mathf.Clamp01(alpha);
            text.color = color;
        }

        private void ResolveCanvasGroup()
        {
            if (canvasGroup != null)
            {
                return;
            }

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void ClipIntroUiToReferenceFrame()
        {
            ClipToReferenceFrame(locationIntroFlyObjects);
            UISafeFrameUtility.ClipToReferenceFrame(bossInfoPanel);
            UISafeFrameUtility.ClipToReferenceFrame(skipHintText != null ? skipHintText.transform as RectTransform : null);
        }

        private static void ClipToReferenceFrame(RectTransform[] targets)
        {
            if (targets == null)
            {
                return;
            }

            for (int i = 0; i < targets.Length; i++)
            {
                UISafeFrameUtility.ClipToReferenceFrame(targets[i]);
            }
        }

        private void SetPlayerPosition(Rigidbody2D body, Vector2 position)
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.position = position;
                return;
            }

            if (player != null)
            {
                Vector3 currentPosition = player.transform.position;
                player.transform.position = new Vector3(position.x, position.y, currentPosition.z);
            }
        }

        private static float EvaluateCurve(AnimationCurve curve, float progress)
        {
            return curve != null && curve.length > 0 ? curve.Evaluate(progress) : progress;
        }

        private void RequestSkip()
        {
            skipRequested = true;
            SetSkipHintVisible(false);
        }

        // 아직 시작하지 않은 지역 인트로를 재생하지 않고 곧바로 최종(숨김) 상태로 정리한다.
        private void CancelLocationIntro()
        {
            if (locationIntroRoutine != null)
            {
                StopCoroutine(locationIntroRoutine);
                locationIntroRoutine = null;
            }

            SetLocationObjectsActive(false);
            SetLocationObjectsAtOffset(-locationFlyOffsetX);
            SetTextAlpha(locationNameText, 0f);
        }

        // 아직 시작하지 않은 보스 리빌(머그샷) 연출을 재생하지 않고 곧바로 최종(숨김) 상태로 정리한다.
        private void CancelBossReveal()
        {
            StopBossBgmDelayRoutine();
            SetBossAnimationFrozen(false);
            SetMugShotStageVisible(false);
            SetBossInfoPanelOffset(-bossInfoTravelOffsetY);
            SetMugShotBackgroundOffset(-mugShotBackgroundTravelOffsetX);
            boss?.PlayCombatBgmForIntro();
        }

        private static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        private void SetSkipHintVisible(bool visible)
        {
            if (skipHintText != null)
            {
                skipHintText.gameObject.SetActive(visible);
            }
        }

        private void BindSkipHintLocalization()
        {
            if (skipHintLocalizationBound
                || skipHintText == null
                || !LoadoutSelectedSkillPanelLocalization.HasLocalizedString(localizedSkipHintText))
            {
                return;
            }

            LoadoutSelectedSkillPanelLocalization.BindLocalizedString(
                localizedSkipHintText,
                true,
                SetSkipHintText);
            skipHintLocalizationBound = true;
        }

        private void UnbindSkipHintLocalization()
        {
            if (!skipHintLocalizationBound)
            {
                return;
            }

            LoadoutSelectedSkillPanelLocalization.UnbindLocalizedString(
                localizedSkipHintText,
                true,
                SetSkipHintText);
            skipHintLocalizationBound = false;
        }

        private void SetSkipHintText(string value)
        {
            if (skipHintText != null)
            {
                skipHintText.text = value ?? string.Empty;
            }
        }

        private IEnumerator WaitUnscaled(float seconds)
        {
            for (float elapsed = 0f; elapsed < seconds && !skipRequested; elapsed += Time.unscaledDeltaTime)
            {
                yield return null;
            }
        }
    }
}
