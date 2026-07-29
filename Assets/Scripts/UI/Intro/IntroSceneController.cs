using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Week14.Bootstrap;

namespace Week14.UI
{
    [DisallowMultipleComponent]
    public sealed class IntroSceneController : MonoBehaviour
    {
        [Serializable]
        private sealed class JungleSyncCheckpoint
        {
            [SerializeField] private string label = string.Empty;
            [Tooltip("Bullet X where this frame advances to the next frame.")]
            [SerializeField] private float bulletX;
            [FormerlySerializedAs("frameIndex")]
            [Tooltip("Frame shown before Bullet X is reached. Reaching Bullet X shows this value + 1.")]
            [SerializeField, Min(0)] private int fromFrameIndex;

            public float BulletX => bulletX;
            public int FromFrameIndex => Mathf.Max(0, fromFrameIndex);
            public int NextFrameIndex => FromFrameIndex + 1;
        }

        [Header("Logo Roots")]
        [SerializeField] private CanvasGroup jungleLogoRoot;
        [SerializeField] private CanvasGroup teamLogoRoot;

        [Header("Bullet")]
        [SerializeField] private RectTransform bullet;
        [SerializeField] private IntroSpriteSequencePlayer bulletAnimation;
        [SerializeField] private GameObject bulletImpactEffect;
        [SerializeField] private Vector2 bulletStartPosition = new(-1080f, 10f);
        [SerializeField] private Vector2 bulletImpactPosition = new(-20f, 10f);
        [SerializeField, Min(0.01f)] private float bulletTravelSeconds = 1.1f;
        [SerializeField] private AnimationCurve bulletMoveCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [FormerlySerializedAs("introStartDelaySeconds")]
        [FormerlySerializedAs("jungleStartDelaySeconds")]
        [SerializeField, Min(0f)] private float bulletPreFireDelaySeconds = 0.15f;

        [Header("Jungle Sync")]
        [SerializeField] private IntroSpriteSequencePlayer jungleBreakAnimation;
        [SerializeField] private IntroSpriteSequencePlayer gameLabCompassAnimation;
        [SerializeField] private bool interpolateJungleFramesBetweenCheckpoints = true;
        [Tooltip("Each checkpoint is a threshold: reaching Bullet X advances From Frame Index to the next frame.")]
        [SerializeField] private JungleSyncCheckpoint[] jungleSyncCheckpoints = Array.Empty<JungleSyncCheckpoint>();
        [SerializeField, HideInInspector] private float jungleSyncStartX = -570f;
        [SerializeField, HideInInspector] private float jungleSyncEndX = -30f;
        [FormerlySerializedAs("jungleHoldSeconds")]
        [SerializeField, Min(0f)] private float postGameLabHoldSeconds = 0.45f;

        [Header("Jungle Shake")]
        [SerializeField] private RectTransform jungleShakeTarget;
        [SerializeField, Min(0f)] private float checkpointShakeDistance = 4f;
        [SerializeField, Min(0f)] private float checkpointShakeSeconds = 0.08f;
        [SerializeField, Min(0.01f)] private float checkpointShakeFrequency = 45f;
        [SerializeField, Min(0f)] private float impactShakeDistance = 16f;
        [SerializeField, Min(0f)] private float impactShakeSeconds = 0.24f;
        [SerializeField, Min(0.01f)] private float impactShakeFrequency = 55f;
        [SerializeField] private AnimationCurve shakeFalloffCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Header("Jungle Debris")]
        [SerializeField] private RectTransform particleContainer;
        [SerializeField, Min(0)] private int checkpointDebrisCount = 6;
        [SerializeField, Min(0)] private int impactDebrisCount = 27;
        [SerializeField] private Color debrisColor = new(0f, 0.87058824f, 0.79607844f, 1f);
        [SerializeField] private Color impactWhiteDebrisColor = Color.white;
        [SerializeField] private Vector2Int debrisSizeRange = new(5, 10);
        [SerializeField, Min(0.01f)] private float impactDebrisSizeMultiplier = 2f;
        [SerializeField, Range(0f, 1f)] private float impactWhiteDebrisRatio = 0.2f;
        [SerializeField, Min(0f)] private float debrisSpawnOffsetRadius = 6f;
        [SerializeField, Min(0f)] private float debrisOriginForwardOffset = 30f;
        [SerializeField] private Vector2 checkpointDebrisSpeedRange = new(140f, 260f);
        [SerializeField] private Vector2 impactDebrisSpeedRange = new(240f, 420f);
        [SerializeField, Min(0f)] private float debrisGravity = 650f;
        [SerializeField] private Vector2 debrisLifetimeRange = new(0.45f, 0.75f);
        [FormerlySerializedAs("debrisForwardBias")]
        [SerializeField, Range(0f, 1f)] private float debrisVerticalSpread = 0.8f;

