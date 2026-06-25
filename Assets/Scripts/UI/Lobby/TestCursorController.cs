using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TestCursorController : MonoBehaviour
{
    [SerializeField] private Sprite clickSprite;

    private Image cursorImage;
    private RectTransform cursorRectTransform;
    private RectTransform parentRectTransform;
    private Canvas parentCanvas;
    private Sprite defaultSprite;

    private void Awake()
    {
        cursorImage = GetComponent<Image>();
        cursorRectTransform = GetComponent<RectTransform>();
        parentRectTransform = cursorRectTransform != null ? cursorRectTransform.parent as RectTransform : null;
        parentCanvas = GetComponentInParent<Canvas>();

        if (cursorImage != null)
        {
            defaultSprite = cursorImage.sprite;
            cursorImage.raycastTarget = false;
        }
    }

    private void OnEnable()
    {
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        Cursor.visible = true;
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        FollowMousePosition(mouse.position.ReadValue());
        UpdateClickSprite(mouse.leftButton.isPressed);
    }

    private void FollowMousePosition(Vector2 mousePosition)
    {
        if (cursorRectTransform == null)
        {
            return;
        }

        if (parentRectTransform == null || parentCanvas == null)
        {
            cursorRectTransform.position = mousePosition;
            return;
        }

        Camera eventCamera = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRectTransform,
                mousePosition,
                eventCamera,
                out Vector2 localPosition))
        {
            cursorRectTransform.anchoredPosition = localPosition;
        }
    }

    private void UpdateClickSprite(bool isLeftButtonPressed)
    {
        if (cursorImage == null || clickSprite == null)
        {
            return;
        }

        cursorImage.sprite = isLeftButtonPressed ? clickSprite : defaultSprite;
    }
}
