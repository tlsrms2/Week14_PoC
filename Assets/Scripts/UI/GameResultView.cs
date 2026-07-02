using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Week14.Audio;
using Week14.Bootstrap;
using Week14.Combat;
using Week14.Enemy;
using Week14.Weapons;

namespace Week14.UI
{
    public sealed class GameResultView : MonoBehaviour
    {
        [Header("Game Over")]
        [SerializeField] private GameObject gameOverRoot;
        [SerializeField] private Button restartButton;

        [FormerlySerializedAs("titleButton")]
        [SerializeField] private Button gameOverLobbyButton;

        [Header("Victory")]
        [SerializeField] private GameObject victoryRoot;
        [SerializeField] private Button victoryLobbyButton;
        [SerializeField] private TMP_Text equippedWeaponText;
        [SerializeField] private Image equippedWeaponIcon;
        [SerializeField] private TMP_Text defeatedBossText;
        [SerializeField] private Image defeatedBossPortrait;

        [Header("Victory Presentation")]
        [SerializeField] private GameObject[] victoryBulletHitImages;
        [SerializeField, Min(0f)] private float victoryBulletHitStartDelaySeconds = 0.4f;
        [SerializeField, Min(0f)] private float victoryBulletHitIntervalSeconds = 0.08f;
        [SerializeField] private Transform victoryBulletParticleParent;
        [SerializeField] private Vector2 victoryBulletSmokeOffset;
        [SerializeField] private Vector2 victoryBulletSparkOffset;
        [SerializeField] private bool victorySmokeEnabled = true;
        [SerializeField, Min(1)] private int victorySmokeBurstCount = 12;
        [SerializeField, Min(0f)] private float victorySmokeLifetimeSeconds = 0.65f;
        [SerializeField, Min(0f)] private float victorySmokeSpeed = 35f;
        [SerializeField, Min(0f)] private float victorySmokeSize = 18f;
        [SerializeField, Min(0f)] private float victorySmokeSpreadRadius = 5f;
        [SerializeField] private Color victorySmokeColor = new(0.45f, 0.45f, 0.45f, 0.55f);
        [SerializeField] private bool victorySparkEnabled = true;
        [SerializeField, Min(1)] private int victorySparkBurstCount = 18;
        [SerializeField, Min(0f)] private float victorySparkLifetimeSeconds = 0.24f;
        [SerializeField, Min(0f)] private float victorySparkSpeed = 90f;
        [SerializeField, Min(0f)] private float victorySparkSize = 7f;
        [SerializeField, Min(0f)] private float victorySparkSpreadRadius = 3f;
        [SerializeField, Min(0f)] private float victorySparkGravity = 0.25f;
        [SerializeField] private Color victorySparkColor = new(1f, 0.72f, 0.18f, 1f);
        [SerializeField] private RectTransform victoryPortraitShakeTarget;
        [SerializeField, Min(0f)] private float victoryPortraitShakeSeconds = 0.25f;
        [SerializeField, Min(0f)] private float victoryPortraitShakeMagnitude = 8f;
        [SerializeField, Min(0f)] private float victoryPortraitShakeFrequency = 55f;

        [Header("Scene")]
        [FormerlySerializedAs("titleSceneName")]
        [SerializeField] private string lobbySceneName = "LobbyScene";

        private Health subscribedPlayerHealth;
        private float previousTimeScale = 1f;
        private bool resultOpen;
        private BossData localizedVictoryBossData;
        private BaseWeaponSO localizedVictoryWeaponData;
        private Coroutine victoryPresentationRoutine;
        private Coroutine victoryPortraitShakeRoutine;
        private RectTransform cachedVictoryPortraitShakeTarget;
        private Vector2 victoryPortraitShakeBasePosition;
        private Sprite victoryParticleSprite;
        private readonly List<GameObject> spawnedVictoryParticles = new();

        private void Awake()
        {
            CacheSceneReferences();
            BindButtons();
            SetResultVisible(false);
        }

        private void OnEnable()
        {
            TrySubscribePlayer();
            BossAI.Defeated += HandleBossDefeated;
        }

        private void OnDisable()
        {
            BossAI.Defeated -= HandleBossDefeated;
            UnsubscribePlayer();
            UnbindLocalizedVictoryBossName();
            UnbindLocalizedVictoryWeaponName();
            StopVictoryPresentation(resetVisuals: true);

            if (resultOpen)
            {
                UnfreezeGame();
            }

            resultOpen = false;
            RefreshInputBlock();
        }

