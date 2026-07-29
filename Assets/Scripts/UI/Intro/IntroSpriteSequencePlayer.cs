using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Week14.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class IntroSpriteSequencePlayer : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Image targetImage;

        [Header("Frames")]
        [SerializeField] private Texture2D sourceTexture;
        [SerializeField] private bool autoPopulateFramesFromTexture = true;
        [SerializeField] private bool previewFirstFrameInEditMode = true;
        [SerializeField] private Sprite[] frames = Array.Empty<Sprite>();

        [Header("Playback")]
        [SerializeField, Min(0.01f)] private float framesPerSecond = 24f;
        [SerializeField, Min(0f)] private float startDelaySeconds;
        [SerializeField] private bool playOnEnable;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private bool loop;
        [SerializeField] private bool holdLastFrame = true;
        [SerializeField] private bool restoreFirstFrameOnDisable;
        [SerializeField] private bool setNativeSizeOnFrameChange;

        [Header("Events")]
        [SerializeField] private UnityEvent completed = new();

        private Coroutine playRoutine;
        private int currentFrameIndex = -1;

        public event Action Completed;

        public bool IsPlaying => playRoutine != null;
        public int FrameCount => frames?.Length ?? 0;
        public float DurationSeconds => FrameCount > 0 ? startDelaySeconds + FrameCount / Mathf.Max(0.01f, framesPerSecond) : startDelaySeconds;
        public float StartDelaySeconds => startDelaySeconds;
        public float FramesPerSecond => Mathf.Max(0.01f, framesPerSecond);

        private void Reset()
        {
            ResolveTargetImage();
        }

        private void Awake()
        {
            ResolveTargetImage();
        }

        private void OnEnable()
        {
            ResolveTargetImage();

            if (playOnEnable)
            {
                PlayFromStart();
            }
            else if (currentFrameIndex < 0)
            {
                SetFrame(0);
            }
        }

        private void OnDisable()
        {
            Stop();

            if (restoreFirstFrameOnDisable)
            {
                SetFrame(0);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveTargetImage();
            framesPerSecond = Mathf.Max(0.01f, framesPerSecond);
            startDelaySeconds = Mathf.Max(0f, startDelaySeconds);

            if (autoPopulateFramesFromTexture)
            {
                PopulateFramesFromTexture();
            }

            if (!Application.isPlaying && previewFirstFrameInEditMode)
            {
                SetFrame(0);
            }
        }
#endif

        public void PlayFromStart()
        {
            Play(resetToFirstFrame: true);
        }

        public void Play()
        {
            Play(resetToFirstFrame: false);
        }

        public void Stop()
        {
            if (playRoutine == null)
            {
                return;
            }

            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        public void SetFrame(int frameIndex)
        {
            if (FrameCount <= 0)
            {
                return;
            }

            int clampedIndex = Mathf.Clamp(frameIndex, 0, FrameCount - 1);
            Sprite frame = frames[clampedIndex];
            if (targetImage != null && frame != null)
            {
                targetImage.sprite = frame;
                targetImage.enabled = true;

                if (setNativeSizeOnFrameChange)
                {
                    targetImage.SetNativeSize();
                }
            }

            currentFrameIndex = clampedIndex;
        }

        public void SetNormalizedProgress(float progress)
        {
            if (FrameCount <= 0)
            {
                return;
            }

            int frameIndex = Mathf.RoundToInt(Mathf.Clamp01(progress) * (FrameCount - 1));
            SetFrame(frameIndex);
        }

        private void Play(bool resetToFirstFrame)
        {
            ResolveTargetImage();
            Stop();

            if (resetToFirstFrame)
            {
                SetFrame(0);
            }

            playRoutine = StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            if (FrameCount <= 0)
            {
                playRoutine = null;
                RaiseCompleted();
                yield break;
            }

            if (startDelaySeconds > 0f)
            {
                for (float elapsed = 0f; elapsed < startDelaySeconds; elapsed += GetDeltaTime())
                {
                    yield return null;
                }
            }

            do
            {
                yield return PlayOnce();
            }
            while (loop && isActiveAndEnabled);

            if (!holdLastFrame)
            {
                SetFrame(0);
            }

            playRoutine = null;
            RaiseCompleted();
        }

        private IEnumerator PlayOnce()
        {
            float duration = FrameCount / Mathf.Max(0.01f, framesPerSecond);
            for (float elapsed = 0f; elapsed < duration; elapsed += GetDeltaTime())
            {
                int frameIndex = Mathf.Min(
                    FrameCount - 1,
                    Mathf.FloorToInt(elapsed * framesPerSecond));
                SetFrame(frameIndex);
                yield return null;
            }

            SetFrame(FrameCount - 1);
        }

        private void ResolveTargetImage()
        {
            if (targetImage == null)
            {
                targetImage = GetComponent<Image>();
            }
        }

        private float GetDeltaTime()
        {
            return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }

        private void RaiseCompleted()
        {
            Completed?.Invoke();
            completed?.Invoke();
        }

#if UNITY_EDITOR
        private void PopulateFramesFromTexture()
        {
            if (sourceTexture == null)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(sourceTexture);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            List<Sprite> loadedFrames = new();
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite)
                {
                    loadedFrames.Add(sprite);
                }
            }

            if (loadedFrames.Count == 0)
            {
                Sprite singleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (singleSprite != null)
                {
                    loadedFrames.Add(singleSprite);
                }
            }

            loadedFrames.Sort(CompareSpritesByTrailingNumber);
            Sprite[] loadedArray = loadedFrames.ToArray();
            if (!FramesMatch(loadedArray))
            {
                frames = loadedArray;
            }
        }

        private bool FramesMatch(Sprite[] loadedFrames)
        {
            if (frames == null || loadedFrames == null || frames.Length != loadedFrames.Length)
            {
                return false;
            }

            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] != loadedFrames[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static int CompareSpritesByTrailingNumber(Sprite left, Sprite right)
        {
            if (left == right)
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            bool hasLeftNumber = TryGetTrailingNumber(left.name, out int leftNumber);
            bool hasRightNumber = TryGetTrailingNumber(right.name, out int rightNumber);
            if (hasLeftNumber && hasRightNumber && leftNumber != rightNumber)
            {
                return leftNumber.CompareTo(rightNumber);
            }

            return string.Compare(left.name, right.name, StringComparison.Ordinal);
        }

        private static bool TryGetTrailingNumber(string value, out int number)
        {
            number = 0;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            int endIndex = value.Length - 1;
            while (endIndex >= 0 && !char.IsDigit(value[endIndex]))
            {
                endIndex--;
            }

            if (endIndex < 0)
            {
                return false;
            }

            int startIndex = endIndex;
            while (startIndex >= 0 && char.IsDigit(value[startIndex]))
            {
                startIndex--;
            }

            string digits = value.Substring(startIndex + 1, endIndex - startIndex);
            return int.TryParse(digits, out number);
        }
#endif
    }
}
