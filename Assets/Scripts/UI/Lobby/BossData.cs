using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace Week14.UI
{
    [CreateAssetMenu(fileName = "BossData", menuName = "Week14/Boss Data")]
    public sealed class BossData : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string bossName;
        [SerializeField] private string crime;
        [SerializeField, TextArea] private string description;
        [SerializeField] private LocalizedString localizedBossName;
        [SerializeField] private LocalizedString localizedCrime;
        [SerializeField] private LocalizedString localizedDescription;
        [SerializeField] private Sprite icon;
        [SerializeField] private Sprite deathImage;
        [Tooltip("로비 보스 패널의 보스슬롯에 표시할 이미지입니다. 비워두면 icon을 대신 사용합니다.")]
        [SerializeField] private Sprite panelIcon;
        [SerializeField] private string sceneName;

        [Header("클리어 보상")]
        [Tooltip("이 보스를 클리어하면 추가로 해금되는 보스 ID 목록입니다.")]
        [SerializeField] private List<string> unlocksBossIds = new();
        [Tooltip("이 보스를 클리어하면 해금되는 액티브 스킬 ID 목록입니다.")]
        [SerializeField] private List<string> unlocksSkillIds = new();
        [Tooltip("이 보스를 클리어하면 해금되는 패시브 스킬 ID 목록입니다.")]
        [SerializeField] private List<string> unlocksPassiveSkillIds = new();
        [Tooltip("이 보스를 클리어하면 해금되는 총기 ID 목록입니다.")]
        [SerializeField] private List<string> unlocksWeaponIds = new();

        public string Id => id;
        public string BossName => bossName;
        public string Crime => crime;
        public string Description => description;
        public LocalizedString LocalizedBossName => localizedBossName;
        public LocalizedString LocalizedCrime => localizedCrime;
        public LocalizedString LocalizedDescription => localizedDescription;
        public bool HasLocalizedBossName => HasLocalizedString(localizedBossName);
        public bool HasLocalizedCrime => HasLocalizedString(localizedCrime);
        public bool HasLocalizedDescription => HasLocalizedString(localizedDescription);
        public Sprite Icon => icon;
        public Sprite DeathImage => deathImage;
        public Sprite ResultPortrait => deathImage != null ? deathImage : icon;
        public Sprite PanelIcon => panelIcon != null ? panelIcon : icon;
        public string SceneName => sceneName;
        public IReadOnlyList<string> UnlocksBossIds => unlocksBossIds;
        public IReadOnlyList<string> UnlocksSkillIds => unlocksSkillIds;
        public IReadOnlyList<string> UnlocksPassiveSkillIds => unlocksPassiveSkillIds;
        public IReadOnlyList<string> UnlocksWeaponIds => unlocksWeaponIds;

        private static bool HasLocalizedString(LocalizedString value)
        {
            return value != null
                && value.TableReference.ReferenceType != TableReference.Type.Empty
                && value.TableEntryReference.ReferenceType != TableEntryReference.Type.Empty;
        }
    }
}
