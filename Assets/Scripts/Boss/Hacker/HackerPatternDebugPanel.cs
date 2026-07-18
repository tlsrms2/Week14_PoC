using UnityEngine;

namespace Week14.Enemy
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hacker Pattern Debug Panel")]
    public sealed class HackerPatternDebugPanel : MonoBehaviour
    {
        [Header("Debug Target")]
        [SerializeField] private BossGraphAsset hackerGraph;

        [Header("Layout")]
        [SerializeField, Min(180f)] private float panelWidth = 260f;

        private HackerBossAI boss;
        private Vector2 scrollPosition;
        private string statusMessage;

        private void OnEnable()
        {
            ResolveBoss();
            if (Application.isPlaying)
            {
                boss?.SetDebugPatternControlActive(true);
            }
        }

        private void Start()
        {
            ResolveBoss();
            boss?.SetDebugPatternControlActive(true);
        }

        private void OnDisable()
        {
            if (Application.isPlaying && boss != null)
            {
                boss.SetDebugPatternControlActive(false);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            ResolveBoss();
            float width = Mathf.Clamp(panelWidth, 180f, Mathf.Max(180f, Screen.width - 24f));
            float height = Mathf.Max(120f, Screen.height - 24f);
            GUILayout.BeginArea(new Rect(12f, 12f, width, height), GUI.skin.box);

            GUILayout.Label("Hacker 패턴 테스트");
            if (boss == null)
            {
                GUILayout.Label("부모에서 HackerBossAI를 찾을 수 없습니다.");
                GUILayout.EndArea();
                return;
            }

            if (hackerGraph == null)
            {
                GUILayout.Label("Hacker Graph 에셋을 지정하세요.");
                GUILayout.EndArea();
                return;
            }

            bool isRunning = boss.IsDebugPatternRunning;
            GUILayout.Label($"현재 페이즈: {boss.CurrentPhaseNumber}");
            GUILayout.Label(isRunning ? "실행 중" : statusMessage ?? "대기 중");

            if (isRunning && GUILayout.Button("현재 패턴 중지", GUILayout.Height(28f)))
            {
                boss.StopDebugPattern();
                statusMessage = "중지됨";
            }

            GUILayout.Space(4f);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            GUI.enabled = !isRunning;
            for (int i = 0; i < hackerGraph.Patterns.Count; i++)
            {
                BossGraphPattern pattern = hackerGraph.Patterns[i];
                if (pattern == null || string.IsNullOrWhiteSpace(pattern.PatternId))
                {
                    continue;
                }

                string patternId = pattern.PatternId;
                if (GUILayout.Button(patternId, GUILayout.Height(26f)))
                {
                    bool started = boss.TryRunDebugPatternOnce(hackerGraph, patternId);
                    statusMessage = started ? $"{patternId} 실행" : $"{patternId} 실행 실패";
                }
            }

            GUI.enabled = true;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
#endif

        private void ResolveBoss()
        {
            if (boss == null)
            {
                boss = GetComponentInParent<HackerBossAI>(true);
            }
        }

        private void OnValidate()
        {
            panelWidth = Mathf.Max(180f, panelWidth);
        }
    }
}
