using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using Week14.Save;

namespace Week14.Challenge
{
    public abstract class ChallengeDefinitionSO : ScriptableObject
    {
        [Tooltip("챌린지 고유 식별자입니다. 저장 데이터에서 이 값으로 완료 여부를 기록합니다.")]
        [SerializeField] private string challengeId;
        [Tooltip("UI에 표시할 챌린지 설명입니다. {0}처럼 자리표시자를 넣으면 LocalizedDescriptionArguments 값으로 채워집니다.")]
        [SerializeField, TextArea] private string description;
        [SerializeField] private LocalizedString localizedDescription;
        [Tooltip("달성 시 지급되는 챌린지 포인트입니다.")]
        [SerializeField] private int rewardPoint;
        [Tooltip("켜져있으면 UI에 (현재 진행도/최대 진행도)를 표시합니다.")]
        [SerializeField] private bool showProgress;

        public string ChallengeId => challengeId;
        public string Description => FormatDescription(description, LocalizedDescriptionArguments);
        public LocalizedString LocalizedDescription => localizedDescription;
        public bool HasLocalizedDescription => HasLocalizedString(localizedDescription);
        public int RewardPoint => rewardPoint;
        public bool ShowProgress => showProgress;

        // 목표 시간처럼 SO 설정값이 바뀔 때마다 로컬라이징 테이블 문구를 같이 고쳐야 하는 걸 막기 위한 자리표시자 인자입니다.
        // 설명 텍스트(플레인/로컬라이즈 둘 다)에 {0}, {1}... 형태로 값을 꽂아 넣고 싶은 챌린지 타입만 오버라이드하면 됩니다.
        public virtual object[] LocalizedDescriptionArguments => Array.Empty<object>();

        public abstract ChallengeType Kind { get; }

        public abstract ChallengeRunState CreateRunState();

        // 기본값 1(단발성 챌린지). 누적 횟수형 챌린지는 목표 횟수로 오버라이드합니다.
        public virtual int MaxProgress => 1;

        public virtual int GetCurrentProgress(string bossId)
        {
            string saveKey = GameSaveManager.BuildChallengeSaveKey(bossId, challengeId);
            return GameSaveManager.IsChallengeCompleted(saveKey) ? 1 : 0;
        }

        private static bool HasLocalizedString(LocalizedString value)
        {
            return value != null
                && value.TableReference.ReferenceType != TableReference.Type.Empty
                && value.TableEntryReference.ReferenceType != TableEntryReference.Type.Empty;
        }

        private static string FormatDescription(string format, object[] arguments)
        {
            if (string.IsNullOrEmpty(format) || arguments == null || arguments.Length == 0)
            {
                return format;
            }

            try
            {
                return string.Format(format, arguments);
            }
            catch (FormatException)
            {
                return format;
            }
        }
    }
}
