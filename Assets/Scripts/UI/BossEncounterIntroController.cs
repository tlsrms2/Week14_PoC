using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
using Week14.Bootstrap;
using Week14.Combat;
using Week14.Enemy;
using Week14.GameFlow;

namespace Week14.UI
{
    public sealed class BossEncounterIntroController : MonoBehaviour
    {
        [Header("지역 정보")]
        [SerializeField] private string locationName = "지역명";
        [SerializeField] private TMP_Text locationNameText;
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
        [SerializeField] private Vector2 bossShadowWorldOffset = new(0.22f, -0.12f);
        [SerializeField] private Color bossShadowColor = new(0f, 0f, 0f, 0.55f);
        [SerializeField] private int bossShadowSortingOrderOffset = -100;

        [Header("화면 참조")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform topLetterboxPanel;
        [SerializeField] private RectTransform bottomLetterboxPanel;
        [Tooltip("BossAI의 전투 UI 루트입니다. 비워두면 BossAI에서 자동으로 찾습니다.")]
        [SerializeField] private RectTransform bossCombatUiRect;

        [Header("씬 참조")]
        [SerializeField] private BossAI boss;
        [SerializeField] private PlayerCombatController player;
        [SerializeField] private CameraFollow2D cameraFollow;

        [Header("재생")]
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private bool waitForSceneTransition = true;
        [SerializeField, Min(0f)] private float startDelaySeconds = 0.15f;
        [SerializeField, Min(0f)] private float playerWalkSpeed = 3.5f;

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
        private Coroutine playRoutine;
        private Coroutine locationIntroRoutine;
        private BossData localizedBossData;
        private bool introControlAcquired;
        private bool cameraMouseLookLocked;
        private bool previousGameplayInputBlocked;
        private bool playFullBossIntro = true;
        private bool cinematicFocusActive;
        private Transform bossShadowRoot;
        private SpriteRenderer[] bossShadowSources = System.Array.Empty<SpriteRenderer>();
        private SpriteRenderer[] bossShadowCopies = System.Array.Empty<SpriteRenderer>();

        private void Awake()
        {
            ResolveReferences();
            ResolveCanvasGroup();
            CacheTargetPositions();
            CacheTransitionTargetPositions();
            SetVisible(false);
            SetLocationNameText(locationName);
            SetTextAlpha(locationNameText, 0f);
            SetTextAlpha(bossNameText, 1f);
            SetMugShotStageVisible(false);
        }

        private void Start()
        {
            if (playOnStart)
            {
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

            EndPlayerCinematicMovement();
            if (cinematicFocusActive && cameraFollow != null)
            {
                cameraFollow.EndCinematicFocus();
                cinematicFocusActive = false;
            }

            ReleaseIntroControl(false);
            SetMugShotStageVisible(false);
            UnbindBossData();
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

            locationName = nextLocationName ?? string.Empty;
            ResolveReferences();
            playFullBossIntro = boss == null || !GameFlowController.ConsumeBossRestartEntry();
            AcquireIntroControl();
            playRoutine = StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            if (waitForSceneTransition)
            {
                while (SceneTransition.IsTransitioning)
                {
                    yield return null;
                }
            }

            // startDelay가 0이어도 모든 씬 오브젝트의 Start가 끝난 뒤 연출을 진행한다.
            yield return null;
            yield return WaitUnscaled(startDelaySeconds);

            ResolveReferences();
            AcquireIntroControl();
            ResolveCanvasGroup();
            BindBossData();
            PreparePresentation();

            if (playFullBossIntro)
            {
                locationIntroRoutine = StartCoroutine(PlayLocationIntro());
            }

            yield return WalkPlayerToIntroPosition();

            if (playFullBossIntro)
            {
                if (locationIntroRoutine != null)
                {
                    yield return locationIntroRoutine;
                    locationIntroRoutine = null;
                }

                yield return PlayBossReveal();
            }

            Transform bossFocusTarget = GetBossFocusTarget();
            yield return ReturnCameraToCombatView(bossFocusTarget);
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
                 && !cameraFollow.IsCinematicReturnToCombatViewSettled(bossFocusTarget);
                 elapsed += Time.unscaledDeltaTime)
            {
                yield return null;
            }

            cameraFollow.EndCinematicFocusToCombatView(bossFocusTarget);
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

            float duration = playerWalkSpeed > 0f ? distance / playerWalkSpeed : 0f;
            if (duration > 0f)
            {
                for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
                {
                    float progress = Mathf.Clamp01(elapsed / duration);
                    SetPlayerPosition(playerBody, Vector2.Lerp(startPosition, endPosition, progress));
                    yield return null;
                }
            }

            SetPlayerPosition(playerBody, endPosition);
            EndPlayerCinematicMovement();
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
                     elapsed < bossFocusSettleTimeoutSeconds && !cameraFollow.IsCinematicZoomSettled();
                     elapsed += Time.unscaledDeltaTime)
                {
                    yield return null;
                }
            }

