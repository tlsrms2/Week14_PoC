using System.Collections.Generic;
using UnityEngine;
using Week14.Skills;
using Week14.UI;
using Week14.Weapons;

namespace Week14.Save
{
    // Assets/Resources/GameSaveConfig.asset 이름으로 배치해두면 GameSaveManager가 자동으로 읽어옵니다.
    [CreateAssetMenu(menuName = "Week14/Save/Game Save Config", fileName = "GameSaveConfig")]
    public sealed class GameSaveConfigSO : ScriptableObject
    {
        [Tooltip("게임을 처음 시작했을 때(또는 세이브 파일을 로드할 때마다) 기본으로 해금되어 있어야 하는 보스 목록입니다. 비워두면 첫 번째 보스(Id \"1\")를 대신 해금합니다.")]
        [SerializeField] private List<BossData> defaultUnlockedBosses = new();
        [Tooltip("게임을 처음 시작했을 때 자동으로 해금 + 무료 구매되는 총기 목록입니다.")]
        [SerializeField] private List<BaseWeaponSO> defaultUnlockedWeapons = new();
        [Tooltip("게임을 처음 시작했을 때 자동으로 해금 + 무료 구매되는 액티브 스킬 목록입니다.")]
        [SerializeField] private List<BaseSkillSO> defaultUnlockedSkills = new();
        [Tooltip("게임을 처음 시작했을 때 자동으로 해금 + 무료 구매되는 패시브 스킬 목록입니다.")]
        [SerializeField] private List<BasePassiveSkillSO> defaultUnlockedPassiveSkills = new();

        public IReadOnlyList<BossData> DefaultUnlockedBosses => defaultUnlockedBosses;
        public IReadOnlyList<BaseWeaponSO> DefaultUnlockedWeapons => defaultUnlockedWeapons;
        public IReadOnlyList<BaseSkillSO> DefaultUnlockedSkills => defaultUnlockedSkills;
        public IReadOnlyList<BasePassiveSkillSO> DefaultUnlockedPassiveSkills => defaultUnlockedPassiveSkills;
    }
}
