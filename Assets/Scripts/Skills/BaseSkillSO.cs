using UnityEngine;

namespace Week14.Skills
{
    public abstract class BaseSkillSO : ScriptableObject
    {
        [Tooltip("스킬 고유 식별자입니다. 저장 데이터와 SkillDatabase에서 이 값으로 스킬을 찾습니다.")]
        [SerializeField] private string skillId;
        [Tooltip("스킬을 발동하기 위해 필요한 스택 수입니다. 스택 최대치로도 사용됩니다.")]
        [SerializeField, Min(1)] private int requiredStack = 1;

        public string SkillId => skillId;
        public int RequiredStack => requiredStack;

        public abstract void Execute(GameObject user);
    }
}