            SetMugShotStageVisible(true);
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
            yield return WaitUnscaled(infoHoldSeconds);
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

        private IEnumerator PlayLocationIntro()
        {
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
        }

        private IEnumerator AnimateCombatUiReveal()
        {
            float duration = Mathf.Max(0f, combatUiRevealSeconds);
            Vector2 topHiddenPosition = topLetterboxTargetPosition + Vector2.up * letterboxExitOffset;
            Vector2 bottomHiddenPosition = bottomLetterboxTargetPosition + Vector2.down * letterboxExitOffset;
            Vector2 combatUiHiddenPosition = bossCombatUiTargetPosition + Vector2.down * bossCombatUiEnterOffset;

            if (duration <= 0f)
            {
                SetAnchoredPosition(topLetterboxPanel, topHiddenPosition);
                SetAnchoredPosition(bottomLetterboxPanel, bottomHiddenPosition);
                SetAnchoredPosition(bossCombatUiRect, bossCombatUiTargetPosition);
                yield break;
            }

            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = EvaluateCurve(combatUiRevealCurve, progress);
                SetAnchoredPosition(topLetterboxPanel, Vector2.LerpUnclamped(topLetterboxTargetPosition, topHiddenPosition, eased));
                SetAnchoredPosition(bottomLetterboxPanel, Vector2.LerpUnclamped(bottomLetterboxTargetPosition, bottomHiddenPosition, eased));
                SetAnchoredPosition(bossCombatUiRect, Vector2.LerpUnclamped(combatUiHiddenPosition, bossCombatUiTargetPosition, eased));
                yield return null;
            }

            SetAnchoredPosition(topLetterboxPanel, topHiddenPosition);
            SetAnchoredPosition(bottomLetterboxPanel, bottomHiddenPosition);
            SetAnchoredPosition(bossCombatUiRect, bossCombatUiTargetPosition);
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

            for (float elapsed = 0f; elapsed < totalSeconds; elapsed += Time.unscaledDeltaTime)
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

            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
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

            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
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

            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
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
            if (!cameraMouseLookLocked && cameraFollow != null)
            {
                cameraFollow.PushMouseLookLock();
                cameraMouseLookLocked = true;
            }

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
            SetLocationNameText(locationName);
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
            SetVisible(true);
        }

        private void FinishPresentation()
        {
            SetMugShotStageVisible(false);
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
                return;
            }

            SetBossNameText(localizedBossData.BossName);
            LoadoutSelectedSkillPanelLocalization.BindLocalizedString(
                localizedBossData.LocalizedBossName,
                localizedBossData.HasLocalizedBossName,
                SetBossNameText);
        }

        private void UnbindBossData()
        {
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

        private void EnsureMugShotShadow()
        {
            if (bossShadowCopies.Length > 0)
            {
                return;
            }

            Transform sourceRoot = GetBossFocusTarget();
            if (sourceRoot == null)
            {
                return;
            }

            GameObject shadowObject = new("MugShotBossShadow");
            bossShadowRoot = shadowObject.transform;
            bossShadowRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            bossShadowRoot.localScale = Vector3.one;
            bossShadowSources = sourceRoot.GetComponentsInChildren<SpriteRenderer>(true);
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

        private static IEnumerator WaitUnscaled(float seconds)
        {
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                yield return null;
            }
        }
    }
}
