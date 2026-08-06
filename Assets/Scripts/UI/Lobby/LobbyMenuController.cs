using System.Collections;
using UnityEngine;
using UnityEngine.Localization.Settings;
using Week14.Audio;
using Week14.Enemy;
using Week14.GameFlow;
using Week14.Save;

namespace Week14.UI
{
    public sealed class LobbyMenuController : MonoBehaviour, IBackClosable
    {
        [Tooltip("로비 씬이 시작될 때 재생할 BGM의 SoundLibrary ID입니다. 비워두면 재생하지 않습니다.")]
        [BossGraphBgmId]
        [SerializeField] private string lobbyBgmId;

        [Tooltip("보스 패널 콘텐츠 루트입니다. OpenBossPanel/CloseBossPanel로 켜고 끕니다.")]
        [SerializeField] private Transform bossPanelContent;
        [Tooltip("로드아웃 패널 콘텐츠 루트입니다. 이 아래에 있는 모든 IPanelGatedInteractable이 항상 활성화됩니다.")]
        [SerializeField] private Transform loadoutPanelContent;
        [Tooltip("보스러시 패널 콘텐츠 루트입니다. OpenBossRushPanel/CloseBossRushPanel로 켜고 끕니다. 보스러시가 해금되지 않았으면 열리지 않습니다.")]
        [SerializeField] private Transform bossRushPanelContent;

        [Tooltip("bossRoot에 마우스를 올리면 켜지는 오브젝트입니다.")]
        [SerializeField] private GameObject bossHoverHighlight;
        [Tooltip("bossHoverHighlight와 함께 켜지고 꺼지는 추가 오브젝트들입니다.")]
        [SerializeField] private GameObject[] bossHoverExtraObjects;
        [Tooltip("loadoutRoot에 마우스를 올리면 켜지는 오브젝트입니다.")]
        [SerializeField] private GameObject loadoutHoverHighlight;
        [Tooltip("loadoutHoverHighlight와 함께 켜지고 꺼지는 추가 오브젝트들입니다.")]
        [SerializeField] private GameObject[] loadoutHoverExtraObjects;
        [Tooltip("bossRushRoot에 마우스를 올리면 켜지는 오브젝트입니다.")]
        [SerializeField] private GameObject bossRushHoverHighlight;
        [Tooltip("bossRushHoverHighlight와 함께 켜지고 꺼지는 추가 오브젝트들입니다.")]
        [SerializeField] private GameObject[] bossRushHoverExtraObjects;

        [Header("보스러시 해금")]
        [Tooltip("현재 세이브에서 엔딩을 봤다면(GameSaveManager.HasSeenEnding) 활성화할 오브젝트들입니다. 해금 전에는 비활성화됩니다.")]
        [SerializeField] private GameObject[] bossRushUnlockedObjects;
        [Tooltip("bossRushRoot의 히트 영역입니다. 해금 전에는 이 콜라이더를 꺼서 HoverDarkenImage/LobbyFloatingTextOnHover 등 " +
            "EventTrigger를 거치지 않는 컴포넌트까지 포함해 포인터 이벤트 자체가 전혀 들어가지 않게 만듭니다.")]
        [SerializeField] private Collider2D bossRushHitArea;

        private bool backCloseBlocked;

        public void SetBackCloseBlocked(bool blocked)
        {
            backCloseBlocked = blocked;
        }

        public void OnBossRootPointerEnter()
        {
            if (GameModalState.BlocksGameplayInput)
            {
                ClearHoverHighlights();
                return;
            }

            SetActiveSafe(bossHoverHighlight, true);
            SetActiveSafe(bossHoverExtraObjects, true);
        }

        public void OnBossRootPointerExit()
        {
            SetActiveSafe(bossHoverHighlight, false);
            SetActiveSafe(bossHoverExtraObjects, false);
        }

        public void OnLoadoutRootPointerEnter()
        {
            if (GameModalState.BlocksGameplayInput)
            {
                ClearHoverHighlights();
                return;
            }

            SetActiveSafe(loadoutHoverHighlight, true);
            SetActiveSafe(loadoutHoverExtraObjects, true);
        }

        public void OnLoadoutRootPointerExit()
        {
            SetActiveSafe(loadoutHoverHighlight, false);
            SetActiveSafe(loadoutHoverExtraObjects, false);
        }

        public void OnBossRushRootPointerEnter()
        {
            if (GameModalState.BlocksGameplayInput)
            {
                ClearHoverHighlights();
                return;
            }

            if (!GameSaveManager.HasSeenEnding)
            {
                return;
            }

            SetActiveSafe(bossRushHoverHighlight, true);
            SetActiveSafe(bossRushHoverExtraObjects, true);
        }

        public void OnBossRushRootPointerExit()
        {
            SetActiveSafe(bossRushHoverHighlight, false);
            SetActiveSafe(bossRushHoverExtraObjects, false);
        }

        public void OpenLoadoutPanel()
        {
            SetActiveSafe(loadoutPanelContent != null ? loadoutPanelContent.gameObject : null, true);
            UIBackStack.Push(this);
        }

        public void CloseLoadoutPanel()
        {
            SetActiveSafe(loadoutPanelContent != null ? loadoutPanelContent.gameObject : null, false);
            UpdateBackStackRegistration();
        }

        public void OpenBossPanel()
        {
            SetActiveSafe(bossPanelContent != null ? bossPanelContent.gameObject : null, true);
            UIBackStack.Push(this);
        }

        public void CloseBossPanel()
        {
            SetActiveSafe(bossPanelContent != null ? bossPanelContent.gameObject : null, false);
            UpdateBackStackRegistration();
        }

