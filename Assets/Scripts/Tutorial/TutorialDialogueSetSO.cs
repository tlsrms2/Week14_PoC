using System;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Tutorial
{
    public enum TutorialStepId
    {
        Intro,
        Move,
        Attack,
        Parry,
        Skill,
        Duel,
        Complete,
        BulletTimeout,
        SkillGauge,
        SkillRetry,
        Shoot
    }

    [Serializable]
    public sealed class TutorialDialogueLine
    {
        [SerializeField] private string speaker;
        [SerializeField, TextArea] private string text;

        public TutorialDialogueLine(string speaker, string text)
        {
            this.speaker = speaker;
            this.text = text;
        }

        public string Speaker => speaker;
        public string Text => text;
    }

    [Serializable]
    public sealed class TutorialStepContent
    {
        [SerializeField] private TutorialStepId step;
        [SerializeField] private List<TutorialDialogueLine> dialogues = new();
        [SerializeField] private string objectiveFormat;

        public TutorialStepContent(TutorialStepId step, string objectiveFormat, params TutorialDialogueLine[] dialogues)
        {
            this.step = step;
            this.objectiveFormat = objectiveFormat;
            this.dialogues = dialogues != null ? new List<TutorialDialogueLine>(dialogues) : new List<TutorialDialogueLine>();
        }

        public TutorialStepId Step => step;
        public IReadOnlyList<TutorialDialogueLine> Dialogues => dialogues;
        public string ObjectiveFormat => objectiveFormat;
    }

    [CreateAssetMenu(menuName = "Week14/Tutorial/Dialogue Set", fileName = "TutorialDialogueSet")]
    public sealed class TutorialDialogueSetSO : ScriptableObject
    {
        [SerializeField] private List<TutorialStepContent> steps = new()
        {
            new TutorialStepContent(
                TutorialStepId.Intro,
                string.Empty,
                new TutorialDialogueLine("이온", "AI, 테러에 대해 조사해줘. 공식 루트 말고, 네가 접근할 수 있는 쪽까지."),
                new TutorialDialogueLine("AI", "비공식 임무는 추가 비용이 발생합니다. 제공 가능한 정보도 제한됩니다."),
                new TutorialDialogueLine("이온", "상관없어. 이제 돈 쓸 데도 별로 없잖아."),
                new TutorialDialogueLine("AI", "확인했습니다. 조사와 병행해 사이보그 장비 적응 훈련을 시작합니다.")),
            new TutorialStepContent(
                TutorialStepId.Move,
                "이동 훈련 {0}/{1}",
                new TutorialDialogueLine("AI", "먼저 기본 기동입니다. WASD로 이동 입력을 확인하겠습니다.")),
            new TutorialStepContent(
                TutorialStepId.Shoot,
                "기본 사격 {0}/{1}",
                new TutorialDialogueLine("AI", "탄환형 에너지 코어는 현재 장비의 체력입니다. 남은 코어가 없을 때 피격되면 사망합니다."),
                new TutorialDialogueLine("AI", "왼클릭으로 코어를 한 발 소모해 사격합니다. 먼저 한 번 쏴서 코어 소모를 확인하십시오.")),
            new TutorialStepContent(
                TutorialStepId.Attack,
                "기본 공격 적중 {0}/{1}",
                new TutorialDialogueLine("AI", "탄환형 에너지 코어는 적의 공격을 막는 보호막이자 사격 자원입니다."),
                new TutorialDialogueLine("AI", "사격하면 코어가 줄어듭니다. 더미에게 기본 공격을 적중시키십시오.")),
            new TutorialStepContent(
                TutorialStepId.Parry,
                "탄환 요격 {0}/{1}",
                new TutorialDialogueLine("AI", "기계화 안구가 적 탄환을 포착하면 기계팔이 요격 궤도를 보조합니다."),
                new TutorialDialogueLine("AI", "정확히 패링하면 코어를 소모하지 않고, 무력화된 탄환 에너지로 코어를 보충할 수 있습니다.")),
            new TutorialStepContent(
                TutorialStepId.Skill,
                "스킬 회피 {0}/{1}",
                new TutorialDialogueLine("AI", "코어를 오래 보유하면 과부하로 자동 배출됩니다. 맞거나 사격해도 코어는 줄어듭니다."),
                new TutorialDialogueLine("AI", "스페이스바 스킬로 탄환을 회피하며 자원 관리를 유지하십시오.")),
            new TutorialStepContent(
                TutorialStepId.Duel,
                "훈련 몹 격파 {0}/{1}",
                new TutorialDialogueLine("AI", "코어가 없는 상태에서 피격되면 사망합니다. 통증 피드백은 차단되지 않습니다."),
                new TutorialDialogueLine("AI", "마지막으로 더미 몬스터와 대전합니다. 공격, 패링, 스킬을 모두 활용하십시오.")),
            new TutorialStepContent(
                TutorialStepId.Complete,
                string.Empty,
                new TutorialDialogueLine("AI", "적응 훈련이 완료되었습니다. 로비로 복귀해 다음 준비를 진행해주십시오."))
        };

        public TutorialStepContent GetStep(TutorialStepId step)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i] != null && steps[i].Step == step)
                {
                    return steps[i];
                }
            }

            return null;
        }

        public TutorialStepContent GetStepOrDefault(TutorialStepId step)
        {
            TutorialStepContent content = GetStep(step);
            if (content != null)
            {
                return content;
            }

            return GetDefaultStep(step);
        }

        public static TutorialStepContent GetDefaultStep(TutorialStepId step)
        {
            return step switch
            {
                TutorialStepId.Shoot => new TutorialStepContent(
                    TutorialStepId.Shoot,
                    "기본 사격 {0}/{1}",
                    new TutorialDialogueLine("AI", "탄환형 에너지 코어는 현재 장비의 체력입니다. 남은 코어가 없을 때 피격되면 사망합니다."),
                    new TutorialDialogueLine("AI", "왼클릭으로 코어를 한 발 소모해 사격합니다. 먼저 한 번 쏴서 코어 소모를 확인하십시오.")),
                TutorialStepId.BulletTimeout => new TutorialStepContent(
                    TutorialStepId.BulletTimeout,
                    string.Empty,
                    new TutorialDialogueLine("AI", "지금부터 보충된 에너지 코어에는 유통기한이 적용됩니다."),
                    new TutorialDialogueLine("AI", "코어를 오래 보유하면 과부하되어 자동으로 배출됩니다. 표시가 붉어지면 곧 사라진다는 뜻입니다.")),
                TutorialStepId.SkillGauge => new TutorialStepContent(
                    TutorialStepId.SkillGauge,
                    string.Empty,
                    new TutorialDialogueLine("AI", "스킬은 전용 게이지가 준비되어 있을 때만 사용할 수 있습니다."),
                    new TutorialDialogueLine("AI", "게이지가 비어 있다면 일정 시간이 지나 다시 충전됩니다. 지금은 훈련을 위해 즉시 충전해 두겠습니다.")),
                TutorialStepId.SkillRetry => new TutorialStepContent(
                    TutorialStepId.SkillRetry,
                    string.Empty,
                    new TutorialDialogueLine("AI", "회피 실패입니다. 코어와 스킬 게이지를 다시 충전했습니다."),
                    new TutorialDialogueLine("AI", "탄막이 퍼지는 순간 스페이스바로 스킬을 사용해 다시 회피하십시오.")),
                _ => null
            };
        }
    }
}
