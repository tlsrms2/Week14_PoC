using UnityEngine;
using Week14.Audio;
using Week14.Save;

namespace Week14.UI
{
    [DisallowMultipleComponent]
    public sealed class LoadoutPurchaseSfxPlayer : MonoBehaviour
    {
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
            float now = Time.unscaledTime;
            if (now - lastPlayTime < debounceSeconds)
            {
                return;
            }

            lastPlayTime = now;
            SoundManager.PlaySfx(SoundEvent.UI_LobbyLoadoutPurchase);
        }
    }
}
