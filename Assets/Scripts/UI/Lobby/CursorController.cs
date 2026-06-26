using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Week14.Combat;
using Week14.UI;

public class CursorController : MonoBehaviour
{
    [SerializeField] private Image normalCursorImage;
    [FormerlySerializedAs("interactableCursorImage")]
    [SerializeField] private Image pressedCursorImage;
    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private int persistentCanvasSortingOrder = 32767;

    private static CursorController instance;

    private RectTransform normalCursorRectTransform;
    private RectTransform pressedCursorRectTransform;
    private RectTransform parentRectTransform;
    private Canvas parentCanvas;
    private bool skipCursorRestore;
    private bool customCursorVisible;

    private void Awake()
    {
        if (normalCursorImage == null)
        {
            normalCursorImage = GetComponent<Image>();
        }

        if (dontDestroyOnLoad && !TryBecomePersistentInstance())
        {
            return;
        }

        CacheCursorReferences();
        ConfigureCursorImage(normalCursorImage);
        ConfigureCursorImage(pressedCursorImage);
        SetCustomCursorVisible(false, false);
    }

    private void OnEnable()
    {
        ApplyOsCursorVisible(true);
    }

    private void OnDisable()
    {
        if (skipCursorRestore)
        {
            return;
        }

        Cursor.visible = true;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        if (ShouldSuppressCustomCursor())
        {
            SetCustomCursorVisible(false, false);
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            SetCustomCursorVisible(false, false);
            ApplyOsCursorVisible(true);
            return;
        }

        Vector2 mousePosition = mouse.position.ReadValue();
        bool cursorVisible = CanRenderCustomCursor();
        bool pressed = cursorVisible
            && pressedCursorImage != null
            && mouse.leftButton.isPressed;

        SetCustomCursorVisible(cursorVisible, pressed);
        ApplyOsCursorVisible(!cursorVisible);

        if (!cursorVisible)
        {
            return;
        }

        FollowMousePosition(mousePosition);
    }

    private void LateUpdate()
    {
        if (customCursorVisible)
        {
            ApplyOsCursorVisible(false);
        }
    }

    private bool TryBecomePersistentInstance()
    {
        if (instance != null && instance != this)
        {
            skipCursorRestore = true;
            SetCustomCursorVisible(false, false);
            Destroy(gameObject);
            return false;
        }

        instance = this;
        MoveToPersistentCanvas();
        return true;
    }

    private void MoveToPersistentCanvas()
    {
        Canvas canvas = ResolvePersistentCanvas();
        if (canvas == null)
        {
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            return;
        }

        Transform canvasTransform = canvas.transform;
        canvasTransform.SetParent(null);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = persistentCanvasSortingOrder;

        MoveImageToCanvas(normalCursorImage, canvasTransform);
        MoveImageToCanvas(pressedCursorImage, canvasTransform);
        DontDestroyOnLoad(canvas.gameObject);
    }

    private void CacheCursorReferences()
    {
        normalCursorRectTransform = normalCursorImage != null ? normalCursorImage.rectTransform : null;
        pressedCursorRectTransform = pressedCursorImage != null ? pressedCursorImage.rectTransform : null;
        parentRectTransform = ResolveParentRectTransform();
        parentCanvas = ResolveParentCanvas();
    }

    private RectTransform ResolveParentRectTransform()
    {
        if (normalCursorRectTransform != null && normalCursorRectTransform.parent is RectTransform normalParent)
        {
            return normalParent;
        }

        if (pressedCursorRectTransform != null && pressedCursorRectTransform.parent is RectTransform pressedParent)
        {
            return pressedParent;
        }

        return transform.parent as RectTransform;
    }

    private Canvas ResolveParentCanvas()
    {
        if (normalCursorImage != null && normalCursorImage.GetComponentInParent<Canvas>() is Canvas normalCanvas)
        {
            return normalCanvas;
        }

        if (pressedCursorImage != null && pressedCursorImage.GetComponentInParent<Canvas>() is Canvas pressedCanvas)
        {
            return pressedCanvas;
        }

        return GetComponentInParent<Canvas>();
    }

    private Canvas ResolvePersistentCanvas()
    {
        if (TryGetComponent(out Canvas ownCanvas))
        {
            return ownCanvas;
        }

        if (normalCursorImage != null && normalCursorImage.GetComponentInParent<Canvas>() is Canvas normalCanvas)
        {
            return normalCanvas;
        }

        if (pressedCursorImage != null && pressedCursorImage.GetComponentInParent<Canvas>() is Canvas pressedCanvas)
        {
            return pressedCanvas;
        }

        return GetComponentInParent<Canvas>();
    }

    private void FollowMousePosition(Vector2 mousePosition)
    {
        if (normalCursorRectTransform == null && pressedCursorRectTransform == null)
        {
            return;
        }

        if (parentRectTransform == null || parentCanvas == null)
        {
            SetCursorPosition(mousePosition);
            return;
        }

        Camera eventCamera = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRectTransform,
                mousePosition,
                eventCamera,
                out Vector2 localPosition))
        {
            SetCursorAnchoredPosition(localPosition);
        }
    }

    private bool CanRenderCustomCursor()
    {
        return normalCursorImage != null
            && normalCursorRectTransform != null
            && isActiveAndEnabled;
    }

    private void SetCustomCursorVisible(bool visible, bool pressed)
    {
        if (normalCursorImage != null)
        {
            normalCursorImage.enabled = visible && !pressed;
        }

        if (pressedCursorImage != null)
        {
            pressedCursorImage.enabled = visible && pressed;
        }

        customCursorVisible = visible;
    }

    private static void ApplyOsCursorVisible(bool visible)
    {
        Cursor.visible = visible;
        Cursor.lockState = CursorLockMode.None;
    }

    private static void ConfigureCursorImage(Image image)
    {
        if (image == null)
        {
            return;
        }

        image.raycastTarget = false;
        image.gameObject.SetActive(true);
    }

    private static void MoveImageToCanvas(Image image, Transform canvasTransform)
    {
        if (image == null || canvasTransform == null)
        {
            return;
        }

        image.rectTransform.SetParent(canvasTransform, false);
        image.rectTransform.SetAsLastSibling();
    }

    private void SetCursorPosition(Vector2 mousePosition)
    {
        if (normalCursorRectTransform != null)
        {
            normalCursorRectTransform.position = mousePosition;
        }

        if (pressedCursorRectTransform != null)
        {
            pressedCursorRectTransform.position = mousePosition;
        }
    }

    private void SetCursorAnchoredPosition(Vector2 localPosition)
    {
        if (normalCursorRectTransform != null)
        {
            normalCursorRectTransform.anchoredPosition = localPosition;
        }

        if (pressedCursorRectTransform != null)
        {
            pressedCursorRectTransform.anchoredPosition = localPosition;
        }
    }

    private bool ShouldSuppressCustomCursor()
    {
        PlayerCombatController player = PlayerCombatController.Active;
        return player != null
            && !GameModalState.BlocksGameplayInput
            && !player.IsExecuting
            && player.Health != null
            && !player.Health.IsDead;
    }

}
