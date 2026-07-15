using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Week14.UI
{
    public sealed class LobbyFloatingTextOnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [SerializeField] private GameObject floatingText;

        private bool isHovering;
        private Vector2 lastPointerPosition;
        private PointerEventData hoverPointerEventData;
        private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

        private void Awake()
        {
            ResolveFloatingText();
        }

        private void OnEnable()
        {
            isHovering = false;
            SetFloatingTextVisible(false);
        }

        private void Start()
        {
            SetFloatingTextVisible(false);
        }

        private void OnDisable()
        {
            isHovering = false;
            SetFloatingTextVisible(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            lastPointerPosition = eventData.position;
            isHovering = true;
            SetFloatingTextVisible(true);
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

        private void EndHover()
        {
            if (!isHovering)
            {
                return;
            }

            isHovering = false;
            SetFloatingTextVisible(false);
        }

        private void SetFloatingTextVisible(bool visible)
        {
            ResolveFloatingText();

            if (floatingText != null && floatingText.activeSelf != visible)
            {
                floatingText.SetActive(visible);
            }
        }

        private void ResolveFloatingText()
        {
            if (floatingText != null)
            {
                return;
            }

            Transform canvas = transform.Find("Canvas");
            Transform text = canvas != null ? canvas.Find("FloatingText") : transform.Find("FloatingText");
            if (text != null)
            {
                floatingText = text.gameObject;
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
            return hitTransform != null && (hitTransform == transform || hitTransform.IsChildOf(transform));
        }
    }
}
