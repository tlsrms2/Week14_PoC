using UnityEngine;

namespace Week14.Combat
{
    public sealed class ParryRingVfx : MonoBehaviour
    {
        private LineRenderer line;
        private Color color;
        private float duration;
        private float elapsed;

        public void Play(LineRenderer nextLine, float seconds, Color nextColor)
        {
            line = nextLine;
            color = nextColor;
            duration = Mathf.Max(0.01f, seconds);
            elapsed = 0f;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float radius = Mathf.Lerp(0.08f, 0.75f, t);
            transform.localScale = Vector3.one * radius;

            if (line != null)
            {
                float flash = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 8f)) * (1f - t);
                Color faded = color;
                faded.a *= Mathf.Clamp01(1f - t + flash * 0.45f);
                line.startColor = faded;
                line.endColor = faded;
                line.startWidth = Mathf.Lerp(0.05f, 0.005f, t) + flash * 0.018f;
                line.endWidth = line.startWidth;
            }

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
