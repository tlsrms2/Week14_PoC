using System;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Audio
{
    [CreateAssetMenu(menuName = "Week14/Audio/Sound Library", fileName = "SoundLibrary")]
    public sealed class SoundLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class SoundEntry
        {
            [Tooltip("SoundManager.PlaySfx/PlayBgm 호출 시 사용하는 식별자입니다.")]
            [SerializeField] private string id;
            [SerializeField] private AudioClip clip;
            [SerializeField, Range(0f, 2f)] private float volume = 1f;
            [SerializeField, Range(0.5f, 2f)] private float pitch = 1f;

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

        private static SoundEntry Find(Dictionary<string, SoundEntry> lookup, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            return lookup.TryGetValue(id, out SoundEntry entry) ? entry : null;
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
    }
}
