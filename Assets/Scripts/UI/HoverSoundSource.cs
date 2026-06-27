using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Week14.Audio;
using Week14.Enemy;

namespace Week14.UI
{
    public sealed class HoverSoundSource : MonoBehaviour, IPointerEnterHandler
    {
        [Tooltip("이 오브젝트에 호버 시 재생할 SFX의 SoundLibrary ID입니다. 비워두면 재생하지 않습니다.")]
        [BossGraphSfxId]
        [SerializeField] private string hoverSfxId;
        [Tooltip("이 시간(초) 안에 다시 호버해도 재생을 막습니다. 레이캐스트 잔떨림 등으로 PointerEnter가 짧은 간격에 반복될 때 중복 재생을 막는 용도입니다.")]
        [SerializeField, Min(0f)] private float debounceSeconds = 0.1f;

        [SerializeField] private Selectable selectable;

        private float lastPlayTime = float.NegativeInfinity;

        private void Awake()
        {
            if (selectable == null)
            {
                selectable = GetComponent<Selectable>();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (string.IsNullOrEmpty(hoverSfxId))
            {
                return;
            }

            if (selectable != null && !selectable.interactable)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now - lastPlayTime < debounceSeconds)
            {
                return;
            }

            lastPlayTime = now;
            SoundManager.PlaySfx(hoverSfxId);
        }
    }
}
