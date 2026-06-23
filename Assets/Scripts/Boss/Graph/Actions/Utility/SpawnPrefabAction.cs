using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class SpawnPrefabAction : BossAction
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private Vector2 positionOffset;
        [SerializeField] private Vector3 rotationEuler;
        [SerializeField] private bool parentToBoss;
        [SerializeField, Min(0f)] private float destroyAfterSeconds;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            GameObject instance = context.SpawnPrefab(prefab, positionOffset, rotationEuler, parentToBoss);
            if (instance != null && destroyAfterSeconds > 0f)
            {
                UnityEngine.Object.Destroy(instance, destroyAfterSeconds);
            }
        }
    }
}
