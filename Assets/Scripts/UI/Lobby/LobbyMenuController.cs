using UnityEngine;
using Week14.Audio;
using Week14.Enemy;

namespace Week14.UI
{
    public sealed class LobbyMenuController : MonoBehaviour
    {
        [Tooltip("로비 씬이 시작될 때 재생할 BGM의 SoundLibrary ID입니다. 비워두면 재생하지 않습니다.")]
        [BossGraphBgmId]
        [SerializeField] private string lobbyBgmId;

        [Tooltip("보스 패널 콘텐츠 루트입니다. 이 아래에 있는 모든 IPanelGatedInteractable이 항상 활성화됩니다.")]
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

        private void Awake()
        {
            if (!string.IsNullOrEmpty(lobbyBgmId))
            {
                SoundManager.PlayBgm(lobbyBgmId);
            }

            SetContentInteractable(bossPanelContent, true);
            SetContentInteractable(loadoutPanelContent, true);
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
