using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using Week14.Save;
using Week14.UI;

namespace Week14.Audio
{
    public sealed class SoundManager : MonoBehaviour
    {
        private const int InitialSfxSourceCount = 8;

        public sealed class SfxPlaybackHandle
        {
            internal SfxPlaybackHandle(AudioSource source, int playbackId)
            {
                Source = source;
                PlaybackId = playbackId;
                InitialVolume = source != null ? source.volume : 0f;
            }

            internal AudioSource Source { get; set; }
            internal int PlaybackId { get; }
            internal float InitialVolume { get; }
            public bool IsPlaying => IsSfxPlaying(this);
        }

        [SerializeField] private SoundLibrary library;
        [SerializeField] private AudioMixerGroup bgmOutput;
        [SerializeField] private AudioMixerGroup sfxOutput;
        [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.7f;
        [SerializeField] private bool bgmMuted;
        [SerializeField] private bool sfxMuted;

        private static SoundManager instance;
        private static string pendingBgmId;
        private static float pendingBgmFadeSeconds;

        private AudioSource bgmSource;
        private readonly List<AudioSource> sfxSources = new();
        private readonly Dictionary<AudioSource, int> sfxPlaybackIds = new();
        private readonly HashSet<AudioSource> bossSfxSources = new();
        private readonly HashSet<AudioSource> pausedBossSfxSources = new();
        private bool bossSfxPaused;
        private bool bossSfxPlaybackBlocked;
        private int nextSfxPlaybackId;
        private int lastSfxPlaybackFrame = -1;
        private string lastSfxId;
        private Coroutine bgmRoutine;
        private string currentBgmId;
        private float currentBgmEntryVolume = 1f;

        public static SoundManager Instance => instance;

        public static float BgmVolume => instance != null ? instance.bgmVolume : 0.7f;
        public static float SfxVolume => instance != null ? instance.sfxVolume : 0.7f;
        public static bool IsBgmMuted => instance != null && instance.bgmMuted;
        public static bool IsSfxMuted => instance != null && instance.sfxMuted;
        public static bool WasSfxPlayedThisFrame(string id)
        {
            return instance != null
                && instance.lastSfxPlaybackFrame == Time.frameCount
                && string.Equals(instance.lastSfxId, id, System.StringComparison.Ordinal);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;

            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
            bgmSource.outputAudioMixerGroup = bgmOutput;

            for (int i = 0; i < InitialSfxSourceCount; i++)
            {
                sfxSources.Add(CreateSfxSource());
            }

            bgmVolume = SettingsManager.BgmVolume;
            sfxVolume = SettingsManager.SfxVolume;
            bgmMuted = SettingsManager.BgmMuted;
            sfxMuted = SettingsManager.SfxMuted;
            RefreshBgmSourceVolume();

            if (pendingBgmId != null)
            {
                string id = pendingBgmId;
                float fadeSeconds = pendingBgmFadeSeconds;
                pendingBgmId = null;
                PlayBgm(id, fadeSeconds);
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
                instance = null;
            }
        }

        private void Update()
        {
            RefreshBossSfxPauseState();
        }

        private void OnValidate()
        {
            RefreshBgmSourceVolume();
        }

        public static void PlayBgm(string id, float fadeSeconds = 0.5f)
        {
            if (instance == null)
            {
                pendingBgmId = id;
                pendingBgmFadeSeconds = fadeSeconds;
                return;
            }

            if (instance.library == null)
            {
                return;
            }

            SoundLibrary.SoundEntry entry = instance.library.FindBgm(id);
            if (entry == null || entry.Clip == null)
            {
                Debug.LogWarning($"{nameof(SoundManager)}: BGM id '{id}' not found.");
                return;
            }

            instance.PlayBgmInternal(id, entry, fadeSeconds);
        }

        public static void StopBgm(float fadeSeconds = 0.5f)
        {
            instance?.StopBgmInternal(fadeSeconds);
        }

        public static void PlaySfx(string id)
        {
            if (instance == null || instance.library == null)
            {
                return;
            }

            SoundLibrary.SoundEntry entry = instance.library.FindSfx(id);
            if (entry == null || entry.Clip == null)
            {
                Debug.LogWarning($"{nameof(SoundManager)}: SFX id '{id}' not found.");
                return;
            }

            if (instance.PlaySfxInternal(entry.Clip, entry.Volume, entry.Pitch) != null)
            {
                instance.RecordSfxPlayback(id);
            }
        }

        public static void PlaySfx(string id, float pitch)
        {
            if (instance == null || instance.library == null)
            {
                return;
            }

            SoundLibrary.SoundEntry entry = instance.library.FindSfx(id);
            if (entry == null || entry.Clip == null)
            {
                Debug.LogWarning($"{nameof(SoundManager)}: SFX id '{id}' not found.");
                return;
            }

            if (instance.PlaySfxInternal(entry.Clip, entry.Volume, pitch) != null)
            {
                instance.RecordSfxPlayback(id);
            }
        }

        public static void PlaySfx(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (instance == null || clip == null)
            {
                return;
            }

            instance.PlaySfxInternal(clip, volume, pitch);
        }

        public static SfxPlaybackHandle PlayLoopingSfx(string id)
        {
            return PlaySfxWithHandle(id, true);
        }

        public static SfxPlaybackHandle PlayTrackedSfx(string id)
        {
            return PlaySfxWithHandle(id, false);
        }

        public static SfxPlaybackHandle PlayBossSfx(string id)
        {
            return PlaySfxWithHandle(id, false, true);
        }

        public static SfxPlaybackHandle PlayBossLoopingSfx(string id)
        {
            return PlaySfxWithHandle(id, true, true);
        }

        private static SfxPlaybackHandle PlaySfxWithHandle(
            string id,
            bool loop,
            bool bossScoped = false)
        {
            if (instance == null || instance.library == null)
            {
                return null;
            }

            SoundLibrary.SoundEntry entry = instance.library.FindSfx(id);
            if (entry == null || entry.Clip == null)
            {
                Debug.LogWarning($"{nameof(SoundManager)}: SFX id '{id}' not found.");
                return null;
            }

            AudioSource source = instance.PlaySfxInternal(
                entry.Clip,
                entry.Volume,
                entry.Pitch,
                loop,
                bossScoped);
            if (source == null
                || !instance.sfxPlaybackIds.TryGetValue(source, out int playbackId))
            {
                return null;
            }

            instance.RecordSfxPlayback(id);
            return new SfxPlaybackHandle(source, playbackId);
        }

        public static void StopSfx(SfxPlaybackHandle handle)
        {
            if (handle == null)
            {
                return;
            }

            AudioSource source = handle.Source;
            handle.Source = null;
            if (source == null
                || instance == null
                || !instance.sfxPlaybackIds.TryGetValue(source, out int playbackId)
                || playbackId != handle.PlaybackId)
            {
                return;
            }

            source.Stop();
            source.loop = false;
            source.clip = null;
            source.volume = 0f;
            source.pitch = 1f;
            instance.sfxPlaybackIds.Remove(source);
            instance.bossSfxSources.Remove(source);
            instance.pausedBossSfxSources.Remove(source);
        }

        public static void SetSfxVolumeScale(SfxPlaybackHandle handle, float volumeScale)
        {
            if (handle?.Source == null || instance == null)
            {
                return;
            }

            AudioSource source = handle.Source;
            if (!instance.sfxPlaybackIds.TryGetValue(source, out int playbackId)
                || playbackId != handle.PlaybackId)
            {
                return;
            }

            source.volume = handle.InitialVolume * Mathf.Clamp01(volumeScale);
        }

        public static void StopAllBossSfx()
        {
            instance?.StopAllBossSfxInternal(true);
        }

        private static bool IsSfxPlaying(SfxPlaybackHandle handle)
        {
            if (handle?.Source == null || instance == null)
            {
                return false;
            }

            AudioSource source = handle.Source;
            return (source.isPlaying || instance.pausedBossSfxSources.Contains(source))
                && instance.sfxPlaybackIds.TryGetValue(source, out int playbackId)
                && playbackId == handle.PlaybackId;
        }

        public static void PlaySfxAtPoint(string id, Vector3 position)
        {
            if (instance == null || instance.library == null)
            {
                return;
            }

            SoundLibrary.SoundEntry entry = instance.library.FindSfx(id);
            if (entry == null || entry.Clip == null)
            {
                Debug.LogWarning($"{nameof(SoundManager)}: SFX id '{id}' not found.");
                return;
            }

            if (instance.sfxMuted)
            {
                return;
            }

            AudioSource.PlayClipAtPoint(entry.Clip, position, entry.Volume * instance.sfxVolume);
        }

        public static void SetBgmVolume(float volume)
        {
            if (instance == null)
            {
                return;
            }

            instance.bgmVolume = Mathf.Clamp(volume, 0f, 2f);
            instance.RefreshBgmSourceVolume();
            SettingsManager.SetBgmVolume(instance.bgmVolume);
        }

        public static void SetSfxVolume(float volume)
        {
            if (instance == null)
            {
                return;
            }

            instance.sfxVolume = Mathf.Clamp(volume, 0f, 2f);
            SettingsManager.SetSfxVolume(instance.sfxVolume);
        }

        public static void SetBgmMuted(bool muted)
        {
            if (instance == null)
            {
                return;
            }

            instance.bgmMuted = muted;
            instance.RefreshBgmSourceVolume();
            SettingsManager.SetBgmMuted(muted);
        }

        public static void SetSfxMuted(bool muted)
        {
            if (instance == null)
            {
                return;
            }

            instance.sfxMuted = muted;
            SettingsManager.SetSfxMuted(muted);
        }

        private void RefreshBgmSourceVolume()
        {
            if (bgmSource == null)
            {
                return;
            }

            bgmSource.volume = bgmMuted ? 0f : currentBgmEntryVolume * bgmVolume;
        }

        private void PlayBgmInternal(string id, SoundLibrary.SoundEntry entry, float fadeSeconds)
        {
            if (currentBgmId == id && bgmSource.isPlaying)
            {
                return;
            }

            currentBgmId = id;

            if (bgmRoutine != null)
            {
                StopCoroutine(bgmRoutine);
            }

            bgmRoutine = StartCoroutine(CrossfadeBgm(entry.Clip, entry.Volume, Mathf.Max(0f, fadeSeconds)));
        }

        private void StopBgmInternal(float fadeSeconds)
        {
            currentBgmId = null;

            if (bgmRoutine != null)
            {
                StopCoroutine(bgmRoutine);
            }

            bgmRoutine = StartCoroutine(FadeOutAndStop(Mathf.Max(0f, fadeSeconds)));
        }

        private IEnumerator CrossfadeBgm(AudioClip clip, float entryVolume, float fadeSeconds)
        {
            float halfFade = fadeSeconds * 0.5f;

            if (halfFade > 0f && bgmSource.isPlaying)
            {
                float startVolume = bgmSource.volume;
                for (float t = 0f; t < halfFade; t += Time.unscaledDeltaTime)
                {
                    bgmSource.volume = Mathf.Lerp(startVolume, 0f, t / halfFade);
                    yield return null;
                }
            }

            bgmSource.clip = clip;
            bgmSource.volume = 0f;
            bgmSource.Play();

            currentBgmEntryVolume = entryVolume;
            float targetVolume = bgmMuted ? 0f : entryVolume * bgmVolume;
            if (halfFade > 0f)
            {
                for (float t = 0f; t < halfFade; t += Time.unscaledDeltaTime)
                {
                    bgmSource.volume = Mathf.Lerp(0f, targetVolume, t / halfFade);
                    yield return null;
                }
            }

            bgmSource.volume = targetVolume;
            bgmRoutine = null;
        }

        private IEnumerator FadeOutAndStop(float fadeSeconds)
        {
            float startVolume = bgmSource.volume;
            for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
            {
                bgmSource.volume = Mathf.Lerp(startVolume, 0f, t / fadeSeconds);
                yield return null;
            }

            bgmSource.Stop();
            bgmSource.volume = 0f;
            bgmRoutine = null;
        }

        private AudioSource PlaySfxInternal(
            AudioClip clip,
            float entryVolume,
            float pitch,
            bool loop = false,
            bool bossScoped = false)
        {
            if (sfxMuted || (bossScoped && ShouldPauseBossSfx()))
            {
                return null;
            }

            AudioSource source = GetAvailableSfxSource();
            bossSfxSources.Remove(source);
            pausedBossSfxSources.Remove(source);
            if (bossScoped)
            {
                bossSfxSources.Add(source);
            }

            source.clip = clip;
            source.volume = Mathf.Clamp(entryVolume, 0f, 2f) * sfxVolume;
            source.pitch = pitch;
            source.loop = loop;
            nextSfxPlaybackId = nextSfxPlaybackId == int.MaxValue
                ? 1
                : nextSfxPlaybackId + 1;
            sfxPlaybackIds[source] = nextSfxPlaybackId;
            source.Play();
            return source;
        }

        private void RecordSfxPlayback(string id)
        {
            lastSfxPlaybackFrame = Time.frameCount;
            lastSfxId = id;
        }

        private AudioSource GetAvailableSfxSource()
        {
            for (int i = 0; i < sfxSources.Count; i++)
            {
                AudioSource source = sfxSources[i];
                if (!source.isPlaying
                    && !(bossSfxPaused && bossSfxSources.Contains(source)))
                {
                    return source;
                }
            }

            AudioSource newSource = CreateSfxSource();
            sfxSources.Add(newSource);
            return newSource;
        }

        private AudioSource CreateSfxSource()
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.outputAudioMixerGroup = sfxOutput;
            return source;
        }

