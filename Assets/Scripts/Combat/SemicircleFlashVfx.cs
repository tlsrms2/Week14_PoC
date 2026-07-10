using UnityEngine;

namespace Week14.Combat
{
    public sealed class SemicircleFlashVfx : MonoBehaviour
    {
        private Mesh mesh;
        private Color[] colorBuffer;
        private Color color;
        private float duration;
        private float elapsed;

        public void Play(Mesh nextMesh, float seconds, Color nextColor)
        {
            mesh = nextMesh;
            color = nextColor;
            duration = Mathf.Max(0.01f, seconds);
            elapsed = 0f;
            colorBuffer = mesh != null ? new Color[mesh.vertexCount] : null;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (mesh != null && colorBuffer != null)
            {
                Color faded = color;
                faded.a *= 1f - t;
                for (int i = 0; i < colorBuffer.Length; i++)
                {
                    colorBuffer[i] = faded;
                }

                mesh.colors = colorBuffer;
            }

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
