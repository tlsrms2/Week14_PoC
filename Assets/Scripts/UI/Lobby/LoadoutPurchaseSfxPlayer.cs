using UnityEngine;
using Week14.Audio;
using Week14.Enemy;
using Week14.Save;

namespace Week14.UI
{
    [DisallowMultipleComponent]
    public sealed class LoadoutPurchaseSfxPlayer : MonoBehaviour
    {
        [Tooltip("총기/액티브/패시브 스킬을 챌린지 포인트로 구매(해금)했을 때 재생할 SoundLibrary SFX ID입니다. 비워두면 재생하지 않습니다.")]
        [BossGraphSfxId]
        [SerializeField] private string purchaseSfxId;
        [Tooltip("같은 프레임이나 아주 짧은 간격에 여러 구매 이벤트가 겹칠 때 중복 재생을 막는 시간(초)입니다.")]
        [SerializeField, Min(0f)] private float debounceSeconds = 0.05f;

        private float lastPlayTime = float.NegativeInfinity;

        private void OnEnable()
        {
            GameSaveManager.ItemPurchased += HandleItemPurchased;
        }

        private void OnDisable()
        {
            GameSaveManager.ItemPurchased -= HandleItemPurchased;
        }

        private void HandleItemPurchased()
        {
            if (string.IsNullOrEmpty(purchaseSfxId))
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now - lastPlayTime < debounceSeconds)
            {
                return;
            }

            lastPlayTime = now;
            SoundManager.PlaySfx(purchaseSfxId);
        }
    }
}
