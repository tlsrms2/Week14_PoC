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

            SoundManager.PlaySfx(hoverSfxId);
        }
    }
}
