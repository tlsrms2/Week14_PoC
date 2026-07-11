using System;
using TMPro;
using UnityEngine;
using Week14.Save;

namespace Week14.UI
{
    // 보스 패널 안에 고정 배치되는 "보스 설명" 구역입니다. LoadoutSelectedSkillPanel과 동일하게
    // 팝업처럼 뜨고 사라지는 게 아니라 항상 붙어있고, 호버할 때마다 내용만 갱신됩니다.
    // 챌린지 목록 표시는 기존 BossChallengePanel에 그대로 위임합니다.
    public sealed class BossDescriptionPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text bestClearTimeText;
        [SerializeField] private BossChallengePanel challengePanel;
        [SerializeField] private LoadoutSelectedSkillPanelLocalization localization;

        [Tooltip("패널을 처음 열었을 때(아직 아무것도 호버하지 않은 최초 상태) 보여줄 보스입니다.")]
        [SerializeField] private BossData defaultBoss;

        private BossData localizedBossData;

        public static BossDescriptionPanel Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (defaultBoss != null)
            {
                Show(defaultBoss);
            }
        }

        private void OnDestroy()
        {
            UnbindLocalizedBossName();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Show(BossData bossData)
        {
            if (bossData == null)
            {
                return;
            }

            UnbindLocalizedBossName();
            BossHoverHighlight.SetHovered(bossData.Id);

            string displayName = bossData.HasLocalizedBossName ? string.Empty : bossData.BossName;
            SetNameText(displayName);

            SetBestClearTimeText(bossData.Id);

            localizedBossData = bossData;
            LoadoutSelectedSkillPanelLocalization.BindLocalizedString(bossData.LocalizedBossName, bossData.HasLocalizedBossName, SetNameText);

            challengePanel?.Show(bossData);
        }

        public void Hide()
        {
            UnbindLocalizedBossName();
            BossHoverHighlight.ClearHovered();
            SetNameText(string.Empty);

            if (bestClearTimeText != null)
            {
                bestClearTimeText.text = string.Empty;
            }

            challengePanel?.Hide();
        }

        private void SetBestClearTimeText(string bossId)
        {
            if (bestClearTimeText == null)
            {
                return;
            }

            string formattedTime;
            if (GameSaveManager.HasBestClearTime(bossId))
            {
                TimeSpan time = TimeSpan.FromSeconds(GameSaveManager.GetBestClearTime(bossId));
                formattedTime = time.ToString(@"mm\:ss\:ff");
            }
            else
            {
                formattedTime = "--:--:--";
            }

            bestClearTimeText.text = localization != null ? localization.FormatBestClearTime(formattedTime) : $"최단 기록: {formattedTime}";
        }

        private void UnbindLocalizedBossName()
        {
            if (localizedBossData == null)
            {
                return;
            }

            LoadoutSelectedSkillPanelLocalization.UnbindLocalizedString(localizedBossData.LocalizedBossName, localizedBossData.HasLocalizedBossName, SetNameText);
            localizedBossData = null;
        }

        private void SetNameText(string value)
        {
            if (nameText != null)
            {
                nameText.text = value;
            }
        }
    }
}