        [Header("Impact Smoke")]
        [SerializeField] private Image impactSmokeImage;
        [SerializeField] private CanvasGroup impactSmokeCanvasGroup;
        [SerializeField] private Sprite[] impactSmokeFrames = Array.Empty<Sprite>();
        [SerializeField, Min(0.01f)] private float impactSmokeFramesPerSecond = 24f;
        [SerializeField, Min(0f)] private float impactSmokeFadeInSeconds = 0.05f;
        [SerializeField, Min(0f)] private float impactSmokeFadeOutSeconds = 0.18f;

        [Header("Logo Fade")]
        [SerializeField] private bool waitForUnitySplashBeforeLogoFade = true;
        [SerializeField] private bool waitForSceneTransitionBeforeLogoFade = true;
        [SerializeField, Min(0f)] private float preJungleLogoFadeHoldSeconds = 1f;
        [SerializeField, Min(0f)] private float jungleLogoFadeInSeconds = 1f;
        [SerializeField, Min(0f)] private float logoSwitchFadeSeconds;
        [SerializeField, Min(0f)] private float blankBetweenLogosSeconds = 0.15f;
        [SerializeField, Min(0f)] private float teamHoldSeconds = 1.2f;
        [SerializeField, Min(0f)] private float teamLogoFadeOutSeconds = 1f;
        [SerializeField, Min(0f)] private float postTeamFadeLoadDelaySeconds;

        [Header("Clipping")]
        [SerializeField] private bool ensureReferenceFrameMask = true;

        [Header("Flow")]
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private bool loadNextSceneOnComplete = true;
        [SerializeField] private string nextSceneName = "TitleScene";

        [SerializeField, HideInInspector] private IntroSpriteSequencePlayer[] jungleLogoAnimations = System.Array.Empty<IntroSpriteSequencePlayer>();

        private Coroutine playRoutine;
        private Coroutine jungleShakeRoutine;
        private RectTransform activeJungleShakeTarget;
        private Vector2 jungleShakeBasePosition;
        private bool hasJungleShakeBasePosition;
        private int lastTriggeredJungleCheckpointIndex = -1;
        private Coroutine impactSmokeRoutine;
        private readonly List<GameObject> activeDebrisParticles = new();

        private void Awake()
        {
            if (playOnStart)
            {
                PrepareLogoGroupsForPlay();
            }

            if (ensureReferenceFrameMask)
            {
                EnsureReferenceFrameMask();
            }
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
            Stop();
        }

        public void Play()
        {
            Stop();
            playRoutine = StartCoroutine(PlayRoutine());
        }

