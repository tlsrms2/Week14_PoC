using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace Week14.UI
{
    [DisallowMultipleComponent]
    public sealed class FloatingDamageView : MonoBehaviour
    {
        [Serializable]
        private sealed class BulletTextStyle
        {
            [FormerlySerializedAs("minDamage")]
            [SerializeField, Min(0)] private int bulletNumber = 1;
            [SerializeField, Min(1f)] private float fontSize = 28f;
            [SerializeField] private Color textColor = Color.white;
            [SerializeField] private Color outlineColor = Color.black;
            [SerializeField, Range(0f, 1f)] private float outlineWidth = 1f;

            public BulletTextStyle()
            {
            }

            public BulletTextStyle(int bulletNumber, float fontSize, Color textColor, Color outlineColor, float outlineWidth)
            {
                this.bulletNumber = bulletNumber;
                this.fontSize = fontSize;
                this.textColor = textColor;
                this.outlineColor = outlineColor;
                this.outlineWidth = outlineWidth;
            }

            public int BulletNumber => bulletNumber;
            public float FontSize => fontSize;
            public Color TextColor => textColor;
            public Color OutlineColor => outlineColor;
            public float OutlineWidth => outlineWidth;
        }

        private sealed class Popup
        {
            public RectTransform Rect;
            public TextMeshProUGUI Text;
            public Vector3 StartWorldPosition;
            public float Timer;
        }

        private const string CanvasName = "FloatingDamageCanvas";
        private const string PopupName = "FloatingDamageText";

        [SerializeField, Min(0.05f)] private float lifetimeSeconds = 0.75f;
        [SerializeField] private Vector2 floatOffset = new(0f, 0.42f);
        [SerializeField, Min(0.01f)] private float canvasScale = 0.01f;
        [SerializeField] private Vector2 textBoxSize = new(2.4f, 0.8f);
        [SerializeField] private int sortingOrder = 80;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [SerializeField] private string format = "{0}";
        [SerializeField] private BulletTextStyle[] styles =
        {
            new(5, 18f, Color.white, Color.black, 0.16f),
            new(4, 20f, new Color(0.72f, 0.95f, 1f, 1f), new Color(0.05f, 0.24f, 0.32f, 1f), 0.18f),
            new(3, 23f, new Color(1f, 0.92f, 0.45f, 1f), new Color(0.35f, 0.12f, 0f, 1f), 0.2f),
            new(2, 26f, new Color(1f, 0.64f, 0.2f, 1f), new Color(0.45f, 0.08f, 0f, 1f), 0.23f),
            new(1, 30f, new Color(1f, 0.42f, 0.16f, 1f), new Color(0.55f, 0f, 0f, 1f), 0.26f)
        };

        private readonly List<Popup> popups = new();
        private Canvas canvas;
        private RectTransform canvasRect;
        private Collider2D areaCollider2D;
        private Collider areaCollider3D;

        public void Show(int damage, int bulletNumber)
        {
            EnsureCanvas();
            if (canvasRect == null)
            {
                return;
            }

            BulletTextStyle style = ResolveStyle(bulletNumber);
            GameObject textObject = new(PopupName, typeof(RectTransform));
            textObject.transform.SetParent(canvasRect, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            Vector3 popupStartWorldPosition = CreatePopupStartWorldPosition();
            rect.position = popupStartWorldPosition;
            rect.sizeDelta = textBoxSize;

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            if (font != null)
            {
                text.font = font;
            }

            text.text = string.Format(format, damage);
            ApplyStyle(text, style);

            popups.Add(new Popup
            {
                Rect = rect,
                Text = text,
                StartWorldPosition = popupStartWorldPosition,
                Timer = 0f
            });
        }

        private void LateUpdate()
        {
            UpdateCanvasPose();
            TickPopups();
        }

        private void EnsureCanvas()
        {
            if (canvas != null)
            {
                return;
            }

            Transform existing = transform.Find(CanvasName);
            canvas = existing != null ? existing.GetComponent<Canvas>() : null;
            if (canvas == null)
            {
                GameObject canvasObject = new(CanvasName, typeof(RectTransform));
                canvasObject.transform.SetParent(transform, false);
                canvas = canvasObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
            canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = Vector2.one;
            canvasRect.localScale = Vector3.one * canvasScale;
            UpdateCanvasPose();
        }

        private void UpdateCanvasPose()
        {
            if (canvasRect == null)
            {
                return;
            }

            canvasRect.position = transform.position;

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                canvasRect.rotation = mainCamera.transform.rotation;
            }
        }

        private void TickPopups()
        {
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                Popup popup = popups[i];
                if (popup == null || popup.Rect == null || popup.Text == null)
                {
                    popups.RemoveAt(i);
                    continue;
                }

                popup.Timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(popup.Timer / lifetimeSeconds);
                Vector3 worldOffset = new(floatOffset.x, floatOffset.y, 0f);
                popup.Rect.position = Vector3.Lerp(popup.StartWorldPosition, popup.StartWorldPosition + worldOffset, EaseOut(t));

                float alpha = EvaluateAlpha(t);
                Color textColor = popup.Text.color;
                textColor.a = alpha;
                popup.Text.color = textColor;

                Color outlineColor = popup.Text.outlineColor;
                outlineColor.a = textColor.a;
                popup.Text.outlineColor = outlineColor;

                if (popup.Timer < lifetimeSeconds)
                {
                    continue;
                }

                Destroy(popup.Rect.gameObject);
                popups.RemoveAt(i);
            }
        }

        private float EvaluateAlpha(float t)
        {
            if (alphaCurve == null || alphaCurve.length == 0)
            {
                return 1f - Mathf.SmoothStep(0f, 1f, t);
            }

            return Mathf.Clamp01(alphaCurve.Evaluate(t));
        }

        private Vector3 CreatePopupStartWorldPosition()
        {
            EnsureAreaCollider();
            if (areaCollider2D != null)
            {
                return CreateRandomPointInCollider(areaCollider2D);
            }

            if (areaCollider3D != null)
            {
                return CreateRandomPointInCollider(areaCollider3D);
            }

            return transform.position;
        }

        private void EnsureAreaCollider()
        {
            if (areaCollider2D == null)
            {
                areaCollider2D = GetComponent<Collider2D>() ?? GetComponentInChildren<Collider2D>();
            }

            if (areaCollider3D == null)
            {
                areaCollider3D = GetComponent<Collider>() ?? GetComponentInChildren<Collider>();
            }
        }

        private static Vector3 CreateRandomPointInCollider(Collider2D areaCollider)
        {
            Bounds bounds = areaCollider.bounds;
            for (int i = 0; i < 16; i++)
            {
                Vector2 point = new(
                    UnityEngine.Random.Range(bounds.min.x, bounds.max.x),
                    UnityEngine.Random.Range(bounds.min.y, bounds.max.y));

                if (areaCollider.OverlapPoint(point))
                {
                    return new Vector3(point.x, point.y, areaCollider.transform.position.z);
                }
            }

            Vector2 fallback = areaCollider.ClosestPoint(bounds.center);
            return new Vector3(fallback.x, fallback.y, areaCollider.transform.position.z);
        }

        private static Vector3 CreateRandomPointInCollider(Collider areaCollider)
        {
            Bounds bounds = areaCollider.bounds;
            for (int i = 0; i < 16; i++)
            {
                Vector3 point = new(
                    UnityEngine.Random.Range(bounds.min.x, bounds.max.x),
                    UnityEngine.Random.Range(bounds.min.y, bounds.max.y),
                    UnityEngine.Random.Range(bounds.min.z, bounds.max.z));

                if (areaCollider.ClosestPoint(point) == point)
                {
                    return point;
                }
            }

            return areaCollider.ClosestPoint(bounds.center);
        }

        private BulletTextStyle ResolveStyle(int bulletNumber)
        {
            if (styles == null || styles.Length == 0)
            {
                return null;
            }

            BulletTextStyle fallback = null;
            for (int i = 0; i < styles.Length; i++)
            {
                BulletTextStyle style = styles[i];
                if (style == null)
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = style;
                }

                if (style.BulletNumber == bulletNumber)
                {
                    return style;
                }
            }

            return fallback;
        }

        private static void ApplyStyle(TextMeshProUGUI text, BulletTextStyle style)
        {
            if (style == null)
            {
                text.fontSize = 18f;
                text.color = Color.white;
                text.outlineColor = Color.black;
                text.outlineWidth = 0.18f;
                return;
            }

            text.fontSize = style.FontSize;
            text.color = style.TextColor;
            text.outlineColor = style.OutlineColor;
            text.outlineWidth = style.OutlineWidth;
        }

        private static float EaseOut(float t)
        {
            return 1f - Mathf.Pow(1f - t, 3f);
        }
    }
}
