using System.Collections;
using UnityEngine;
using UnityEngine.Localization.Settings;
using Week14.Audio;
using Week14.Enemy;

namespace Week14.UI
{
    public sealed class LobbyMenuController : MonoBehaviour
    {
        [Tooltip("로비 씬이 시작될 때 재생할 BGM의 SoundLibrary ID입니다. 비워두면 재생하지 않습니다.")]
        [BossGraphBgmId]
        [SerializeField] private string lobbyBgmId;

        [Tooltip("보스 패널 콘텐츠 루트입니다. OpenBossPanel/CloseBossPanel로 켜고 끕니다.")]
        [SerializeField] private Transform bossPanelContent;
        [Tooltip("로드아웃 패널 콘텐츠 루트입니다. 이 아래에 있는 모든 IPanelGatedInteractable이 항상 활성화됩니다.")]
        [SerializeField] private Transform loadoutPanelContent;

        [Tooltip("bossRoot에 마우스를 올리면 켜지는 오브젝트입니다.")]
        [SerializeField] private GameObject bossHoverHighlight;
        [Tooltip("bossHoverHighlight와 함께 켜지고 꺼지는 추가 오브젝트들입니다.")]
        [SerializeField] private GameObject[] bossHoverExtraObjects;
        [Tooltip("loadoutRoot에 마우스를 올리면 켜지는 오브젝트입니다.")]
        [SerializeField] private GameObject loadoutHoverHighlight;
        [Tooltip("loadoutHoverHighlight와 함께 켜지고 꺼지는 추가 오브젝트들입니다.")]
        [SerializeField] private GameObject[] loadoutHoverExtraObjects;

        public void OnBossRootPointerEnter()
        {
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
            SetActiveSafe(loadoutHoverHighlight, true);
            SetActiveSafe(loadoutHoverExtraObjects, true);
        }

        public void OnLoadoutRootPointerExit()
        {
            SetActiveSafe(loadoutHoverHighlight, false);
            SetActiveSafe(loadoutHoverExtraObjects, false);
        }

        public void OpenLoadoutPanel()
        {
            SetActiveSafe(loadoutPanelContent != null ? loadoutPanelContent.gameObject : null, true);
        }

        public void CloseLoadoutPanel()
        {
            SetActiveSafe(loadoutPanelContent != null ? loadoutPanelContent.gameObject : null, false);
        }

        public void OpenBossPanel()
        {
            SetActiveSafe(bossPanelContent != null ? bossPanelContent.gameObject : null, true);
        }

        public void CloseBossPanel()
        {
            SetActiveSafe(bossPanelContent != null ? bossPanelContent.gameObject : null, false);
        }

        private void Awake()
        {
            if (!string.IsNullOrEmpty(lobbyBgmId))
            {
                SoundManager.PlayBgm(lobbyBgmId);
            }

            SetContentInteractable(loadoutPanelContent, true);

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
            if (!string.IsNullOrEmpty(lobbyBgmId))
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
