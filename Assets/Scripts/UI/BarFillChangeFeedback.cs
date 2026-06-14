using UnityEngine;
using UnityEngine.UI;

namespace Week14.UI
{
    internal sealed class BarFillChangeFeedback
    {
        private const string DeltaFillName = "DeltaFill";
        private const float AnimationSeconds = 0.3f;
        private static readonly Color DeltaColor = new(1f, 1f, 1f, 0.86f);

        private Image sourceImage;
        private Image deltaImage;
        private float startAmount;
        private float targetAmount;
        private float elapsedSeconds;
        private bool initialized;
        private bool increasing;
        private bool playing;

        public void Configure(Image image)
        {
            sourceImage = image;
            initialized = false;
            playing = false;

            if (sourceImage == null)
            {
                deltaImage = null;
                return;
            }

            deltaImage = FindOrCreateDeltaImage(sourceImage);
            if (deltaImage == null)
            {
                return;
            }

            CopyImageSettings(sourceImage, deltaImage);
            deltaImage.enabled = false;
        }

        public void SetAmount(float amount, bool animate)
        {
            float nextAmount = Mathf.Clamp01(amount);
            if (sourceImage == null)
            {
                return;
            }

            if (!initialized || !animate || !CanAnimate(sourceImage) || deltaImage == null)
            {
                ResetTo(nextAmount);
                return;
            }

            if (Mathf.Approximately(targetAmount, nextAmount))
            {
                return;
            }

            CopyImageSettings(sourceImage, deltaImage);
            startAmount = GetCurrentEdge(nextAmount);
            targetAmount = nextAmount;
            increasing = targetAmount > startAmount;
            elapsedSeconds = 0f;
            playing = true;

            if (increasing)
            {
                sourceImage.fillAmount = startAmount;
                deltaImage.fillAmount = targetAmount;
            }
            else
            {
                sourceImage.fillAmount = targetAmount;
                deltaImage.fillAmount = startAmount;
            }

            deltaImage.enabled = true;
        }

        public void Tick()
        {
            if (!playing || sourceImage == null || deltaImage == null)
            {
                return;
            }

            elapsedSeconds += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedSeconds / AnimationSeconds);
            float edgeAmount = Mathf.Lerp(startAmount, targetAmount, t);

            if (increasing)
            {
                sourceImage.fillAmount = edgeAmount;
                deltaImage.fillAmount = targetAmount;
            }
            else
            {
                sourceImage.fillAmount = targetAmount;
                deltaImage.fillAmount = edgeAmount;
            }

            if (t < 1f)
            {
                return;
            }

            sourceImage.fillAmount = targetAmount;
            deltaImage.enabled = false;
            playing = false;
        }

        private void ResetTo(float amount)
        {
            targetAmount = amount;
            startAmount = amount;
            elapsedSeconds = AnimationSeconds;
            initialized = true;
            playing = false;
            sourceImage.fillAmount = amount;

            if (deltaImage != null)
            {
                deltaImage.enabled = false;
            }
        }

        private float GetCurrentEdge(float nextAmount)
        {
            if (!playing || deltaImage == null || !deltaImage.enabled)
            {
                return sourceImage.fillAmount;
            }

            return nextAmount > targetAmount ? sourceImage.fillAmount : deltaImage.fillAmount;
        }

        private static bool CanAnimate(Image image)
        {
            return image != null && image.type == Image.Type.Filled;
        }

        private static Image FindOrCreateDeltaImage(Image source)
        {
            Transform parent = source.transform.parent;
            if (parent == null)
            {
                return null;
            }

            Transform existing = parent.Find(DeltaFillName);
            Image image = existing != null ? existing.GetComponent<Image>() : null;
            if (image == null)
            {
                GameObject imageObject = new GameObject(DeltaFillName, typeof(RectTransform));
                imageObject.transform.SetParent(parent, false);
                image = imageObject.AddComponent<Image>();
            }

            RectTransform sourceRect = source.rectTransform;
            RectTransform deltaRect = image.rectTransform;
            deltaRect.anchorMin = sourceRect.anchorMin;
            deltaRect.anchorMax = sourceRect.anchorMax;
            deltaRect.offsetMin = sourceRect.offsetMin;
            deltaRect.offsetMax = sourceRect.offsetMax;
            deltaRect.pivot = sourceRect.pivot;
            deltaRect.localScale = sourceRect.localScale;
            deltaRect.localRotation = sourceRect.localRotation;
            int sourceIndex = source.transform.GetSiblingIndex();
            if (image.transform.GetSiblingIndex() < sourceIndex)
            {
                sourceIndex--;
            }

            image.transform.SetSiblingIndex(Mathf.Max(0, sourceIndex));
            return image;
        }

        private static void CopyImageSettings(Image source, Image target)
        {
            if (source == null || target == null)
            {
                return;
            }

            target.sprite = source.sprite;
            target.type = source.type;
            target.fillMethod = source.fillMethod;
            target.fillOrigin = source.fillOrigin;
            target.fillClockwise = source.fillClockwise;
            target.preserveAspect = source.preserveAspect;
            target.material = source.material;
            target.raycastTarget = false;
            target.color = DeltaColor;
        }
    }
}