        private bool ShouldPauseBossSfx()
        {
            return bossSfxPlaybackBlocked
                || (GameModalState.BlocksGameplayInput
                    && Mathf.Approximately(Time.timeScale, 0f));
        }

        private void RefreshBossSfxPauseState()
        {
            bool shouldPause = ShouldPauseBossSfx();
            if (bossSfxPaused == shouldPause)
            {
                return;
            }

            bossSfxPaused = shouldPause;
            if (shouldPause)
            {
                pausedBossSfxSources.Clear();
                foreach (AudioSource source in bossSfxSources)
                {
                    if (source != null && source.isPlaying)
                    {
                        source.Pause();
                        pausedBossSfxSources.Add(source);
                    }
                }

                return;
            }

            foreach (AudioSource source in pausedBossSfxSources)
            {
                source?.UnPause();
            }

            pausedBossSfxSources.Clear();
        }

        private void StopAllBossSfxInternal(bool blockNewPlayback)
        {
            foreach (AudioSource source in bossSfxSources)
            {
                if (source == null)
                {
                    continue;
                }

                source.Stop();
                source.loop = false;
                source.clip = null;
                source.volume = 0f;
                source.pitch = 1f;
                sfxPlaybackIds.Remove(source);
            }

            bossSfxSources.Clear();
            pausedBossSfxSources.Clear();
            bossSfxPaused = false;
            bossSfxPlaybackBlocked = blockNewPlayback;
        }

        private void HandleActiveSceneChanged(Scene _, Scene __)
        {
            StopAllBossSfxInternal(false);
        }
    }
}
