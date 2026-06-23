using System.Collections;
using UnityEngine;
using Week14.Enemy;

namespace Week14.Environment
{
    public sealed class BossCombatSlidingDoor : MonoBehaviour
    {
        [Header("Boss")]
        [Tooltip("비워두면 씬에서 처음 전투를 시작한 보스에 반응합니다.")]
        [SerializeField] private BossAI targetBoss;

        [Header("Door")]
        [SerializeField] private Transform leftDoor;
        [SerializeField] private Transform rightDoor;
        [SerializeField, Min(0f)] private float openDistance = 3f;
        [SerializeField, Min(0.01f)] private float closeDuration = 0.6f;
        [SerializeField] private bool startOpened = true;
        [SerializeField] private AnimationCurve closeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private Vector3 leftClosedLocalPosition;
        private Vector3 rightClosedLocalPosition;
        private Coroutine closeRoutine;
        private bool initialized;
        private bool closed;

        private void Awake()
        {
            InitializePositions();

            if (startOpened)
            {
                OpenInstant();
            }
            else
            {
                closed = true;
            }
        }

        private void OnEnable()
        {
            BossAI.CombatStarted += HandleCombatStarted;
        }

        private void Start()
        {
            if (targetBoss != null)
            {
                if (targetBoss.IsCombatStarted)
                {
                    Close();
                }

                return;
            }

            BossAI[] bosses = FindObjectsByType<BossAI>(FindObjectsSortMode.None);
            for (int i = 0; i < bosses.Length; i++)
            {
                if (!bosses[i].IsCombatStarted)
                {
                    continue;
                }

                targetBoss = bosses[i];
                Close();
                return;
            }
        }

        private void OnDisable()
        {
            BossAI.CombatStarted -= HandleCombatStarted;
        }

        public void Close()
        {
            InitializePositions();
            if (!initialized || closed)
            {
                return;
            }

            if (closeRoutine != null)
            {
                StopCoroutine(closeRoutine);
            }

            closeRoutine = StartCoroutine(CloseRoutine());
        }

        public void OpenInstant()
        {
            InitializePositions();
            if (!initialized)
            {
                return;
            }

            if (closeRoutine != null)
            {
                StopCoroutine(closeRoutine);
                closeRoutine = null;
            }

            leftDoor.localPosition = leftClosedLocalPosition + Vector3.left * openDistance;
            rightDoor.localPosition = rightClosedLocalPosition + Vector3.right * openDistance;
            closed = false;
        }

        private void HandleCombatStarted(BossAI boss)
        {
            if (targetBoss != null && boss != targetBoss)
            {
                return;
            }

            targetBoss ??= boss;
            Close();
        }

        private IEnumerator CloseRoutine()
        {
            Vector3 leftStart = leftDoor.localPosition;
            Vector3 rightStart = rightDoor.localPosition;
            float elapsed = 0f;

            while (elapsed < closeDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / closeDuration);
                float t = closeCurve != null ? closeCurve.Evaluate(progress) : progress;

                leftDoor.localPosition = Vector3.LerpUnclamped(leftStart, leftClosedLocalPosition, t);
                rightDoor.localPosition = Vector3.LerpUnclamped(rightStart, rightClosedLocalPosition, t);

                yield return null;
            }

            leftDoor.localPosition = leftClosedLocalPosition;
            rightDoor.localPosition = rightClosedLocalPosition;
            closeRoutine = null;
            closed = true;
        }

        private void InitializePositions()
        {
            if (initialized || leftDoor == null || rightDoor == null)
            {
                return;
            }

            leftClosedLocalPosition = leftDoor.localPosition;
            rightClosedLocalPosition = rightDoor.localPosition;
            initialized = true;
        }
    }
}
