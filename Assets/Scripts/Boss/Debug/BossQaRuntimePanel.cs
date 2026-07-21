#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Week14.Enemy
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    internal sealed class BossQaRuntimePanel : MonoBehaviour
    {
        private const float PanelWidth = 200f;
        private const float RefreshIntervalSeconds = 0.5f;
        private const int PatternColumnCount = 2;

        private readonly List<GraphBossAI> bosses = new();
        private readonly List<BossGraphPattern> phasePatterns = new();
        private readonly HashSet<string> phasePatternIds = new();
        private GraphBossAI selectedBoss;
        private Vector2 patternScrollPosition;
        private float nextRefreshAt;
        private bool isOpen;
        private bool isPointerCaptured;
        private string statusMessage;
        private GUIStyle opaquePanelStyle;
        private Texture2D opaquePanelTexture;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureCreated()
        {
            if (FindAnyObjectByType<BossQaRuntimePanel>() != null)
            {
                return;
            }

            GameObject root = new("Boss QA Runtime Panel")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            DontDestroyOnLoad(root);
            root.AddComponent<BossQaRuntimePanel>();
        }

        private void Update()
        {
            if (WasToggleKeyPressed())
            {
                isOpen = !isOpen;
                statusMessage = null;
                if (isOpen)
                {
                    RefreshBosses(true);
                }
                else
                {
                    SetPointerCaptured(false);
                }
            }

            if (isOpen)
            {
                RefreshBosses(false);
            }

            SetPointerCaptured(isOpen && IsPointerOverPanel());
        }

        private void OnDisable()
        {
            SetPointerCaptured(false);
        }

        private void OnDestroy()
        {
            SetPointerCaptured(false);
            if (opaquePanelTexture != null)
            {
                Destroy(opaquePanelTexture);
            }
        }

        private void OnGUI()
        {
            if (!isOpen)
            {
                return;
            }

            GUI.depth = -1000;
            EnsureOpaquePanelStyle();
            Rect panelRect = GetPanelRect();
            GUILayout.BeginArea(panelRect, opaquePanelStyle);
            DrawHeader();
            DrawBossSelector();
            DrawSelectedBoss();
            GUILayout.EndArea();
        }

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Boss QA Tool", GUILayout.ExpandWidth(true));
            if (GUILayout.Button("닫기 (F12)", GUILayout.Width(90f)))
            {
                isOpen = false;
                SetPointerCaptured(false);
            }

            GUILayout.EndHorizontal();
        }

        private void DrawBossSelector()
        {
            if (bosses.Count == 0)
            {
                GUILayout.Space(8f);
                GUILayout.Label("현재 제어 가능한 보스가 없습니다.");
                return;
            }

            if (bosses.Count == 1)
            {
                selectedBoss = bosses[0];
                return;
            }

            GUILayout.Label("대상 보스");
            for (int i = 0; i < bosses.Count; i++)
            {
                GraphBossAI boss = bosses[i];
                bool wasEnabled = GUI.enabled;
                GUI.enabled = boss != selectedBoss;
                if (GUILayout.Button(boss.DisplayName))
                {
                    selectedBoss = boss;
                    patternScrollPosition = Vector2.zero;
                    statusMessage = null;
                }

                GUI.enabled = wasEnabled;
            }
        }

        private void DrawSelectedBoss()
        {
            if (selectedBoss == null)
            {
                return;
            }

            GUILayout.Space(8f);
            GUILayout.Label($"{selectedBoss.DisplayName} / Phase {selectedBoss.CurrentPhaseNumber}");
            GUILayout.Label($"현재 패턴: {FormatPatternId(selectedBoss.QaCurrentPatternId)}");
            GUILayout.Label($"반복 고정: {FormatPatternId(selectedBoss.QaForcedPatternId)}");

            DrawBossSpecificControls();

            string pauseLabel = selectedBoss.IsQaBehaviorPaused ? "보스 행동 재개" : "보스 행동 멈춤";
            if (GUILayout.Button(pauseLabel, GUILayout.Height(32f)))
            {
                bool paused = !selectedBoss.IsQaBehaviorPaused;
                bool changed = selectedBoss.TrySetQaBehaviorPaused(paused);
                statusMessage = changed
                    ? paused ? "보스 행동을 멈췄습니다." : "보스 행동을 재개했습니다."
                    : "현재 상태에서는 행동을 변경할 수 없습니다.";
            }

            if (!string.IsNullOrWhiteSpace(selectedBoss.QaForcedPatternId)
                && GUILayout.Button("반복 패턴 고정 해제", GUILayout.Height(26f)))
            {
                selectedBoss.CancelQaForcedPattern();
                statusMessage = "반복 패턴 고정을 해제했습니다.";
            }

            if (!string.IsNullOrWhiteSpace(statusMessage))
            {
                GUILayout.Label(statusMessage);
            }

            GUILayout.Space(8f);
            GUILayout.Label("반복 실행할 패턴");
            BossGraphAsset graph = selectedBoss.QaGraphAsset;
            if (graph == null)
            {
                GUILayout.Label("활성 Boss Graph가 없습니다.");
                return;
            }

            BossGraphPhase phase = graph.GetPhase(selectedBoss.CurrentPhaseIndex);
            if (phase == null)
            {
                GUILayout.Label("현재 페이즈 설정이 없습니다.");
                return;
            }

            patternScrollPosition = GUILayout.BeginScrollView(patternScrollPosition);
            DrawSpecialPatterns(graph, phase);
            CollectPhasePatterns(graph, phase);
            GUILayout.Label("현재 페이즈 일반 패턴");
            if (phasePatterns.Count == 0)
            {
                GUILayout.Label("등록된 일반 패턴이 없습니다.");
            }
            else
            {
                DrawPatternGrid(phasePatterns);
            }

            GUILayout.EndScrollView();
        }

        private void DrawSpecialPatterns(BossGraphAsset graph, BossGraphPhase phase)
        {
            bool hasOpening = !string.IsNullOrWhiteSpace(phase.OpeningPatternId);
            bool hasSignature = !string.IsNullOrWhiteSpace(phase.SignaturePatternId);
            if (!hasOpening && !hasSignature)
            {
                return;
            }

            GUILayout.Label("오프닝 / 시그니처 패턴");
            GUILayout.BeginHorizontal();
            DrawSpecialPatternSlot(graph, "오프닝", phase.OpeningPatternId);
            DrawSpecialPatternSlot(graph, "시그니처", phase.SignaturePatternId);
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
        }

        private void DrawSpecialPatternSlot(BossGraphAsset graph, string role, string patternId)
        {
            if (string.IsNullOrWhiteSpace(patternId))
            {
                GUILayout.Label(string.Empty, GUILayout.Height(42f), GUILayout.ExpandWidth(true));
                return;
            }

            BossGraphPattern pattern = GetRunnablePattern(graph, patternId);
            bool wasEnabled = GUI.enabled;
            GUI.enabled = pattern != null;
            DrawPatternButton(pattern, $"{role}\n{patternId}", 42f);
            GUI.enabled = wasEnabled;
        }

        private void CollectPhasePatterns(BossGraphAsset graph, BossGraphPhase phase)
        {
            phasePatterns.Clear();
            phasePatternIds.Clear();
            IReadOnlyList<BossGraphPatternEntry> entries = phase.Patterns;
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                string patternId = entries[i]?.PatternId;
                if (string.IsNullOrWhiteSpace(patternId)
                    || patternId == phase.OpeningPatternId
                    || patternId == phase.SignaturePatternId
                    || !phasePatternIds.Add(patternId))
                {
                    continue;
                }

                BossGraphPattern pattern = GetRunnablePattern(graph, patternId);
                if (pattern != null)
                {
                    phasePatterns.Add(pattern);
                }
            }
        }

        private void DrawPatternGrid(IReadOnlyList<BossGraphPattern> patterns)
        {
            int drawnPatternCount = 0;
            for (int i = 0; i < patterns.Count; i++)
            {
                BossGraphPattern pattern = patterns[i];
                if (drawnPatternCount % PatternColumnCount == 0)
                {
                    GUILayout.BeginHorizontal();
                }

                DrawPatternButton(pattern, pattern.PatternId, 27f);

                drawnPatternCount++;
                if (drawnPatternCount % PatternColumnCount == 0)
                {
                    GUILayout.EndHorizontal();
                }
            }

            if (drawnPatternCount % PatternColumnCount != 0)
            {
                GUILayout.Label(string.Empty, GUILayout.Height(27f), GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();
            }
        }

        private void DrawPatternButton(BossGraphPattern pattern, string label, float height)
        {
            if (!GUILayout.Button(label, GUILayout.Height(height)) || pattern == null)
            {
                return;
            }

            bool forced = selectedBoss.TrySetQaForcedPattern(pattern.PatternId);
            statusMessage = forced
                ? $"'{pattern.PatternId}' 반복 실행을 고정했습니다."
                : $"'{pattern.PatternId}' 반복 고정에 실패했습니다.";
        }

        private static BossGraphPattern GetRunnablePattern(BossGraphAsset graph, string patternId)
        {
            BossGraphPattern pattern = graph != null ? graph.GetPattern(patternId) : null;
            return pattern?.NodeKeys != null && pattern.NodeKeys.Count > 0
                ? pattern
                : null;
        }

        private void DrawBossSpecificControls()
        {
            if (selectedBoss is not AssassinBossAI assassin)
            {
                return;
            }

            GUILayout.Space(6f);
            GUILayout.Label($"Assassin 상태: {(assassin.IsStealthed ? "은신" : "비은신")}");
            bool enableStealth = !assassin.IsStealthed;
            string buttonLabel = enableStealth ? "은신 그래프로 전환" : "비은신 그래프로 전환";
            if (!GUILayout.Button(buttonLabel, GUILayout.Height(30f)))
            {
                return;
            }

            bool changed = assassin.TrySetQaStealth(enableStealth);
            patternScrollPosition = Vector2.zero;
            statusMessage = changed
                ? enableStealth
                    ? "현재 패턴과 반복 고정을 해제하고 은신 그래프로 전환했습니다."
                    : "현재 패턴과 반복 고정을 해제하고 비은신 그래프로 전환했습니다."
                : "대상 그래프가 없거나 현재 상태에서는 전환할 수 없습니다.";
        }

        private void RefreshBosses(bool force)
        {
            if (!force && Time.unscaledTime < nextRefreshAt)
            {
                return;
            }

            nextRefreshAt = Time.unscaledTime + RefreshIntervalSeconds;
            GraphBossAI[] found = FindObjectsByType<GraphBossAI>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            bosses.Clear();
            for (int i = 0; i < found.Length; i++)
            {
                GraphBossAI boss = found[i];
                if (boss != null && boss is not HackerHologramBoss)
                {
                    bosses.Add(boss);
                }
            }

            bosses.Sort((left, right) => string.CompareOrdinal(left.DisplayName, right.DisplayName));
            if (selectedBoss == null || !bosses.Contains(selectedBoss))
            {
                selectedBoss = bosses.Count > 0 ? bosses[0] : null;
                patternScrollPosition = Vector2.zero;
                statusMessage = null;
            }
        }

        private static string FormatPatternId(string patternId)
        {
            return string.IsNullOrWhiteSpace(patternId) ? "-" : patternId;
        }

        private void SetPointerCaptured(bool captured)
        {
            if (isPointerCaptured == captured)
            {
                return;
            }

            isPointerCaptured = captured;
            if (captured)
            {
                CursorController.PushForceSystemCursorVisible();
                PlayerCombatController.PushPointerInputSuppression();
            }
            else
            {
                PlayerCombatController.PopPointerInputSuppression();
                CursorController.PopForceSystemCursorVisible();
            }
        }

        private static Rect GetPanelRect()
        {
            float width = Mathf.Min(PanelWidth, Mathf.Max(180f, Screen.width - 24f));
            float height = Mathf.Max(120f, Screen.height - 24f);
            return new Rect(Screen.width - width - 12f, 12f, width, height);
        }

        private static bool IsPointerOverPanel()
        {
            if (!TryGetPointerScreenPosition(out Vector2 screenPosition))
            {
                return false;
            }

            Vector2 guiPosition = new(screenPosition.x, Screen.height - screenPosition.y);
            return GetPanelRect().Contains(guiPosition);
        }

        private static bool TryGetPointerScreenPosition(out Vector2 screenPosition)
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                screenPosition = default;
                return false;
            }

            screenPosition = mouse.position.ReadValue();
            return true;
#else
            screenPosition = Input.mousePosition;
            return true;
#endif
        }

        private void EnsureOpaquePanelStyle()
        {
            if (opaquePanelStyle != null)
            {
                return;
            }

            opaquePanelTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "Boss QA Panel Background"
            };
            opaquePanelTexture.SetPixel(0, 0, new Color32(24, 26, 32, 255));
            opaquePanelTexture.Apply(false, true);

            opaquePanelStyle = new GUIStyle(GUI.skin.window);
            opaquePanelStyle.border = new RectOffset();
            opaquePanelStyle.normal.background = opaquePanelTexture;
            opaquePanelStyle.hover.background = opaquePanelTexture;
            opaquePanelStyle.active.background = opaquePanelTexture;
            opaquePanelStyle.focused.background = opaquePanelTexture;
        }

        private static bool WasToggleKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current?.f12Key.wasPressedThisFrame == true;
#else
            return Input.GetKeyDown(KeyCode.F12);
#endif
        }
    }
}
#endif
