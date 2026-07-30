using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Week14.Save;

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
            }

            internal AudioSource Source { get; set; }
            internal int PlaybackId { get; }
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
        private int nextSfxPlaybackId;
        private int lastButtonClickSfxFrame = -1;
        private Coroutine bgmRoutine;
        private string currentBgmId;
        private float currentBgmEntryVolume = 1f;

        public static SoundManager Instance => instance;

        public static float BgmVolume => instance != null ? instance.bgmVolume : 0.7f;
        public static float SfxVolume => instance != null ? instance.sfxVolume : 0.7f;
        public static bool IsBgmMuted => instance != null && instance.bgmMuted;
        public static bool IsSfxMuted => instance != null && instance.sfxMuted;

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
                instance = null;
            }
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

            instance.PlaySfxInternal(entry.Clip, entry.Volume, entry.Pitch);
        }

        public static void PlaySfx(SoundId id)
        {
            PlaySfx(id.ToLibraryId());
        }

        public static void PlaySfx(SoundEvent soundEvent)
        {
            if (instance == null || instance.library == null)
            {
                return;
            }

            SoundLibrary.SoundEntry entry = instance.library.FindSfx(soundEvent);
            if (entry == null || entry.Clip == null)
            {
                return;
            }

            instance.PlaySfxInternal(entry.Clip, entry.Volume, entry.Pitch);
        }

        public static void PlayButtonClickSfx()
        {
            if (instance == null || instance.lastButtonClickSfxFrame == Time.frameCount)
            {
                return;
            }

            instance.lastButtonClickSfxFrame = Time.frameCount;
            PlaySfx(SoundEvent.UI_ButtonClick);
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

            instance.PlaySfxInternal(entry.Clip, entry.Volume, pitch);
        }

        public static void PlaySfx(SoundId id, float pitch)
        {
            PlaySfx(id.ToLibraryId(), pitch);
        }

        public static void PlaySfx(SoundEvent soundEvent, float pitch)
        {
            if (instance == null || instance.library == null)
            {
                return;
            }

            SoundLibrary.SoundEntry entry = instance.library.FindSfx(soundEvent);
            if (entry == null || entry.Clip == null)
            {
                return;
            }

            instance.PlaySfxInternal(entry.Clip, entry.Volume, pitch);
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

        public static SfxPlaybackHandle PlayLoopingSfx(SoundId id)
        {
            return PlayLoopingSfx(id.ToLibraryId());
        }

        public static SfxPlaybackHandle PlayLoopingSfx(SoundEvent soundEvent)
        {
            return PlaySfxWithHandle(soundEvent, true);
        }

        public static SfxPlaybackHandle PlayTrackedSfx(string id)
        {
            return PlaySfxWithHandle(id, false);
        }

        public static SfxPlaybackHandle PlayTrackedSfx(SoundId id)
        {
            return PlayTrackedSfx(id.ToLibraryId());
        }

        public static SfxPlaybackHandle PlayTrackedSfx(SoundEvent soundEvent)
        {
            return PlaySfxWithHandle(soundEvent, false);
        }

        private static SfxPlaybackHandle PlaySfxWithHandle(
            SoundEvent soundEvent,
            bool loop)
        {
            if (instance == null || instance.library == null)
            {
                return null;
            }

            SoundLibrary.SoundEntry entry = instance.library.FindSfx(soundEvent);
            return PlaySfxWithHandle(entry, loop);
        }

        private static SfxPlaybackHandle PlaySfxWithHandle(string id, bool loop)
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

            return PlaySfxWithHandle(entry, loop);
        }

        private static SfxPlaybackHandle PlaySfxWithHandle(
            SoundLibrary.SoundEntry entry,
            bool loop)
        {
            if (instance == null || entry == null || entry.Clip == null)
            {
                return null;
            }

            AudioSource source = instance.PlaySfxInternal(
                entry.Clip,
                entry.Volume,
                entry.Pitch,
                loop);
            if (source == null
                || !instance.sfxPlaybackIds.TryGetValue(source, out int playbackId))
            {
                return null;
            }

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
        }

        private static bool IsSfxPlaying(SfxPlaybackHandle handle)
        {
            if (handle?.Source == null || instance == null)
            {
                return false;
            }

            AudioSource source = handle.Source;
            return source.isPlaying
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

        public static void PlaySfxAtPoint(SoundId id, Vector3 position)
        {
            PlaySfxAtPoint(id.ToLibraryId(), position);
        }

        public static void PlaySfxAtPoint(SoundEvent soundEvent, Vector3 position)
        {
            if (instance == null || instance.library == null)
            {
                return;
            }

            SoundLibrary.SoundEntry entry = instance.library.FindSfx(soundEvent);
            if (entry == null || entry.Clip == null || instance.sfxMuted)
            {
                return;
            }

            AudioSource.PlayClipAtPoint(
                entry.Clip,
                position,
                entry.Volume * instance.sfxVolume);
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

        private AudioSource PlaySfxInternal(AudioClip clip, float entryVolume, float pitch, bool loop = false)
        {
            if (sfxMuted)
            {
                return null;
            }

            AudioSource source = GetAvailableSfxSource();
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

        private AudioSource GetAvailableSfxSource()
        {
            for (int i = 0; i < sfxSources.Count; i++)
            {
                if (!sfxSources[i].isPlaying)
                {
                    return sfxSources[i];
                }
            }

            AudioSource source = CreateSfxSource();
            sfxSources.Add(source);
            return source;
        }

        private AudioSource CreateSfxSource()
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.outputAudioMixerGroup = sfxOutput;
            return source;
        }
    }
}