        private void Update()
        {
            TrySubscribePlayer();
        }

        public void RestartScene()
        {
            Time.timeScale = 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            SceneTransition.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void ReturnToLobby()
        {
            Time.timeScale = 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            SceneTransition.LoadScene(lobbySceneName);
        }

        private void CacheSceneReferences()
        {
            gameOverRoot ??= FindGameObject("GameOverRoot");
            victoryRoot ??= FindGameObject("VictoryRoot");
            restartButton ??= FindComponent<Button>("RestartButton");
            gameOverLobbyButton ??= FindComponentIn<Button>(gameOverRoot, "LobbyButton")
                ?? FindComponent<Button>("GameOverLobbyButton");
            victoryLobbyButton ??= FindComponentIn<Button>(victoryRoot, "LobbyButton")
                ?? FindComponent<Button>("VictoryLobbyButton");
            equippedWeaponText ??= FindComponentIn<TMP_Text>(victoryRoot, "EquippedWeaponText")
                ?? FindComponentIn<TMP_Text>(victoryRoot, "VictoryWeaponText");
            equippedWeaponIcon ??= FindComponentIn<Image>(victoryRoot, "EquippedWeaponIcon")
                ?? FindComponentIn<Image>(victoryRoot, "VictoryWeaponIcon");
            defeatedBossText ??= FindComponentIn<TMP_Text>(victoryRoot, "DefeatedBossText")
                ?? FindComponentIn<TMP_Text>(victoryRoot, "VictoryBossText");
            defeatedBossPortrait ??= FindComponentIn<Image>(victoryRoot, "DefeatedBossPortrait")
                ?? FindComponentIn<Image>(victoryRoot, "VictoryBossPortrait");
            victoryPortraitShakeTarget ??= defeatedBossPortrait != null
                ? defeatedBossPortrait.rectTransform
                : null;
        }

        private void BindButtons()
        {
            restartButton?.onClick.AddListener(RestartScene);
            gameOverLobbyButton?.onClick.AddListener(ReturnToLobby);
            if (victoryLobbyButton != gameOverLobbyButton)
            {
                victoryLobbyButton?.onClick.AddListener(ReturnToLobby);
            }
        }

        private GameObject FindGameObject(string childName)
        {
            Transform child = FindChildRecursive(transform, childName);
            return child != null ? child.gameObject : null;
        }

        private T FindComponent<T>(string childName) where T : Component
        {
            Transform child = FindChildRecursive(transform, childName);
            return child != null ? child.GetComponent<T>() : null;
        }

        private T FindComponentIn<T>(GameObject root, string childName) where T : Component
        {
            if (root == null)
            {
                return null;
            }

            Transform child = FindChildRecursive(root.transform, childName);
            return child != null ? child.GetComponent<T>() : null;
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            foreach (Transform child in root)
            {
                if (child.name == childName)
                {
                    return child;
                }

                Transform match = FindChildRecursive(child, childName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private void TrySubscribePlayer()
        {
            PlayerCombatController player = PlayerCombatController.Active;
            Health nextHealth = player != null ? player.Health : null;
            if (subscribedPlayerHealth == nextHealth)
            {
                return;
            }

            UnsubscribePlayer();
            subscribedPlayerHealth = nextHealth;
            if (subscribedPlayerHealth != null)
            {
                subscribedPlayerHealth.Died += HandlePlayerDied;
            }
        }

        private void UnsubscribePlayer()
        {
            if (subscribedPlayerHealth == null)
            {
                return;
            }

            subscribedPlayerHealth.Died -= HandlePlayerDied;
            subscribedPlayerHealth = null;
        }

        private void HandlePlayerDied(Health _)
        {
            StartCoroutine(PlayPlayerDeathThenShowGameOver());
        }

        private IEnumerator PlayPlayerDeathThenShowGameOver()
        {
            SoundManager.StopBgm();

            PlayerCombatController player = PlayerCombatController.Active;
            CameraFollow2D playerCamera = player?.CameraFollow;
            PlayerCombatConfig config = player?.Config;
            Transform deathFocusTarget = player != null
                ? (player.BodyRoot != null ? player.BodyRoot : player.transform)
                : null;

            if (playerCamera != null && deathFocusTarget != null)
            {
                playerCamera.BeginCinematicFocus(
                    deathFocusTarget,
                    config != null ? config.DeathCameraFocusWeight : 1f,
                    config != null ? config.DeathCameraZoomMultiplier : 0.7f);
            }

            // 카메라 줌인이 끝날 때까지(정상 시간으로) 기다린 뒤 적/투사체를 멈춥니다. 줌인이 끝나지 않아도 최대 대기 시간을 넘기면 진행합니다.
            float maxWorldFreezeDelaySeconds = config != null ? config.DeathWorldFreezeDelaySeconds : 0.3f;
            float zoomWaitElapsed = 0f;
            while (playerCamera != null
                && !playerCamera.IsCinematicZoomSettled()
                && zoomWaitElapsed < maxWorldFreezeDelaySeconds)
            {
                zoomWaitElapsed += Time.deltaTime;
                yield return null;
            }

            float previousTimeScaleBeforeDeath = Time.timeScale;
            Time.timeScale = 0f;

            PlayerVisualRig visual = player?.Visual;
            if (visual != null)
            {
                // deathAnimator는 UnscaledTime으로 갱신되므로 Time.timeScale=0이어도 정상 재생됩니다.
                visual.PlayDeath();
                float deathAnimationSeconds = config != null ? config.DeathAnimationSeconds : 1.5333333f;
                if (deathAnimationSeconds > 0f)
                {
                    yield return new WaitForSecondsRealtime(deathAnimationSeconds);
                }
            }

            Time.timeScale = previousTimeScaleBeforeDeath;

            playerCamera?.EndCinematicFocus();
            ShowGameOver();
        }

        private void HandleBossDefeated(BossAI boss)
        {
            ShowVictory(boss);
        }

        private void ShowGameOver()
        {
            UnbindLocalizedVictoryBossName();
            UnbindLocalizedVictoryWeaponName();
            StopVictoryPresentation(resetVisuals: true);
            ShowResult(gameOverRoot, restartButton);
        }

        private void ShowVictory(BossAI boss)
        {
            RefreshVictorySummary(boss);
            ResetVictoryPresentationVisuals();

            GameObject targetRoot = victoryRoot != null ? victoryRoot : gameOverRoot;
            Selectable focusTarget = victoryLobbyButton != null ? victoryLobbyButton : gameOverLobbyButton;
            ShowResult(targetRoot, focusTarget);
            StartVictoryPresentation(targetRoot);
        }

        private void RefreshVictorySummary(BossAI boss)
        {
            BaseWeaponSO weapon = WeaponLoadoutManager.Instance != null
                ? WeaponLoadoutManager.Instance.CurrentWeapon
                : null;
            BossData bossData = boss != null ? boss.BossData : null;

            SetText(equippedWeaponText, weapon != null ? weapon.DisplayName : string.Empty);
            BindLocalizedVictoryWeaponName(weapon);
            SetImage(equippedWeaponIcon, weapon != null ? weapon.Icon : null);

            string bossName = bossData != null && !string.IsNullOrWhiteSpace(bossData.BossName)
                ? bossData.BossName
                : boss != null ? boss.DisplayName : string.Empty;
            SetText(defeatedBossText, bossName);
            BindLocalizedVictoryBossName(bossData);
            SetImage(defeatedBossPortrait, bossData != null ? bossData.ResultPortrait : null);
        }

        private void ShowResult(GameObject targetRoot, Selectable focusTarget)
        {
            SetResultVisible(true, targetRoot, focusTarget);
        }

        private static void FocusSelectable(Selectable target)
        {
            if (target == null || EventSystem.current == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        private void SetResultVisible(bool visible)
        {
            SetResultVisible(visible, null, null);
        }

        private void SetResultVisible(bool visible, GameObject activeRoot, Selectable focusTarget)
        {
            if (!visible)
            {
                UnbindLocalizedVictoryBossName();
                UnbindLocalizedVictoryWeaponName();
            }

            if (!visible || activeRoot != victoryRoot)
            {
                StopVictoryPresentation(resetVisuals: true);
            }

            resultOpen = visible;
            if (visible && activeRoot == null)
            {
                activeRoot = gameOverRoot;
            }

            SetRootVisible(gameOverRoot, visible && activeRoot == gameOverRoot);
            SetRootVisible(victoryRoot, visible && activeRoot == victoryRoot);

            if (visible)
            {
                FocusSelectable(focusTarget);
                FreezeGame();
            }
            else
            {
                UnfreezeGame();
            }

            RefreshInputBlock();
        }

        private void StartVictoryPresentation(GameObject activeRoot)
        {
            if (activeRoot != victoryRoot || VictoryHitCount == 0)
            {
                return;
            }

            StopVictoryPresentation(resetVisuals: false);
            cachedVictoryPortraitShakeTarget = null;
            ResetVictoryPortraitShakeTarget();
            victoryPresentationRoutine = StartCoroutine(PlayVictoryPresentationRoutine());
        }

        private IEnumerator PlayVictoryPresentationRoutine()
        {
            if (victoryBulletHitStartDelaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(victoryBulletHitStartDelaySeconds);
            }

            int hitCount = VictoryHitCount;
            for (int i = 0; i < hitCount; i++)
            {
                GameObject hitImage = SetIndexedActive(victoryBulletHitImages, i, true);
                SpawnVictorySmoke(hitImage);
                SpawnVictorySpark(hitImage);
                StartVictoryPortraitShake();

                if (victoryBulletHitIntervalSeconds > 0f && i < hitCount - 1)
                {
                    yield return new WaitForSecondsRealtime(victoryBulletHitIntervalSeconds);
                }
            }

            victoryPresentationRoutine = null;
        }

        private int VictoryHitCount => victoryBulletHitImages?.Length ?? 0;

        private void StopVictoryPresentation(bool resetVisuals)
        {
            if (victoryPresentationRoutine != null)
            {
                StopCoroutine(victoryPresentationRoutine);
                victoryPresentationRoutine = null;
            }

            if (victoryPortraitShakeRoutine != null)
            {
                StopCoroutine(victoryPortraitShakeRoutine);
                victoryPortraitShakeRoutine = null;
            }

            if (resetVisuals)
            {
                ResetVictoryPresentationVisuals();
            }
        }

        private void ResetVictoryPresentationVisuals()
        {
            SetAllActive(victoryBulletHitImages, false);
            DestroySpawnedVictoryParticles();
            ResetVictoryPortraitShakeTarget();
        }

        private static void SetAllActive(GameObject[] targets, bool active)
        {
            if (targets == null)
            {
                return;
            }

            foreach (GameObject target in targets)
            {
                if (target != null)
                {
                    target.SetActive(active);
                }
            }
        }

        private static GameObject SetIndexedActive(GameObject[] targets, int index, bool active)
        {
            if (targets == null || index < 0 || index >= targets.Length || targets[index] == null)
            {
                return null;
            }

            targets[index].SetActive(active);
            return targets[index];
        }

        private void SpawnVictorySmoke(GameObject anchor)
        {
            if (!victorySmokeEnabled)
            {
                return;
            }

            SpawnVictoryParticleImages(
                "Victory Smoke",
                anchor,
                victoryBulletSmokeOffset,
                victorySmokeBurstCount,
                victorySmokeLifetimeSeconds,
                victorySmokeSpeed,
                victorySmokeSize,
                victorySmokeSpreadRadius,
                0f,
                victorySmokeColor,
                growOverLifetime: true);
        }

        private void SpawnVictorySpark(GameObject anchor)
        {
            if (!victorySparkEnabled)
            {
                return;
            }

            SpawnVictoryParticleImages(
                "Victory Spark",
                anchor,
                victoryBulletSparkOffset,
                victorySparkBurstCount,
                victorySparkLifetimeSeconds,
                victorySparkSpeed,
                victorySparkSize,
                victorySparkSpreadRadius,
                victorySparkGravity,
                victorySparkColor,
                growOverLifetime: false);
        }

        private void SpawnVictoryParticleImages(
            string objectName,
            GameObject anchor,
            Vector2 offset,
            int burstCount,
            float lifetimeSeconds,
            float speed,
            float size,
            float spreadRadius,
            float gravity,
            Color color,
            bool growOverLifetime)
        {
            if (anchor == null)
            {
                return;
            }

            RectTransform parent = GetVictoryParticleParent(anchor);
            if (parent == null)
            {
                return;
            }

            Vector2 origin = GetVictoryParticleOrigin(parent, anchor.transform, offset);
            int count = Mathf.Max(1, burstCount);
            float lifetime = Mathf.Max(0.01f, lifetimeSeconds);
            for (int i = 0; i < count; i++)
            {
                GameObject particle = CreateVictoryParticleImage(objectName, parent, origin, size, color);
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(0f, spreadRadius);
                Vector2 startOffset = new(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                Vector2 velocity = new(Mathf.Cos(angle), Mathf.Sin(angle));
                velocity *= Random.Range(speed * 0.45f, speed);
                StartCoroutine(AnimateVictoryParticleImage(
                    particle,
                    origin + startOffset,
                    velocity,
                    lifetime,
                    size,
                    gravity,
                    color,
                    growOverLifetime));
            }
        }

        private void DestroySpawnedVictoryParticles()
        {
            for (int i = spawnedVictoryParticles.Count - 1; i >= 0; i--)
            {
                GameObject particle = spawnedVictoryParticles[i];
                if (particle != null)
                {
                    Destroy(particle);
                }
            }

            spawnedVictoryParticles.Clear();
        }

        private RectTransform GetVictoryParticleParent(GameObject anchor)
        {
            Transform parent = victoryBulletParticleParent;
            if (parent == null && victoryRoot != null)
            {
                parent = victoryRoot.transform;
            }

            if (parent == null)
            {
                parent = anchor.transform.parent;
            }

            return parent as RectTransform;
        }

        private static Vector2 GetVictoryParticleOrigin(RectTransform parent, Transform anchor, Vector2 offset)
        {
            Camera canvasCamera = GetCanvasCamera(parent);
            Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(canvasCamera, anchor.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent,
                screenPosition,
                canvasCamera,
                out Vector2 localPosition);
            return localPosition + offset;
        }

        private static Camera GetCanvasCamera(RectTransform target)
        {
            Canvas canvas = target.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        }

        private GameObject CreateVictoryParticleImage(
            string objectName,
            RectTransform parent,
            Vector2 anchoredPosition,
            float size,
            Color color)
        {
            GameObject particle = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            particle.transform.SetParent(parent, false);
            particle.transform.SetAsLastSibling();

            RectTransform rect = particle.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = Vector2.one * Mathf.Max(1f, size);

            Image image = particle.GetComponent<Image>();
            image.sprite = GetVictoryParticleSprite();
            image.color = color;
            image.raycastTarget = false;

            spawnedVictoryParticles.Add(particle);
            return particle;
        }

        private Sprite GetVictoryParticleSprite()
        {
            if (victoryParticleSprite != null)
            {
                return victoryParticleSprite;
            }

            const int textureSize = 16;
            Texture2D texture = new(textureSize, textureSize, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            Vector2 center = new((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);
            float radius = textureSize * 0.5f;
            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(1f - distance / radius);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            victoryParticleSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, textureSize, textureSize),
                new Vector2(0.5f, 0.5f),
                textureSize);
            victoryParticleSprite.hideFlags = HideFlags.HideAndDontSave;
            return victoryParticleSprite;
        }

        private IEnumerator AnimateVictoryParticleImage(
            GameObject particle,
            Vector2 startPosition,
            Vector2 velocity,
            float lifetimeSeconds,
            float startSize,
            float gravity,
            Color color,
            bool growOverLifetime)
        {
            if (particle == null)
            {
                yield break;
            }

            RectTransform rect = particle.GetComponent<RectTransform>();
            Image image = particle.GetComponent<Image>();
            float elapsed = 0f;
            while (particle != null && elapsed < lifetimeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / lifetimeSeconds);
                Vector2 position = startPosition + velocity * elapsed;
                position.y -= gravity * elapsed * elapsed * 100f;
                rect.anchoredPosition = position;

                float sizeProgress = growOverLifetime ? Mathf.Lerp(0.35f, 1f, progress) : 1f - progress;
                rect.sizeDelta = Vector2.one * Mathf.Max(1f, startSize * sizeProgress);

                Color currentColor = color;
                currentColor.a = color.a * (1f - progress);
                image.color = currentColor;
                yield return null;
            }

            spawnedVictoryParticles.Remove(particle);
            Destroy(particle);
        }

        private void StartVictoryPortraitShake()
        {
            RectTransform target = GetVictoryPortraitShakeTarget();
            if (target == null || victoryPortraitShakeSeconds <= 0f || victoryPortraitShakeMagnitude <= 0f)
            {
                return;
            }

            if (victoryPortraitShakeRoutine != null)
            {
                StopCoroutine(victoryPortraitShakeRoutine);
            }

            CacheVictoryPortraitShakeBasePosition(target);
            victoryPortraitShakeRoutine = StartCoroutine(ShakeVictoryPortraitRoutine(target));
        }

        private IEnumerator ShakeVictoryPortraitRoutine(RectTransform target)
        {
            float elapsed = 0f;
            while (elapsed < victoryPortraitShakeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / victoryPortraitShakeSeconds);
                float damper = 1f - progress;
                float sample = Time.unscaledTime * victoryPortraitShakeFrequency;
                float x = (Mathf.PerlinNoise(sample, 0f) * 2f - 1f) * victoryPortraitShakeMagnitude * damper;
                float y = (Mathf.PerlinNoise(0f, sample) * 2f - 1f) * victoryPortraitShakeMagnitude * damper;
                target.anchoredPosition = victoryPortraitShakeBasePosition + new Vector2(x, y);
                yield return null;
            }

            target.anchoredPosition = victoryPortraitShakeBasePosition;
            victoryPortraitShakeRoutine = null;
        }

        private RectTransform GetVictoryPortraitShakeTarget()
        {
            if (victoryPortraitShakeTarget != null)
            {
                return victoryPortraitShakeTarget;
            }

            return defeatedBossPortrait != null ? defeatedBossPortrait.rectTransform : null;
        }

        private void CacheVictoryPortraitShakeBasePosition(RectTransform target)
        {
            if (cachedVictoryPortraitShakeTarget == target)
            {
                return;
            }

            cachedVictoryPortraitShakeTarget = target;
            victoryPortraitShakeBasePosition = target.anchoredPosition;
        }

        private void ResetVictoryPortraitShakeTarget()
        {
            RectTransform target = GetVictoryPortraitShakeTarget();
            if (target == null)
            {
                cachedVictoryPortraitShakeTarget = null;
                return;
            }

            CacheVictoryPortraitShakeBasePosition(target);
            target.anchoredPosition = victoryPortraitShakeBasePosition;
        }

        private static void SetRootVisible(GameObject root, bool visible)
        {
            if (root != null)
            {
                root.SetActive(visible);
            }
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }

        private static void SetImage(Image image, Sprite sprite)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = sprite;
            image.enabled = sprite != null;
        }

        private void BindLocalizedVictoryBossName(BossData bossData)
        {
            UnbindLocalizedVictoryBossName();

            if (bossData == null || !bossData.HasLocalizedBossName)
            {
                return;
            }

            localizedVictoryBossData = bossData;
            bossData.LocalizedBossName.StringChanged += SetDefeatedBossText;
            bossData.LocalizedBossName.RefreshString();
        }

        private void UnbindLocalizedVictoryBossName()
        {
            if (localizedVictoryBossData == null)
            {
                return;
            }

            if (localizedVictoryBossData.HasLocalizedBossName)
            {
                localizedVictoryBossData.LocalizedBossName.StringChanged -= SetDefeatedBossText;
            }

            localizedVictoryBossData = null;
        }

        private void SetDefeatedBossText(string value)
        {
            SetText(defeatedBossText, value);
        }

        private void BindLocalizedVictoryWeaponName(BaseWeaponSO weapon)
        {
            UnbindLocalizedVictoryWeaponName();

            if (weapon == null || !weapon.HasLocalizedDisplayName)
            {
                return;
            }

            localizedVictoryWeaponData = weapon;
            weapon.LocalizedDisplayName.StringChanged += SetEquippedWeaponText;
            weapon.LocalizedDisplayName.RefreshString();
        }

        private void UnbindLocalizedVictoryWeaponName()
        {
            if (localizedVictoryWeaponData == null)
            {
                return;
            }

            if (localizedVictoryWeaponData.HasLocalizedDisplayName)
            {
                localizedVictoryWeaponData.LocalizedDisplayName.StringChanged -= SetEquippedWeaponText;
            }

            localizedVictoryWeaponData = null;
        }

        private void SetEquippedWeaponText(string value)
        {
            SetText(equippedWeaponText, value);
        }

        private void RefreshInputBlock()
        {
            GameModalState.BlocksGameplayInput = resultOpen;
        }

        private void FreezeGame()
        {
            if (Time.timeScale > 0f)
            {
                previousTimeScale = Time.timeScale;
            }

            Time.timeScale = 0f;
        }

        private void UnfreezeGame()
        {
            Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        }
    }
}
