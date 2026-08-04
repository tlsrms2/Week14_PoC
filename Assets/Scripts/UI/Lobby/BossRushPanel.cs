using System;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;
using Week14.Enemy;
using Week14.GameFlow;
using Week14.Save;

namespace Week14.UI
{
    [DisallowMultipleComponent]
    public sealed class BossRushPanel : MonoBehaviour
    {
        [Serializable]
        private sealed class BossRecordField
        {
            [SerializeField] private BossData bossData;
            [SerializeField] private TMP_Text bossNameText;
            [SerializeField] private TMP_Text bestTimeText;

            [NonSerialized] private LocalizedString.ChangeHandler localizedNameChanged;

            public string BossId => bossData != null ? bossData.Id : null;

            public void BindAndRefresh()
            {
                Unbind();
                if (bossData == null)
                {
                    SetBossName(string.Empty);
                    SetBestTime("--:--:--");
                    return;
                }

                localizedNameChanged = SetBossName;
                LoadoutSelectedSkillPanelLocalization.BindLocalizedString(
                    bossData.LocalizedBossName,
                    bossData.HasLocalizedBossName,
                    localizedNameChanged);

                if (!bossData.HasLocalizedBossName)
                {
                    SetBossName(bossData.BossName);
                }

                string time = GameSaveManager.HasBossRushBossBestTime(bossData.Id)
                    ? BossRushController.FormatTime(
                        GameSaveManager.GetBossRushBossBestTime(bossData.Id))
                    : "--:--:--";
                SetBestTime(time);
            }

            public void Unbind()
            {
                if (localizedNameChanged == null)
                {
                    return;
                }

                if (bossData != null)
                {
                    LoadoutSelectedSkillPanelLocalization.UnbindLocalizedString(
                        bossData.LocalizedBossName,
                        bossData.HasLocalizedBossName,
                        localizedNameChanged);
                }

                localizedNameChanged = null;
            }

            private void SetBossName(string value)
            {
                if (bossNameText != null)
                {
                    bossNameText.text = value ?? string.Empty;
                }
            }

            private void SetBestTime(string value)
            {
                if (bestTimeText != null)
                {
                    bestTimeText.text = value;
                }
            }
        }

        [Header("UI")]
        [SerializeField] private TMP_Text bestRecordText;
        [SerializeField] private Button startButton;
        [Tooltip("보스 이름과 해당 보스의 최단 기록을 표시할 필드입니다.")]
        [SerializeField] private BossRecordField[] bossRecordFields = new BossRecordField[5];

        [Header("Boss Scenes")]
        [Tooltip("진행 순서대로 보스러시 전용 씬 이름을 정확히 5개 지정합니다.")]
        [SerializeField] private string[] bossSceneNames = new string[5];

        [Header("Audio")]
        [SerializeField, BossGraphBgmId] private string bossRushBgmId;
        [SerializeField, Min(0f)] private float bossRushBgmFadeSeconds = 0.5f;

        [Header("Debug")]
        [Tooltip("Editor 또는 Development Build에서만 적용됩니다. 플레이어 무적과 보스 한 방 처치를 활성화합니다.")]
        [SerializeField] private bool enableDebugCheats;

        private void Awake()
        {
            startButton?.onClick.AddListener(StartBossRush);
        }

        private void OnEnable()
        {
            RefreshRecord();
            if (startButton != null)
            {
                startButton.interactable = true;
            }
        }

        private void OnDisable()
        {
            if (bossRecordFields == null)
            {
                return;
            }

            for (int i = 0; i < bossRecordFields.Length; i++)
            {
                bossRecordFields[i]?.Unbind();
            }
        }

        private void OnDestroy()
        {
            startButton?.onClick.RemoveListener(StartBossRush);
        }

        public void StartBossRush()
        {
            if (BossRushController.IsRunning
                || !GameSaveManager.HasSeenEnding
                || !BossRushController.StartRun(
                    bossSceneNames,
                    GetBossIds(),
                    bossRushBgmId,
                    bossRushBgmFadeSeconds,
                    enableDebugCheats))
            {
                return;
            }

            if (startButton != null)
            {
                startButton.interactable = false;
            }
        }

        private string[] GetBossIds()
        {
            if (bossRecordFields == null)
            {
                return Array.Empty<string>();
            }

            string[] ids = new string[bossRecordFields.Length];
            for (int i = 0; i < bossRecordFields.Length; i++)
            {
                ids[i] = bossRecordFields[i]?.BossId;
            }

            return ids;
        }

        public void RefreshRecord()
        {
            if (bestRecordText != null)
            {
                string time = GameSaveManager.HasBossRushBestTime
                    ? BossRushController.FormatTime(GameSaveManager.BossRushBestTime)
                    : "--:--:--";
                bestRecordText.text = time;
            }

            if (bossRecordFields == null)
            {
                return;
            }

            for (int i = 0; i < bossRecordFields.Length; i++)
            {
                bossRecordFields[i]?.BindAndRefresh();
            }
        }
    }
}
