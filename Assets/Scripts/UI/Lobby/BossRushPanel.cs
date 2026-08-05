using System;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
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

            // 패널이 닫혀있는 동안(OnDisable로 Unbind되어있는 동안)에도 보스 이름 로컬라이징 문구를
            // 미리 로드해둔다. BindAndRefresh()만으로는 패널이 열리기 전까지 아무도 구독하지 않아서
            // 처음 열 때 살짝 늦게 뜬다.
            public void WarmUp()
            {
                if (bossData == null)
                {
                    return;
                }

                LoadoutSelectedSkillPanelLocalization.RefreshIfLocalized(bossData.LocalizedBossName, bossData.HasLocalizedBossName);
            }

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

        [Header("고정 문구 예열")]
        [Tooltip("이 패널 안의 제목/\"최고 기록\" 라벨처럼 LocalizeStringEvent로 직접 바인딩된 고정 문구들입니다. " +
            "이 컴포넌트들은 패널이 닫히면 같이 비활성화되어 구독이 끊기고, 열릴 때마다 처음부터 다시 로드하느라 " +
            "잠깐 이전 언어가 보입니다. 여기 등록해두면 미리 로드해서 그 현상을 없앱니다. 보스 이름과는 다른 로컬라이징 " +
            "테이블을 쓰기 때문에 별도로 예열해야 합니다.")]
        [SerializeField] private LocalizeStringEvent[] fixedLocalizeEvents;

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

            // 로비 진입 직후(패널이 열리기 전) 한 번, 그리고 언어가 바뀔 때마다 보스러시 보스 이름
            // 로컬라이징 문구를 미리 로드해둔다. 이벤트 구독을 OnEnable/OnDisable이 아니라
            // Awake/OnDestroy에 걸어서, 패널이 닫혀 있는 동안(비활성 상태)에도 예열이 계속 동작하게 한다.
            WarmUpBossNames();
            WarmUpFixedLabels();
            LocalizationSettings.SelectedLocaleChanged += HandleLocaleChangedForWarmUp;
        }

        private void WarmUpBossNames()
        {
            if (bossRecordFields == null)
            {
                return;
            }

            for (int i = 0; i < bossRecordFields.Length; i++)
            {
                bossRecordFields[i]?.WarmUp();
            }
        }

        private void WarmUpFixedLabels()
        {
            if (fixedLocalizeEvents == null)
            {
                return;
            }

            for (int i = 0; i < fixedLocalizeEvents.Length; i++)
            {
                LocalizeStringEvent localizeEvent = fixedLocalizeEvents[i];
                if (localizeEvent == null)
                {
                    continue;
                }

                LocalizedString localizedString = localizeEvent.StringReference;
                LoadoutSelectedSkillPanelLocalization.RefreshIfLocalized(
                    localizedString,
                    LoadoutSelectedSkillPanelLocalization.HasLocalizedString(localizedString));
            }
        }

        private void HandleLocaleChangedForWarmUp(Locale locale)
        {
            WarmUpBossNames();
            WarmUpFixedLabels();
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
            LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChangedForWarmUp;
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
