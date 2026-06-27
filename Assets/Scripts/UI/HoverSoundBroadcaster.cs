using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Week14.Audio;
using Week14.Enemy;

namespace Week14.UI
{
    public sealed class HoverSoundBroadcaster : MonoBehaviour
    {
        [Tooltip("이 오브젝트 아래의 모든 Selectable(Button/Toggle/Slider 등)에 공통으로 적용할 호버 SFX의 SoundLibrary ID입니다. 비워두면 재생하지 않습니다.")]
        [BossGraphSfxId]
        [SerializeField] private string hoverSfxId;
        [Tooltip("같은 Selectable에서 이 시간(초) 안에 다시 호버해도 재생을 막습니다. 레이캐스트 잔떨림 등으로 PointerEnter가 짧은 간격에 반복될 때 중복 재생을 막는 용도입니다.")]
        [SerializeField, Min(0f)] private float debounceSeconds = 0.1f;

        private readonly Dictionary<Selectable, float> lastPlayTimes = new();

        private void Awake()
        {
            WireHoverSound();
        }

        private void WireHoverSound()
        {
            if (string.IsNullOrEmpty(hoverSfxId))
            {
                return;
            }

            foreach (Selectable selectable in GetComponentsInChildren<Selectable>(includeInactive: true))
            {
                AddHoverListener(selectable);
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
            SoundManager.PlaySfx(hoverSfxId);
        }
    }
}