        public void Stop()
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }

            StopLogoAnimations();
            StopBullet();
            StopJungleShake(true);
            StopImpactSmoke(true);
            ClearDebrisParticles();
        }

        private IEnumerator PlayRoutine()
        {
            PrepareLogoGroupsForPlay();
            ResetLogoAnimations();
            PrepareBullet();
            CaptureJungleShakeBasePosition();
            ResetJungleCheckpointShakeState();
            ResetImpactSmoke();
            ClearDebrisParticles();

            yield return WaitUntilLogoFadeCanStart();

            if (preJungleLogoFadeHoldSeconds > 0f)
            {
                yield return WaitSeconds(preJungleLogoFadeHoldSeconds);
            }

            if (jungleLogoFadeInSeconds > 0f)
            {
                yield return FadeGroup(jungleLogoRoot, 0f, 1f, jungleLogoFadeInSeconds);
            }
            else
            {
                SetGroup(jungleLogoRoot, 1f, true);
            }

            if (bulletPreFireDelaySeconds > 0f)
            {
                yield return WaitSeconds(bulletPreFireDelaySeconds);
            }

            yield return PlayBulletPass();
            yield return PlayGameLabCompass();

            if (postGameLabHoldSeconds > 0f)
            {
                yield return WaitSeconds(postGameLabHoldSeconds);
            }

            if (logoSwitchFadeSeconds > 0f)
            {
                yield return FadeGroup(jungleLogoRoot, 1f, 0f, logoSwitchFadeSeconds);
            }
            else
            {
                SetGroup(jungleLogoRoot, 0f, false);
            }

            if (blankBetweenLogosSeconds > 0f)
            {
                yield return WaitSeconds(blankBetweenLogosSeconds);
            }

            if (logoSwitchFadeSeconds > 0f)
            {
                SetGroup(teamLogoRoot, 0f, true);
                yield return FadeGroup(teamLogoRoot, 0f, 1f, logoSwitchFadeSeconds);
            }
            else
            {
                SetGroup(teamLogoRoot, 1f, true);
            }

            if (teamHoldSeconds > 0f)
            {
                yield return WaitSeconds(teamHoldSeconds);
            }

            if (teamLogoFadeOutSeconds > 0f)
            {
                yield return FadeGroup(teamLogoRoot, 1f, 0f, teamLogoFadeOutSeconds);
            }
            else
            {
                SetGroup(teamLogoRoot, 0f, false);
            }

            if (postTeamFadeLoadDelaySeconds > 0f)
            {
                yield return WaitSeconds(postTeamFadeLoadDelaySeconds);
            }

            playRoutine = null;

            if (loadNextSceneOnComplete && !string.IsNullOrWhiteSpace(nextSceneName))
            {
                SceneTransition.LoadSceneFromCovered(nextSceneName);
            }
        }

        private IEnumerator PlayBulletPass()
        {
            IntroSpriteSequencePlayer jungle = GetJungleBreakAnimation();
            if (bullet == null)
            {
                SetJungleToFinalSyncFrame(jungle);
                yield break;
            }

            SetBulletPosition(bulletStartPosition);
            SetBulletVisible(true);
            bulletAnimation?.PlayFromStart();
            ResetJungleCheckpointShakeState();

            float duration = Mathf.Max(0.01f, bulletTravelSeconds);
            for (float elapsed = 0f; elapsed < duration; elapsed += GetDeltaTime())
            {
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                float moveProgress = EvaluateBulletMoveCurve(normalizedTime);
                Vector2 position = Vector2.LerpUnclamped(bulletStartPosition, bulletImpactPosition, moveProgress);
                SetBulletPosition(position);
                SyncJungleToBullet(position.x);
                TriggerJungleCheckpointEffects(position);
                yield return null;
            }

            SetBulletPosition(bulletImpactPosition);
            SyncJungleToBullet(bulletImpactPosition.x);
            TriggerJungleCheckpointEffects(bulletImpactPosition);
            SetJungleToFinalSyncFrame(jungle);
            StopBullet();
            TriggerJungleShake(impactShakeDistance, impactShakeSeconds, impactShakeFrequency);
            SpawnDebrisBurst(
                bulletImpactPosition,
                impactDebrisCount,
                impactDebrisSpeedRange,
                impactDebrisSizeMultiplier,
                impactWhiteDebrisRatio);
            PlayImpactSmoke();

            if (bulletImpactEffect != null)
            {
                bulletImpactEffect.SetActive(true);
            }
        }

        private IEnumerator PlayGameLabCompass()
        {
            IntroSpriteSequencePlayer gameLab = GetGameLabCompassAnimation();
            if (gameLab == null)
            {
                yield break;
            }

            gameLab.PlayFromStart();
            yield return WaitForAnimation(gameLab);
        }

        private void ResetLogoAnimations()
        {
            IntroSpriteSequencePlayer jungle = GetJungleBreakAnimation();
            IntroSpriteSequencePlayer gameLab = GetGameLabCompassAnimation();

            ResetAnimation(jungle);
            ResetAnimation(gameLab);

            if (bulletImpactEffect != null)
            {
                bulletImpactEffect.SetActive(false);
            }
        }

        private void StopLogoAnimations()
        {
            StopAnimation(GetJungleBreakAnimation());
            StopAnimation(GetGameLabCompassAnimation());
        }

        private void PrepareBullet()
        {
            if (bulletAnimation != null)
            {
                bulletAnimation.Stop();
                bulletAnimation.SetFrame(0);
            }

            SetBulletPosition(bulletStartPosition);
            SetBulletVisible(false);
        }

        private void StopBullet()
        {
            bulletAnimation?.Stop();
            SetBulletVisible(false);
        }

        private IEnumerator WaitForAnimation(IntroSpriteSequencePlayer animation)
        {
            if (animation == null)
            {
                yield break;
            }

            float waitSeconds = Mathf.Max(0f, animation.DurationSeconds);
            if (waitSeconds <= 0f)
            {
                yield break;
            }

            yield return WaitSeconds(waitSeconds);
        }

        private void SyncJungleToBullet(float bulletX)
        {
            IntroSpriteSequencePlayer jungle = GetJungleBreakAnimation();
            if (jungle == null)
            {
                return;
            }

            if (TryGetFirstJungleSyncCheckpoint(out JungleSyncCheckpoint first)
                && TryGetLastJungleSyncCheckpoint(out JungleSyncCheckpoint last))
            {
                jungle.SetFrame(ResolveJungleSyncFrame(first, last, bulletX, jungle.FrameCount));
                return;
            }

            float progress = Mathf.Approximately(jungleSyncStartX, jungleSyncEndX)
                ? 1f
                : Mathf.InverseLerp(jungleSyncStartX, jungleSyncEndX, bulletX);
            jungle.SetNormalizedProgress(progress);
        }

        private int ResolveJungleSyncFrame(
            JungleSyncCheckpoint first,
            JungleSyncCheckpoint last,
            float bulletX,
            int frameCount)
        {
            int maxFrameIndex = Mathf.Max(0, frameCount - 1);
            bool increasingX = last.BulletX >= first.BulletX;

            int previousCheckpointIndex = GetFirstJungleSyncCheckpointIndex();
            if (previousCheckpointIndex < 0)
            {
                return 0;
            }

            int frameIndex = Mathf.Clamp(first.FromFrameIndex, 0, maxFrameIndex);
            JungleSyncCheckpoint previous = null;
            for (int i = previousCheckpointIndex; i < jungleSyncCheckpoints.Length; i++)
            {
                JungleSyncCheckpoint checkpoint = jungleSyncCheckpoints[i];
                if (checkpoint == null)
                {
                    continue;
                }

                if (!HasReachedCheckpoint(bulletX, checkpoint.BulletX, increasingX))
                {
                    return interpolateJungleFramesBetweenCheckpoints && previous != null
                        ? InterpolateCheckpointFrame(previous, checkpoint, bulletX, maxFrameIndex)
                        : frameIndex;
                }

                frameIndex = Mathf.Clamp(checkpoint.NextFrameIndex, 0, maxFrameIndex);
                previous = checkpoint;
            }

            return frameIndex;
        }

        private int InterpolateCheckpointFrame(
            JungleSyncCheckpoint from,
            JungleSyncCheckpoint to,
            float bulletX,
            int maxFrameIndex)
        {
            if (from == null || to == null)
            {
                return 0;
            }

            float progress = Mathf.Approximately(from.BulletX, to.BulletX)
                ? 1f
                : Mathf.InverseLerp(from.BulletX, to.BulletX, bulletX);
            int frameIndex = Mathf.RoundToInt(Mathf.Lerp(from.NextFrameIndex, to.NextFrameIndex, progress));
            return Mathf.Clamp(frameIndex, 0, maxFrameIndex);
        }

        private void SetJungleToFinalSyncFrame(IntroSpriteSequencePlayer jungle)
        {
            if (jungle == null)
            {
                return;
            }

            if (TryGetLastJungleSyncCheckpoint(out JungleSyncCheckpoint last))
            {
                jungle.SetFrame(last.NextFrameIndex);
                return;
            }

            jungle.SetNormalizedProgress(1f);
        }

        private bool TryGetFirstJungleSyncCheckpoint(out JungleSyncCheckpoint checkpoint)
        {
            int index = GetFirstJungleSyncCheckpointIndex();
            checkpoint = index >= 0 ? jungleSyncCheckpoints[index] : null;
            return checkpoint != null;
        }

        private bool TryGetLastJungleSyncCheckpoint(out JungleSyncCheckpoint checkpoint)
        {
            if (jungleSyncCheckpoints != null)
            {
                for (int i = jungleSyncCheckpoints.Length - 1; i >= 0; i--)
                {
                    checkpoint = jungleSyncCheckpoints[i];
                    if (checkpoint != null)
                    {
                        return true;
                    }
                }
            }

            checkpoint = null;
            return false;
        }

        private int GetFirstJungleSyncCheckpointIndex()
        {
            if (jungleSyncCheckpoints == null)
            {
                return -1;
            }

            for (int i = 0; i < jungleSyncCheckpoints.Length; i++)
            {
                if (jungleSyncCheckpoints[i] != null)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool HasReachedCheckpoint(float bulletX, float checkpointX, bool increasingX)
        {
            return increasingX
                ? bulletX >= checkpointX
                : bulletX <= checkpointX;
        }

        private void ResetJungleCheckpointShakeState()
        {
            lastTriggeredJungleCheckpointIndex = -1;
        }

        private void TriggerJungleCheckpointEffects(Vector2 bulletPosition)
        {
            if (!TryGetFirstJungleSyncCheckpoint(out JungleSyncCheckpoint first)
                || !TryGetLastJungleSyncCheckpoint(out JungleSyncCheckpoint last))
            {
                return;
            }

            bool increasingX = last.BulletX >= first.BulletX;
            int startIndex = Mathf.Max(0, lastTriggeredJungleCheckpointIndex + 1);
            for (int i = startIndex; i < jungleSyncCheckpoints.Length; i++)
            {
                JungleSyncCheckpoint checkpoint = jungleSyncCheckpoints[i];
                if (checkpoint == null)
                {
                    continue;
                }

                if (!HasReachedCheckpoint(bulletPosition.x, checkpoint.BulletX, increasingX))
                {
                    break;
                }

                lastTriggeredJungleCheckpointIndex = i;
                Vector2 checkpointPosition = GetBulletPathPositionAtX(
                    checkpoint.BulletX,
                    bulletPosition.y);
                TriggerJungleShake(
                    checkpointShakeDistance,
                    checkpointShakeSeconds,
                    checkpointShakeFrequency);
                SpawnDebrisBurst(
                    checkpointPosition,
                    checkpointDebrisCount,
                    checkpointDebrisSpeedRange);
            }
        }

        private void TriggerJungleShake(float distance, float seconds, float frequency)
        {
            if (distance <= 0f || seconds <= 0f)
            {
                return;
            }

            RectTransform target = GetJungleShakeTarget();
            if (target == null)
            {
                return;
            }

            if (activeJungleShakeTarget != target || !hasJungleShakeBasePosition)
            {
                CaptureJungleShakeBasePosition();
            }

            StopJungleShake(false);
            jungleShakeRoutine = StartCoroutine(ShakeJungleRoutine(
                target,
                distance,
                seconds,
                frequency));
        }

        private IEnumerator ShakeJungleRoutine(
            RectTransform target,
            float distance,
            float seconds,
            float frequency)
        {
            float duration = Mathf.Max(0f, seconds);
            float shakeFrequency = Mathf.Max(0.01f, frequency);
            float seed = UnityEngine.Random.value * 1000f;

            for (float elapsed = 0f; elapsed < duration; elapsed += GetDeltaTime())
            {
                if (target == null)
                {
                    yield break;
                }

                float progress = Mathf.Clamp01(elapsed / duration);
                float strength = Mathf.Max(0f, distance * EvaluateShakeFalloff(progress));
                float sample = elapsed * shakeFrequency;
                Vector2 noise = new(
                    Mathf.PerlinNoise(seed, sample) * 2f - 1f,
                    Mathf.PerlinNoise(seed + 37.1f, sample) * 2f - 1f);

                if (noise.sqrMagnitude > 1f)
                {
                    noise.Normalize();
                }

                target.anchoredPosition = jungleShakeBasePosition + noise * strength;
                yield return null;
            }

            RestoreJungleShakePosition();
            jungleShakeRoutine = null;
        }

        private float EvaluateShakeFalloff(float progress)
        {
            if (shakeFalloffCurve == null || shakeFalloffCurve.length == 0)
            {
                return 1f - Mathf.Clamp01(progress);
            }

            return shakeFalloffCurve.Evaluate(Mathf.Clamp01(progress));
        }

        private void CaptureJungleShakeBasePosition()
        {
            activeJungleShakeTarget = GetJungleShakeTarget();
            if (activeJungleShakeTarget == null)
            {
                hasJungleShakeBasePosition = false;
                return;
            }

            jungleShakeBasePosition = activeJungleShakeTarget.anchoredPosition;
            hasJungleShakeBasePosition = true;
        }

        private RectTransform GetJungleShakeTarget()
        {
            if (jungleShakeTarget != null)
            {
                return jungleShakeTarget;
            }

            return jungleLogoRoot != null
                ? jungleLogoRoot.transform as RectTransform
                : null;
        }

        private void StopJungleShake(bool restorePosition)
        {
            if (jungleShakeRoutine != null)
            {
                StopCoroutine(jungleShakeRoutine);
                jungleShakeRoutine = null;
            }

            if (restorePosition)
            {
                RestoreJungleShakePosition();
            }
        }

        private void RestoreJungleShakePosition()
        {
            if (hasJungleShakeBasePosition && activeJungleShakeTarget != null)
            {
                activeJungleShakeTarget.anchoredPosition = jungleShakeBasePosition;
            }
        }

        private Vector2 GetBulletPathPositionAtX(float bulletX, float fallbackY)
        {
            if (Mathf.Approximately(bulletStartPosition.x, bulletImpactPosition.x))
            {
                return new Vector2(bulletX, fallbackY);
            }

            float progress = Mathf.InverseLerp(
                bulletStartPosition.x,
                bulletImpactPosition.x,
                bulletX);
            return new Vector2(
                bulletX,
                Mathf.LerpUnclamped(bulletStartPosition.y, bulletImpactPosition.y, progress));
        }

        private void SpawnDebrisBurst(
            Vector2 junglePosition,
            int count,
            Vector2 speedRange,
            float sizeMultiplier = 1f,
            float whiteParticleRatio = 0f)
        {
            RectTransform container = GetParticleContainer();
            if (container == null || count <= 0)
            {
                return;
            }

            Vector2 origin = ConvertJunglePositionToParticleContainerPosition(
                GetDebrisOriginPosition(junglePosition),
                container);
            int whiteParticleCount = Mathf.Clamp(
                Mathf.RoundToInt(count * Mathf.Clamp01(whiteParticleRatio)),
                0,
                count);

            for (int i = 0; i < count; i++)
            {
                Vector2 spawnOffset = RandomUnitVector()
                    * UnityEngine.Random.Range(0f, debrisSpawnOffsetRadius);
                Color particleColor = i < whiteParticleCount
                    ? impactWhiteDebrisColor
                    : debrisColor;
                CreateDebrisParticle(
                    container,
                    origin + spawnOffset,
                    RandomLeftwardDebrisDirection() * RandomRange(speedRange),
                    particleColor,
                    sizeMultiplier);
            }
        }

        private Vector2 GetDebrisOriginPosition(Vector2 bulletPosition)
        {
            return bulletPosition + GetBulletTravelDirection() * debrisOriginForwardOffset;
        }

        private void CreateDebrisParticle(
            RectTransform container,
            Vector2 anchoredPosition,
            Vector2 velocity,
            Color particleColor,
            float sizeMultiplier)
        {
            GameObject particle = new(
                "IntroDebrisParticle",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            particle.transform.SetParent(container, false);

            RectTransform rectTransform = particle.transform as RectTransform;
            if (rectTransform == null)
            {
                Destroy(particle);
                return;
            }

            int minSize = Mathf.Max(1, Mathf.Min(debrisSizeRange.x, debrisSizeRange.y));
            int maxSize = Mathf.Max(minSize, Mathf.Max(debrisSizeRange.x, debrisSizeRange.y));
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            float clampedSizeMultiplier = Mathf.Max(0.01f, sizeMultiplier);
            rectTransform.sizeDelta = new Vector2(
                UnityEngine.Random.Range(minSize, maxSize + 1) * clampedSizeMultiplier,
                UnityEngine.Random.Range(minSize, maxSize + 1) * clampedSizeMultiplier);
            rectTransform.localScale = Vector3.one;
            rectTransform.localEulerAngles = new Vector3(
                0f,
                0f,
                UnityEngine.Random.Range(0f, 360f));

            Image image = particle.GetComponent<Image>();
            image.color = particleColor;
            image.raycastTarget = false;

            activeDebrisParticles.Add(particle);
            StartCoroutine(AnimateDebrisParticle(
                rectTransform,
                image,
                velocity,
                UnityEngine.Random.Range(-720f, 720f),
                RandomRange(debrisLifetimeRange),
                particleColor));
        }

        private IEnumerator AnimateDebrisParticle(
            RectTransform rectTransform,
            Image image,
            Vector2 velocity,
            float angularVelocity,
            float lifetime,
            Color particleColor)
        {
            GameObject particle = rectTransform.gameObject;
            float duration = Mathf.Max(0.01f, lifetime);
            Vector3 startScale = rectTransform.localScale;

            for (float elapsed = 0f; elapsed < duration;)
            {
                if (rectTransform == null)
                {
                    yield break;
                }

                float deltaTime = GetDeltaTime();
                float progress = Mathf.Clamp01(elapsed / duration);
                velocity.y -= debrisGravity * deltaTime;
                rectTransform.anchoredPosition += velocity * deltaTime;
                rectTransform.Rotate(0f, 0f, angularVelocity * deltaTime);
                rectTransform.localScale = startScale
                    * (1f - Mathf.SmoothStep(0f, 1f, progress));

                if (image != null)
                {
                    Color color = particleColor;
                    color.a *= 1f - Mathf.SmoothStep(0f, 1f, progress);
                    image.color = color;
                }

                elapsed += deltaTime;
                yield return null;
            }

            activeDebrisParticles.Remove(particle);
            if (particle != null)
            {
                Destroy(particle);
            }
        }

        private Vector2 ConvertJunglePositionToParticleContainerPosition(
            Vector2 junglePosition,
            RectTransform container)
        {
            RectTransform jungleRoot = jungleLogoRoot != null
                ? jungleLogoRoot.transform as RectTransform
                : null;

            if (jungleRoot == null)
            {
                return junglePosition;
            }

            Vector3 worldPosition = jungleRoot.TransformPoint(junglePosition);
            Vector3 containerPosition = container.InverseTransformPoint(worldPosition);
            return new Vector2(containerPosition.x, containerPosition.y);
        }

        private RectTransform GetParticleContainer()
        {
            if (particleContainer != null)
            {
                return particleContainer;
            }

            Transform jungleRoot = jungleLogoRoot != null
                ? jungleLogoRoot.transform
                : null;
            return jungleRoot != null
                ? jungleRoot.Find("ParticleContainer") as RectTransform
                : null;
        }

        private Vector2 GetBulletTravelDirection()
        {
            Vector2 direction = bulletImpactPosition - bulletStartPosition;
            return direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : Vector2.right;
        }

        private void ClearDebrisParticles()
        {
            for (int i = activeDebrisParticles.Count - 1; i >= 0; i--)
            {
                GameObject particle = activeDebrisParticles[i];
                if (particle != null)
                {
                    Destroy(particle);
                }
            }

            activeDebrisParticles.Clear();
        }

        private Vector2 RandomLeftwardDebrisDirection()
        {
            float verticalSpread = Mathf.Clamp01(debrisVerticalSpread);
            Vector2 direction = new(
                -UnityEngine.Random.Range(0.35f, 1f),
                UnityEngine.Random.Range(-verticalSpread, verticalSpread));
            return direction.normalized;
        }

        private static Vector2 RandomUnitVector()
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private static float RandomRange(Vector2 range)
        {
            float min = Mathf.Min(range.x, range.y);
            float max = Mathf.Max(range.x, range.y);
            return UnityEngine.Random.Range(min, max);
        }

        private void PlayImpactSmoke()
        {
            ResolveImpactSmokeReferences();
            if (impactSmokeImage == null || impactSmokeFrames == null || impactSmokeFrames.Length == 0)
            {
                return;
            }

            StopImpactSmoke(false);
            SetImpactSmokeFrame(0);
            SetImpactSmokeAlpha(0f);
            impactSmokeRoutine = StartCoroutine(PlayImpactSmokeRoutine());
        }

        private IEnumerator PlayImpactSmokeRoutine()
        {
            int frameCount = impactSmokeFrames.Length;
            float framesPerSecond = Mathf.Max(0.01f, impactSmokeFramesPerSecond);
            float sequenceDuration = frameCount / framesPerSecond;
            float fadeInDuration = Mathf.Max(0f, impactSmokeFadeInSeconds);

            for (float elapsed = 0f; elapsed < sequenceDuration;)
            {
                int frameIndex = Mathf.Min(frameCount - 1, Mathf.FloorToInt(elapsed * framesPerSecond));
                SetImpactSmokeFrame(frameIndex);

                float alpha = fadeInDuration <= 0f
                    ? 1f
                    : Mathf.Lerp(0f, 1f, Mathf.Clamp01(elapsed / fadeInDuration));
                SetImpactSmokeAlpha(alpha);

                elapsed += GetDeltaTime();
                yield return null;
            }

            SetImpactSmokeFrame(frameCount - 1);
            SetImpactSmokeAlpha(1f);
            yield return FadeImpactSmokeAlpha(1f, 0f, impactSmokeFadeOutSeconds);
            SetImpactSmokeAlpha(0f);
            impactSmokeRoutine = null;
        }

        private IEnumerator FadeImpactSmokeAlpha(float fromAlpha, float toAlpha, float seconds)
        {
            float duration = Mathf.Max(0f, seconds);
            if (duration <= 0f)
            {
                SetImpactSmokeAlpha(toAlpha);
                yield break;
            }

            for (float elapsed = 0f; elapsed < duration;)
            {
                float progress = Mathf.Clamp01(elapsed / duration);
                SetImpactSmokeAlpha(Mathf.Lerp(fromAlpha, toAlpha, progress));
                elapsed += GetDeltaTime();
                yield return null;
            }

            SetImpactSmokeAlpha(toAlpha);
        }

        private void ResetImpactSmoke()
        {
            ResolveImpactSmokeReferences();
            StopImpactSmoke(false);
            SetImpactSmokeFrame(0);
            SetImpactSmokeAlpha(0f);
        }

        private void StopImpactSmoke(bool resetToHidden)
        {
            if (impactSmokeRoutine != null)
            {
                StopCoroutine(impactSmokeRoutine);
                impactSmokeRoutine = null;
            }

            if (resetToHidden)
            {
                SetImpactSmokeFrame(0);
                SetImpactSmokeAlpha(0f);
            }
        }

        private void SetImpactSmokeFrame(int frameIndex)
        {
            if (impactSmokeImage == null || impactSmokeFrames == null || impactSmokeFrames.Length == 0)
            {
                return;
            }

            Sprite frame = impactSmokeFrames[Mathf.Clamp(frameIndex, 0, impactSmokeFrames.Length - 1)];
            if (frame != null)
            {
                impactSmokeImage.sprite = frame;
                impactSmokeImage.enabled = true;
            }
        }

        private void SetImpactSmokeAlpha(float alpha)
        {
            float clampedAlpha = Mathf.Clamp01(alpha);
            if (impactSmokeCanvasGroup != null)
            {
                impactSmokeCanvasGroup.alpha = clampedAlpha;
                impactSmokeCanvasGroup.interactable = false;
                impactSmokeCanvasGroup.blocksRaycasts = false;
            }
            else if (impactSmokeImage != null)
            {
                Color color = impactSmokeImage.color;
                color.a = clampedAlpha;
                impactSmokeImage.color = color;
            }
        }

        private void ResolveImpactSmokeReferences()
        {
            if (impactSmokeImage == null)
            {
                RectTransform container = GetParticleContainer();
                Transform smoke = container != null ? container.Find("ImpactSmoke") : null;
                if (smoke != null)
                {
                    impactSmokeImage = smoke.GetComponent<Image>();
                }
            }

            if (impactSmokeCanvasGroup == null && impactSmokeImage != null)
            {
                impactSmokeCanvasGroup = impactSmokeImage.GetComponent<CanvasGroup>();
            }

            if (impactSmokeImage != null)
            {
                impactSmokeImage.raycastTarget = false;
            }
        }

        private void ResetAnimation(IntroSpriteSequencePlayer animation)
        {
            if (animation == null)
            {
                return;
            }

            animation.Stop();
            animation.SetFrame(0);
        }

        private void StopAnimation(IntroSpriteSequencePlayer animation)
        {
            animation?.Stop();
        }

        private IntroSpriteSequencePlayer GetJungleBreakAnimation()
        {
            return jungleBreakAnimation != null
                ? jungleBreakAnimation
                : GetLegacyLogoAnimation(0);
        }

        private IntroSpriteSequencePlayer GetGameLabCompassAnimation()
        {
            return gameLabCompassAnimation != null
                ? gameLabCompassAnimation
                : GetLegacyLogoAnimation(1);
        }

        private IntroSpriteSequencePlayer GetLegacyLogoAnimation(int index)
        {
            if (jungleLogoAnimations == null
                || index < 0
                || index >= jungleLogoAnimations.Length)
            {
                return null;
            }

            return jungleLogoAnimations[index];
        }

        private void SetBulletPosition(Vector2 position)
        {
            if (bullet != null)
            {
                bullet.anchoredPosition = position;
            }
        }

        private void SetBulletVisible(bool visible)
        {
            if (bullet != null)
            {
                bullet.gameObject.SetActive(visible);
            }
        }

        private float EvaluateBulletMoveCurve(float progress)
        {
            if (bulletMoveCurve == null || bulletMoveCurve.length == 0)
            {
                return Mathf.Clamp01(progress);
            }

            return bulletMoveCurve.Evaluate(Mathf.Clamp01(progress));
        }

        private IEnumerator FadeGroup(CanvasGroup group, float fromAlpha, float toAlpha, float seconds)
        {
            if (group == null)
            {
                yield break;
            }

            float startAlpha = Mathf.Clamp01(fromAlpha);
            float endAlpha = Mathf.Clamp01(toAlpha);
            group.alpha = startAlpha;
            group.interactable = false;
            group.blocksRaycasts = false;
            group.gameObject.SetActive(true);

            float duration = Mathf.Max(0f, seconds);
            if (duration <= 0f)
            {
                SetGroup(group, endAlpha, endAlpha > 0f);
                yield break;
            }

            for (float elapsed = 0f; elapsed < duration; elapsed += GetDeltaTime())
            {
                float progress = Mathf.Clamp01(elapsed / duration);
                group.alpha = Mathf.Lerp(startAlpha, endAlpha, progress);
                yield return null;
            }

            SetGroup(group, endAlpha, endAlpha > 0f);
        }

        private IEnumerator WaitSeconds(float seconds)
        {
            for (float elapsed = 0f; elapsed < seconds; elapsed += GetDeltaTime())
            {
                yield return null;
            }
        }

        private IEnumerator WaitUntilLogoFadeCanStart()
        {
            if (waitForUnitySplashBeforeLogoFade)
            {
                while (!SplashScreen.isFinished)
                {
                    yield return null;
                }
            }

            if (waitForSceneTransitionBeforeLogoFade)
            {
                while (SceneTransition.IsTransitioning)
                {
                    yield return null;
                }
            }
        }

        private void SetGroup(CanvasGroup group, float alpha, bool visible)
        {
            if (group == null)
            {
                return;
            }

            group.alpha = Mathf.Clamp01(alpha);
            group.interactable = false;
            group.blocksRaycasts = false;
            group.gameObject.SetActive(visible);
        }

        private void PrepareLogoGroupsForPlay()
        {
            SetGroup(jungleLogoRoot, 0f, true);
            SetGroup(teamLogoRoot, 0f, false);
        }

        private void EnsureReferenceFrameMask()
        {
            if (transform is not RectTransform rectTransform)
            {
                return;
            }

            rectTransform.sizeDelta = new Vector2(
                UISafeFrameUtility.ReferenceWidth,
                UISafeFrameUtility.ReferenceHeight);

            RectMask2D mask = GetComponent<RectMask2D>();
            if (mask == null)
            {
                mask = gameObject.AddComponent<RectMask2D>();
            }

            mask.padding = Vector4.zero;
            mask.softness = Vector2Int.zero;
        }

        private float GetDeltaTime()
        {
            return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }
    }
}
