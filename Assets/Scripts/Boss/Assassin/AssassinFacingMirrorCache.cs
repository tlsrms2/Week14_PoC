using UnityEngine;

namespace Week14.Enemy
{
    // 스프라이트를 flipX로 좌우 반전할 때, 그 스프라이트의 자식(무기 등 오프셋을 가진 파츠)의 로컬 x
    // 위치도 같이 반전시켜준다 — flipX만 켜면 자식 오프셋은 그대로 남아 위치가 어긋나 보이기 때문이다.
    // AssassinBossAI와 AssassinClone이 같은 방식으로 재사용한다.
    internal sealed class AssassinFacingMirrorCache
    {
        private Transform[] children;
        private Vector3[] baseLocalPositions;
        private bool cached;

        public void Apply(Transform root, bool flip)
        {
            if (root == null)
            {
                return;
            }

            if (!cached)
            {
                cached = true;
                int childCount = root.childCount;
                children = new Transform[childCount];
                baseLocalPositions = new Vector3[childCount];
                for (int i = 0; i < childCount; i++)
                {
                    Transform child = root.GetChild(i);
                    children[i] = child;
                    baseLocalPositions[i] = child.localPosition;
                }
            }

            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == null)
                {
                    continue;
                }

                Vector3 basePosition = baseLocalPositions[i];
                child.localPosition = new Vector3(flip ? -basePosition.x : basePosition.x, basePosition.y, basePosition.z);
            }
        }
    }
}
