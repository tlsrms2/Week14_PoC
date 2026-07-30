using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Week14.Audio;

namespace Week14.UI
{
    public sealed class HoverSoundBroadcaster : MonoBehaviour
    {
        [Tooltip("같은 Selectable에서 이 시간(초) 안에 다시 호버해도 재생을 막습니다. 레이캐스트 잔떨림 등으로 PointerEnter가 짧은 간격에 반복될 때 중복 재생을 막는 용도입니다.")]
        [SerializeField, Min(0f)] private float debounceSeconds = 0.1f;

        private readonly Dictionary<Selectable, float> lastPlayTimes = new();

        private void Awake()
        {
            WireUiSounds();
        }

        private void WireUiSounds()
        {
            foreach (Selectable selectable in GetComponentsInChildren<Selectable>(includeInactive: true))
            {
                AddHoverListener(selectable);

                if (selectable is Button button)
                {
                    button.onClick.AddListener(SoundManager.PlayButtonClickSfx);
                }
            }
        }

        private void AddHoverListener(Selectable selectable)
        {
            EventTrigger trigger = selectable.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = selectable.gameObject.AddComponent<EventTrigger>();
            }

            EventTrigger.Entry entry = new() { eventID = EventTriggerType.PointerEnter };
            entry.callback.AddListener(_ => PlayHoverSound(selectable));
            trigger.triggers.Add(entry);
        }

        private void PlayHoverSound(Selectable selectable)
        {
            if (selectable != null && !selectable.interactable)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (lastPlayTimes.TryGetValue(selectable, out float lastPlayTime) && now - lastPlayTime < debounceSeconds)
            {
                return;
            }

            lastPlayTimes[selectable] = now;
            SoundManager.PlaySfx(SoundEvent.UI_Hover);
        }
    }
}
