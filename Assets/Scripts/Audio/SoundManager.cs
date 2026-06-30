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

        public static void PlaySfx(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (instance == null || clip == null)
            {
                return;
            }

            instance.PlaySfxInternal(clip, volume, pitch);
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

        private void PlaySfxInternal(AudioClip clip, float entryVolume, float pitch)
        {
            if (sfxMuted)
            {
                return;
            }

            AudioSource source = GetAvailableSfxSource();
            source.clip = clip;
            source.volume = Mathf.Clamp(entryVolume, 0f, 2f) * sfxVolume;
            source.pitch = pitch;
            source.Play();
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