        public void OpenBossRushPanel()
        {
            if (!GameSaveManager.HasSeenEnding)
            {
                return;
            }

            SetActiveSafe(bossRushPanelContent != null ? bossRushPanelContent.gameObject : null, true);
            UIBackStack.Push(this);
        }

        public void CloseBossRushPanel()
        {
            SetActiveSafe(bossRushPanelContent != null ? bossRushPanelContent.gameObject : null, false);
            UpdateBackStackRegistration();
        }

        // ESC(뒤로가기)로 로드아웃/보스/보스러시 패널이 열려 있는 동안은 그 패널만 닫고,
        // 일시정지 패널(PauseMenuView)이 대신 열리지 않도록 UIBackStack에 등록해둔다.
        public bool CloseByBack()
        {
            if (backCloseBlocked
                && ((loadoutPanelContent != null && loadoutPanelContent.gameObject.activeSelf)
                    || (bossPanelContent != null && bossPanelContent.gameObject.activeSelf)
                    || (bossRushPanelContent != null && bossRushPanelContent.gameObject.activeSelf)))
            {
                return true;
            }

            if (loadoutPanelContent != null && loadoutPanelContent.gameObject.activeSelf)
            {
                CloseLoadoutPanel();
                return true;
            }

            if (bossPanelContent != null && bossPanelContent.gameObject.activeSelf)
            {
                CloseBossPanel();
                return true;
            }

            if (bossRushPanelContent != null && bossRushPanelContent.gameObject.activeSelf)
            {
                CloseBossRushPanel();
                return true;
            }

            return false;
        }

        private void UpdateBackStackRegistration()
        {
            bool loadoutOpen = loadoutPanelContent != null && loadoutPanelContent.gameObject.activeSelf;
            bool bossOpen = bossPanelContent != null && bossPanelContent.gameObject.activeSelf;
            bool bossRushOpen = bossRushPanelContent != null && bossRushPanelContent.gameObject.activeSelf;

            if (loadoutOpen || bossOpen || bossRushOpen)
            {
                UIBackStack.Push(this);
            }
            else
            {
                UIBackStack.Remove(this);
            }
        }

        public void ClearHoverHighlights()
        {
            SetActiveSafe(bossHoverHighlight, false);
            SetActiveSafe(bossHoverExtraObjects, false);
            SetActiveSafe(loadoutHoverHighlight, false);
            SetActiveSafe(loadoutHoverExtraObjects, false);
            SetActiveSafe(bossRushHoverHighlight, false);
            SetActiveSafe(bossRushHoverExtraObjects, false);
        }

        // 씬 리로드 없이 보스러시 해금 상태 변화를 즉시 반영하고 싶을 때(예: 디버그 툴, 엔딩 직후 로비 복귀) 외부에서도 호출합니다.
        public void RefreshBossRushUnlockState()
        {
            bool unlocked = GameSaveManager.HasSeenEnding;

            SetActiveSafe(bossRushUnlockedObjects, unlocked);

            if (bossRushHitArea != null)
            {
                bossRushHitArea.enabled = unlocked;
            }

            if (!unlocked)
            {
                SetActiveSafe(bossRushHoverHighlight, false);
                SetActiveSafe(bossRushHoverExtraObjects, false);
            }
        }

        private void Awake()
        {
            if (!string.IsNullOrEmpty(lobbyBgmId))
            {
                SoundManager.PlayBgm(lobbyBgmId);
            }

            SetContentInteractable(loadoutPanelContent, true);
            RefreshBossRushUnlockState();

            StartCoroutine(WarmUpLocalization());
        }

        private void Start()
        {
            // 로드아웃/보스 패널 밑에 있는 컴포넌트들은 자기 Awake에서 예열(로컬라이징 미리 로드,
            // 기본 표시 항목 세팅 등)을 하는데, 그게 여기서 패널을 꺼버리기 전에 한 번은 활성 상태로
            // 실행돼야 한다. Start는 씬의 모든 Awake가 끝난 뒤에 실행되는 게 보장되므로, 여기서
            // 닫아야 그 순서가 항상 지켜진다.
            CloseLoadoutPanel();
            CloseBossPanel();
            CloseBossRushPanel();
        }

        // 로컬라이제이션 시스템(로케일 선택 + Preload로 지정된 테이블)을 미리 초기화해둔다.
        // 이걸 안 해두면 로드아웃 패널에서 처음으로 로컬라이징 텍스트를 호버할 때 테이블이
        // 그제서야 로드되면서 첫 표시가 살짝 늦게 뜬다.
        private static IEnumerator WarmUpLocalization()
        {
            yield return LocalizationSettings.InitializationOperation;
        }

        private void OnDestroy()
        {
            UIBackStack.Remove(this);

            if (!string.IsNullOrEmpty(lobbyBgmId) && !BossRushController.IsRunning)
            {
                SoundManager.StopBgm();
            }
        }

        private static void SetActiveSafe(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }

        private static void SetActiveSafe(GameObject[] targets, bool active)
        {
            if (targets == null)
            {
                return;
            }

            for (int i = 0; i < targets.Length; i++)
            {
                SetActiveSafe(targets[i], active);
            }
        }

        private static void SetContentInteractable(Transform content, bool interactable)
        {
            if (content == null)
            {
                return;
            }

            IPanelGatedInteractable[] gated = content.GetComponentsInChildren<IPanelGatedInteractable>(true);
            for (int i = 0; i < gated.Length; i++)
            {
                gated[i].SetPanelOpen(interactable);
            }
        }
    }
}
