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

        [SerializeField] private Selectable selectable;

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

            SoundManager.PlaySfx(hoverSfxId);
        }
    }
}
