using System;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Audio
{
    [CreateAssetMenu(menuName = "Week14/Audio/Sound Library", fileName = "SoundLibrary")]
    public sealed class SoundLibrary : ScriptableObject
    {
        public const string UncategorizedSfxCategory = "미분류";

        [Serializable]
        public sealed class SoundEntry
        {
            [SerializeField, HideInInspector] private string category;
            [SerializeField, HideInInspector] private List<SoundEvent> usages = new();
            [Tooltip("SoundManager.PlaySfx/PlayBgm 호출 시 사용하는 식별자입니다.")]
            [SerializeField] private string id;
            [SerializeField] private AudioClip clip;
            [SerializeField, Range(0f, 2f)] private float volume = 1f;
            [SerializeField, Range(0.5f, 2f)] private float pitch = 1f;

            public string Category => NormalizeSfxCategory(category);
            public IReadOnlyList<SoundEvent> Usages => usages;
            public string Id => id;
            public AudioClip Clip => clip;
            public float Volume => volume;
            public float Pitch => pitch;
        }

        [Header("Background Music")]
        [SerializeField] private List<SoundEntry> bgmEntries = new();

        [Header("Sound Effects")]
        [SerializeField] private List<SoundEntry> sfxEntries = new();

        private Dictionary<string, SoundEntry> bgmById;
        private Dictionary<string, SoundEntry> sfxById;
        private Dictionary<SoundEvent, SoundEntry> sfxByEvent;

        public IReadOnlyList<string> BgmIds => GetIds(bgmEntries);
        public IReadOnlyList<string> SfxIds => GetIds(sfxEntries);
        public IReadOnlyList<SoundEntry> SfxEntries => sfxEntries;

        public static string NormalizeSfxCategory(string category)
        {
            return string.IsNullOrWhiteSpace(category)
                ? UncategorizedSfxCategory
                : category.Trim();
        }

        public SoundEntry FindBgm(string id)
        {
            bgmById ??= BuildLookup(bgmEntries);
            return Find(bgmById, id);
        }

        public SoundEntry FindSfx(string id)
        {
            sfxById ??= BuildLookup(sfxEntries);
            return Find(sfxById, id);
        }

        public SoundEntry FindSfx(SoundId id)
        {
            return FindSfx(id.ToLibraryId());
        }

        public SoundEntry FindSfx(SoundEvent soundEvent)
        {
            sfxByEvent ??= BuildEventLookup(sfxEntries);
            return sfxByEvent.TryGetValue(soundEvent, out SoundEntry entry)
                ? entry
                : null;
        }

        private void OnEnable()
        {
            InvalidateLookups();
        }

        private void OnValidate()
        {
            InvalidateLookups();
        }

        private void InvalidateLookups()
        {
            bgmById = null;
            sfxById = null;
            sfxByEvent = null;
        }

        private static SoundEntry Find(Dictionary<string, SoundEntry> lookup, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            return lookup.TryGetValue(id, out SoundEntry entry) ? entry : null;
        }

        private static IReadOnlyList<string> GetIds(List<SoundEntry> entries)
        {
            List<string> ids = new(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && !string.IsNullOrEmpty(entries[i].Id))
                {
                    ids.Add(entries[i].Id);
                }
            }

            return ids;
        }

        private static Dictionary<string, SoundEntry> BuildLookup(List<SoundEntry> entries)
        {
            Dictionary<string, SoundEntry> lookup = new(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                SoundEntry entry = entries[i];
                if (entry != null && !string.IsNullOrEmpty(entry.Id))
                {
                    lookup[entry.Id] = entry;
                }
            }

            return lookup;
        }

        private static Dictionary<SoundEvent, SoundEntry> BuildEventLookup(
            List<SoundEntry> entries)
        {
            Dictionary<SoundEvent, SoundEntry> lookup = new();
            for (int i = 0; i < entries.Count; i++)
            {
                SoundEntry entry = entries[i];
                if (entry?.Usages == null)
                {
                    continue;
                }

                for (int usageIndex = 0; usageIndex < entry.Usages.Count; usageIndex++)
                {
                    lookup[entry.Usages[usageIndex]] = entry;
                }
            }

            return lookup;
        }
    }
}
