using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Week14.UI
{
    public sealed class HoverOutlineImage : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [Tooltip("호버 중일 때 활성화할 아웃라인 UI Graphic(Image 등)입니다.")]
        [SerializeField] private Graphic outlineGraphic;
        [Tooltip("호버 중일 때 활성화할 아웃라인 SpriteRenderer입니다.")]
        [SerializeField] private SpriteRenderer outlineSpriteRenderer;

        private bool isHovering;
        private Vector2 lastPointerPosition;
        private PointerEventData hoverPointerEventData;
        private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

        private void OnEnable()
        {
            isHovering = false;
            SetOutlineVisible(false);
        }

        private void Start()
        {
            // OnEnable 시점엔 outlineGraphic/outlineSpriteRenderer를 설정하는 다른 스크립트의 Awake가
            // 아직 실행 전일 수 있다. Start는 모든 Awake 이후에 실행되니 여기서 한 번 더 꺼서 보장한다.
            isHovering = false;
            SetOutlineVisible(false);
        }

        private void OnDisable()
        {
            isHovering = false;
            SetOutlineVisible(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            lastPointerPosition = eventData.position;
            StartHover();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            lastPointerPosition = eventData.position;
            EndHover();
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            lastPointerPosition = eventData.position;
        }

        private void Update()
        {
            if (isHovering && !IsPointerStillOverThis())
            {
                EndHover();
            }
        }

        private void StartHover()
        {
            if (isHovering)
            {
                return;
            }

            isHovering = true;
            SetOutlineVisible(true);
        }

        private void EndHover()
        {
            if (!isHovering)
            {
                return;
            }

            isHovering = false;
            SetOutlineVisible(false);
        }

        private void SetOutlineVisible(bool visible)
        {
            if (outlineGraphic != null)
            {
                outlineGraphic.enabled = visible;
            }

            if (outlineSpriteRenderer != null)
            {
                outlineSpriteRenderer.enabled = visible;
            }
        }

        private bool IsPointerStillOverThis()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return true;
            }

            if (hoverPointerEventData == null)
            {
                hoverPointerEventData = new PointerEventData(eventSystem);
            }

            hoverPointerEventData.position = lastPointerPosition;
            raycastResults.Clear();
            eventSystem.RaycastAll(hoverPointerEventData, raycastResults);

            if (raycastResults.Count == 0)
            {
                return false;
            }

            return IsOwnTransform(raycastResults[0].gameObject.transform);
        }

        private bool IsOwnTransform(Transform hitTransform)
        {
            if (hitTransform == null)
            {
                return false;
            }

            if (hitTransform == transform || hitTransform.IsChildOf(transform))
            {
                return true;
            }

            if (outlineGraphic != null && (hitTransform == outlineGraphic.transform || hitTransform.IsChildOf(outlineGraphic.transform)))
            {
                return true;
            }

            return outlineSpriteRenderer != null
                   && (hitTransform == outlineSpriteRenderer.transform || hitTransform.IsChildOf(outlineSpriteRenderer.transform));
        }
    }
}
