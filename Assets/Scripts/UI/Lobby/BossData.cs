using System.Collections.Generic;
using UnityEngine;

namespace Week14.UI
{
    [CreateAssetMenu(fileName = "BossData", menuName = "Week14/Boss Data")]
    public sealed class BossData : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string bossName;
        [SerializeField] private string crime;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private string sceneName;

        [Header("클리어 보상")]
        [Tooltip("이 보스를 클리어하면 추가로 해금되는 보스 ID 목록입니다.")]
        [SerializeField] private List<string> unlocksBossIds = new();
        [Tooltip("이 보스를 클리어하면 해금되는 스킬 ID 목록입니다.")]
        [SerializeField] private List<string> unlocksSkillIds = new();
        [Tooltip("이 보스를 클리어하면 해금되는 총기 ID 목록입니다.")]
        [SerializeField] private List<string> unlocksWeaponIds = new();

        public string Id => id;
        public string BossName => bossName;
        public string Crime => crime;
        public string Description => description;
        public Sprite Icon => icon;
        public string SceneName => sceneName;
        public IReadOnlyList<string> UnlocksBossIds => unlocksBossIds;
        public IReadOnlyList<string> UnlocksSkillIds => unlocksSkillIds;
        public IReadOnlyList<string> UnlocksWeaponIds => unlocksWeaponIds;
    }
}
