using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Week14.Enemy;

namespace Week14.UI
{
    public sealed class BossLivesView : MonoBehaviour
    {
        [SerializeField] private BossAI target;
        [SerializeField, Min(1)] private int fallbackMaxLives = 1;
        [SerializeField] private List<GameObject> lifeBackgrounds = new();
        [SerializeField, FormerlySerializedAs("lifeObjects")] private List<GameObject> lifeImages = new();

        private void Awake()
        {
            Refresh();
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void SetTarget(BossAI nextTarget)
        {
            if (target == nextTarget)
            {
                Refresh();
                return;
            }

            Unsubscribe();
            target = nextTarget;
            Subscribe();
            Refresh();
        }

        public void Refresh()
        {
            int maxLives = target != null ? target.MaxLives : fallbackMaxLives;
            int currentLives = target != null ? target.CurrentLives : maxLives;
            SetValue(currentLives, maxLives);
        }

        private void Subscribe()
        {
            if (target != null)
            {
                target.LivesChanged += HandleLivesChanged;
            }
        }

        private void Unsubscribe()
        {
            if (target != null)
            {
                target.LivesChanged -= HandleLivesChanged;
            }
        }

        private void HandleLivesChanged(int currentLives, int maxLives)
        {
            SetValue(currentLives, maxLives);
        }

        private void SetValue(int currentLives, int maxLives)
        {
            maxLives = Mathf.Max(1, maxLives);
            currentLives = Mathf.Clamp(currentLives, 0, maxLives);
            fallbackMaxLives = maxLives;

            SetActiveByCount(lifeBackgrounds, maxLives);
            SetLifeImages(lifeImages, currentLives, maxLives);
        }

        private static void SetActiveByCount(IReadOnlyList<GameObject> objects, int activeCount)
        {
            activeCount = Mathf.Max(0, activeCount);
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] != null)
                {
                    objects[i].SetActive(i < activeCount);
                }
            }
        }

        private static void SetLifeImages(IReadOnlyList<GameObject> objects, int currentLives, int maxLives)
        {
            int visibleLifeCount = Mathf.Min(maxLives, objects.Count);
            int firstActiveIndex = Mathf.Max(0, visibleLifeCount - currentLives);

            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] != null)
                {
                    objects[i].SetActive(i < visibleLifeCount && i >= firstActiveIndex);
                }
            }
        }
    }
}
