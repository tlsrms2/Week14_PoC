using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using Week14.Save;

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

        [Header("보스전 진입 연출")]
        [Tooltip("플레이어가 걷기 시작할 월드 좌표입니다.")]
        [SerializeField] private Vector2 introWalkStartPosition;
        [Tooltip("플레이어가 걷기를 마치고 전투를 시작할 월드 좌표입니다.")]
        [SerializeField] private Vector2 introWalkEndPosition;

        [Header("클리어 보상")]
        [Tooltip("이 보스를 클리어하면 추가로 해금되는 보스 ID 목록입니다.")]
        [SerializeField] private List<string> unlocksBossIds = new();
        [Tooltip("이 보스를 클리어하면 해금되는 액티브 스킬 ID 목록입니다.")]
        [SerializeField] private List<string> unlocksSkillIds = new();
        [Tooltip("이 보스를 클리어하면 해금되는 패시브 스킬 ID 목록입니다.")]
        [SerializeField] private List<string> unlocksPassiveSkillIds = new();
        [Tooltip("이 보스를 클리어하면 해금되는 총기 ID 목록입니다.")]
        [SerializeField] private List<string> unlocksWeaponIds = new();

        [Header("해금 조건")]
        [Tooltip("0보다 크면, 처치한 보스 '종류' 수(중복 제거)가 이 값 이상일 때 이 보스가 자동으로 해금됩니다. " +
            "다른 보스의 unlocksBossIds 체이닝 해금과는 별개로 추가 적용됩니다(둘 중 하나만 만족해도 해금).")]
        [SerializeField] private int requiredDistinctBossKills = 0;

        [Header("엔딩")]
        [Tooltip("이 보스가 최종보스인지 표시합니다.")]
        [SerializeField] private bool isFinalBoss;

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
        public Vector2 IntroWalkStartPosition => introWalkStartPosition;
        public Vector2 IntroWalkEndPosition => introWalkEndPosition;
        public IReadOnlyList<string> UnlocksBossIds => unlocksBossIds;
        public IReadOnlyList<string> UnlocksSkillIds => unlocksSkillIds;
        public IReadOnlyList<string> UnlocksPassiveSkillIds => unlocksPassiveSkillIds;
        public IReadOnlyList<string> UnlocksWeaponIds => unlocksWeaponIds;
        public int RequiredDistinctBossKills => requiredDistinctBossKills;
        public bool IsFinalBoss => isFinalBoss;

        // 명시적으로 해금(unlocksBossIds 체이닝 등)되었거나, 처치한 보스 종류 수가 조건을 만족하면 해금된 것으로 취급합니다.
        public bool IsUnlocked()
        {
            return GameSaveManager.IsUnlocked(id)
                || (requiredDistinctBossKills > 0 && GameSaveManager.ClearedBossCount >= requiredDistinctBossKills);
        }

        private static bool HasLocalizedString(LocalizedString value)
        {
            return value != null
                && value.TableReference.ReferenceType != TableReference.Type.Empty
                && value.TableEntryReference.ReferenceType != TableEntryReference.Type.Empty;
        }
    }
}
