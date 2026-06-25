using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Week14.Combat;
using Week14.UI;

public class CursorController : MonoBehaviour
{
    [SerializeField] private Image normalCursorImage;
    [SerializeField] private Image interactableCursorImage;
    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private int persistentCanvasSortingOrder = 32767;

    private static CursorController instance;

    private RectTransform normalCursorRectTransform;
    private RectTransform interactableCursorRectTransform;
    private RectTransform parentRectTransform;
    private Canvas parentCanvas;
    private EventSystem cachedEventSystem;
    private PointerEventData pointerEventData;
    private readonly List<RaycastResult> raycastResults = new();
    private readonly List<MonoBehaviour> handlerComponents = new();
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
        ConfigureCursorImage(interactableCursorImage);
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
        bool interactable = cursorVisible
            && interactableCursorImage != null
            && IsPointerOverInteractable(mousePosition);

        SetCustomCursorVisible(cursorVisible, interactable);
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
        MoveImageToCanvas(interactableCursorImage, canvasTransform);
        DontDestroyOnLoad(canvas.gameObject);
    }

    private void CacheCursorReferences()
    {
        normalCursorRectTransform = normalCursorImage != null ? normalCursorImage.rectTransform : null;
        interactableCursorRectTransform = interactableCursorImage != null ? interactableCursorImage.rectTransform : null;
        parentRectTransform = ResolveParentRectTransform();
        parentCanvas = ResolveParentCanvas();
    }

    private RectTransform ResolveParentRectTransform()
    {
        if (normalCursorRectTransform != null && normalCursorRectTransform.parent is RectTransform normalParent)
        {
            return normalParent;
        }

        if (interactableCursorRectTransform != null && interactableCursorRectTransform.parent is RectTransform interactableParent)
        {
            return interactableParent;
        }

        return transform.parent as RectTransform;
    }

    private Canvas ResolveParentCanvas()
    {
        if (normalCursorImage != null && normalCursorImage.GetComponentInParent<Canvas>() is Canvas normalCanvas)
        {
            return normalCanvas;
        }

        if (interactableCursorImage != null && interactableCursorImage.GetComponentInParent<Canvas>() is Canvas interactableCanvas)
        {
            return interactableCanvas;
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

        if (interactableCursorImage != null && interactableCursorImage.GetComponentInParent<Canvas>() is Canvas interactableCanvas)
        {
            return interactableCanvas;
        }

        return GetComponentInParent<Canvas>();
    }

    private void FollowMousePosition(Vector2 mousePosition)
    {
        if (normalCursorRectTransform == null && interactableCursorRectTransform == null)
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

    private void SetCustomCursorVisible(bool visible, bool interactable)
    {
        if (normalCursorImage != null)
        {
            normalCursorImage.enabled = visible && !interactable;
        }

        if (interactableCursorImage != null)
        {
            interactableCursorImage.enabled = visible && interactable;
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

        if (interactableCursorRectTransform != null)
        {
            interactableCursorRectTransform.position = mousePosition;
        }
    }

    private void SetCursorAnchoredPosition(Vector2 localPosition)
    {
        if (normalCursorRectTransform != null)
        {
            normalCursorRectTransform.anchoredPosition = localPosition;
        }

        if (interactableCursorRectTransform != null)
        {
            interactableCursorRectTransform.anchoredPosition = localPosition;
        }
    }

    private bool IsPointerOverInteractable(Vector2 mousePosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return false;
        }

        if (pointerEventData == null || cachedEventSystem != eventSystem)
        {
            cachedEventSystem = eventSystem;
            pointerEventData = new PointerEventData(eventSystem);
        }

        pointerEventData.Reset();
        pointerEventData.position = mousePosition;
        raycastResults.Clear();
        eventSystem.RaycastAll(pointerEventData, raycastResults);

        for (int i = 0; i < raycastResults.Count; i++)
        {
            GameObject target = raycastResults[i].gameObject;
            if (target != null && IsInteractable(target))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsInteractable(GameObject target)
    {
        Selectable selectable = target.GetComponentInParent<Selectable>();
        if (selectable != null && selectable.IsActive() && selectable.IsInteractable())
        {
            return true;
        }

        return HasEnabledPointerHandler<IPointerClickHandler>(target)
            || HasEnabledPointerHandler<IPointerDownHandler>(target);
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

    private bool HasEnabledPointerHandler<T>(GameObject target) where T : class
    {
        handlerComponents.Clear();
        target.GetComponentsInParent(false, handlerComponents);

        for (int i = 0; i < handlerComponents.Count; i++)
        {
            MonoBehaviour component = handlerComponents[i];
            if (component != null && component.isActiveAndEnabled && component is T)
            {
                handlerComponents.Clear();
                return true;
            }
        }

        handlerComponents.Clear();
        return false;
    }
}
