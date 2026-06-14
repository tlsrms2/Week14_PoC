using UnityEngine;

namespace Week14.Combat
{
    public sealed class ParryRingVfx : MonoBehaviour
    {
        private LineRenderer line;
        private Color color;
        private float duration;
        private float elapsed;
        private float scale = 1f;

        public void Play(LineRenderer nextLine, float seconds, Color nextColor)
        {
            Play(nextLine, seconds, nextColor, 1f);
        }

        public void Play(LineRenderer nextLine, float seconds, Color nextColor, float nextScale)
        {
            line = nextLine;
            color = nextColor;
            duration = Mathf.Max(0.01f, seconds);
            elapsed = 0f;
            scale = Mathf.Max(0f, nextScale);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float radius = Mathf.Lerp(0.08f, 0.75f, t) * scale;
            transform.localScale = Vector3.one * radius;

            if (line != null)
            {
                float flash = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 8f)) * (1f - t);
                Color faded = color;
                faded.a *= Mathf.Clamp01(1f - t + flash * 0.45f);
                line.startColor = faded;
                line.endColor = faded;
                line.startWidth = (Mathf.Lerp(0.05f, 0.005f, t) + flash * 0.018f) * scale;
                line.endWidth = line.startWidth;
            }

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
