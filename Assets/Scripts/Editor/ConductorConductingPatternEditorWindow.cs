using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Week14.Enemy;

public sealed class ConductorConductingPatternEditorWindow : EditorWindow
{
    private const string PatternsPropertyName = "conductingPatterns";
    private const float SidebarWidth = 240f;
    private const float CanvasPadding = 12f;
    private const float PixelsPerUnit = 120f;
    private const float MinPointDistancePixels = 4f;

    private UnityEngine.Object owner;
    private SerializedObject ownerObject;
    private int selectedPatternIndex;
    private int activeStrokeIndex = -1;

    public static void Open(UnityEngine.Object nextOwner, int patternIndex)
    {
        ConductorConductingPatternEditorWindow window =
            GetWindow<ConductorConductingPatternEditorWindow>("Conductor Cue Shape");
        window.owner = nextOwner;
        window.ownerObject = nextOwner != null ? new SerializedObject(nextOwner) : null;
        window.selectedPatternIndex = Mathf.Max(0, patternIndex);
        window.Show();
    }

    private void OnGUI()
    {
        if (owner == null)
        {
            EditorGUILayout.HelpBox("Conductor를 먼저 선택하세요.", MessageType.Info);
            return;
        }

        ownerObject ??= new SerializedObject(owner);
        ownerObject.Update();

        SerializedProperty patterns = ownerObject.FindProperty(PatternsPropertyName);
        if (patterns == null)
        {
            EditorGUILayout.HelpBox("선택한 오브젝트에 지휘 모양 데이터가 없습니다.", MessageType.Warning);
            return;
        }

        selectedPatternIndex = Mathf.Clamp(selectedPatternIndex, 0, Mathf.Max(0, patterns.arraySize - 1));

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawSidebar(patterns, GUILayout.Width(SidebarWidth));
            DrawCanvasArea(patterns);
        }

        ownerObject.ApplyModifiedProperties();
    }

    private void DrawSidebar(SerializedProperty patterns, GUILayoutOption width)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, width))
        {
            EditorGUILayout.LabelField("Shape List", EditorStyles.boldLabel);
            for (int i = 0; i < patterns.arraySize; i++)
            {
                SerializedProperty pattern = patterns.GetArrayElementAtIndex(i);
                string id = pattern.FindPropertyRelative("patternId")?.stringValue;
                if (GUILayout.Toggle(selectedPatternIndex == i, string.IsNullOrWhiteSpace(id) ? $"Shape {i + 1}" : id, "Button"))
                {
                    selectedPatternIndex = i;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+"))
                {
                    AddPattern(patterns);
                    selectedPatternIndex = patterns.arraySize - 1;
                }

                using (new EditorGUI.DisabledScope(patterns.arraySize == 0))
                {
                    if (GUILayout.Button("-"))
                    {
                        patterns.DeleteArrayElementAtIndex(selectedPatternIndex);
                        selectedPatternIndex = Mathf.Clamp(selectedPatternIndex, 0, Mathf.Max(0, patterns.arraySize - 1));
                        return;
                    }
                }
            }

            if (patterns.arraySize == 0)
            {
                return;
            }

            SerializedProperty selectedPattern = patterns.GetArrayElementAtIndex(selectedPatternIndex);
            EditorGUILayout.Space(6f);
            EditorGUILayout.PropertyField(selectedPattern.FindPropertyRelative("patternId"), new GUIContent("Pattern ID"));

            SerializedProperty strokes = selectedPattern.FindPropertyRelative("strokes");
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Stroke Order", EditorStyles.boldLabel);
            if (strokes == null)
            {
                return;
            }

            for (int i = 0; i < strokes.arraySize; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Stroke {i + 1}", GUILayout.Width(72f));
                    using (new EditorGUI.DisabledScope(i <= 0))
                    {
                        if (GUILayout.Button("Up", GUILayout.Width(34f)))
                        {
                            strokes.MoveArrayElement(i, i - 1);
                        }
                    }

                    using (new EditorGUI.DisabledScope(i >= strokes.arraySize - 1))
                    {
                        if (GUILayout.Button("Dn", GUILayout.Width(34f)))
                        {
                            strokes.MoveArrayElement(i, i + 1);
                        }
                    }

                    if (GUILayout.Button("X", GUILayout.Width(24f)))
                    {
                        strokes.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }
            }

            if (GUILayout.Button("Clear Strokes"))
            {
                strokes.ClearArray();
            }
        }
    }

    private void DrawCanvasArea(SerializedProperty patterns)
    {
        Rect canvasRect = GUILayoutUtility.GetRect(
            100f,
            10000f,
            100f,
            10000f,
            GUILayout.ExpandWidth(true),
            GUILayout.ExpandHeight(true));
        canvasRect = new Rect(
            canvasRect.x + CanvasPadding,
            canvasRect.y + CanvasPadding,
            canvasRect.width - CanvasPadding * 2f,
            canvasRect.height - CanvasPadding * 2f);

        DrawCanvasBackground(canvasRect);
        if (patterns.arraySize == 0)
        {
            return;
        }

        SerializedProperty pattern = patterns.GetArrayElementAtIndex(selectedPatternIndex);
        SerializedProperty strokes = pattern.FindPropertyRelative("strokes");
        if (strokes == null)
        {
            return;
        }

        HandleCanvasInput(canvasRect, strokes);
        DrawStrokes(canvasRect, strokes);
    }

    private static void DrawCanvasBackground(Rect rect)
    {
        EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));
        Handles.BeginGUI();
        Handles.color = new Color(1f, 1f, 1f, 0.12f);
        Vector2 center = rect.center;
        Handles.DrawLine(new Vector3(rect.xMin, center.y), new Vector3(rect.xMax, center.y));
        Handles.DrawLine(new Vector3(center.x, rect.yMin), new Vector3(center.x, rect.yMax));
        Handles.EndGUI();
    }

    private void HandleCanvasInput(Rect rect, SerializedProperty strokes)
    {
        Event currentEvent = Event.current;
        if (!rect.Contains(currentEvent.mousePosition))
        {
            return;
        }

        if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
        {
            activeStrokeIndex = strokes.arraySize;
            strokes.InsertArrayElementAtIndex(activeStrokeIndex);
            SerializedProperty points = strokes.GetArrayElementAtIndex(activeStrokeIndex).FindPropertyRelative("points");
            points.ClearArray();
            AddPoint(points, GuiToShapePoint(rect, currentEvent.mousePosition));
            currentEvent.Use();
        }
        else if (currentEvent.type == EventType.MouseDrag && currentEvent.button == 0 && activeStrokeIndex >= 0)
        {
            SerializedProperty points = strokes.GetArrayElementAtIndex(activeStrokeIndex).FindPropertyRelative("points");
            Vector2 nextPoint = GuiToShapePoint(rect, currentEvent.mousePosition);
            if (ShouldAppendPoint(rect, points, nextPoint))
            {
                AddPoint(points, nextPoint);
            }

            currentEvent.Use();
            Repaint();
        }
        else if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0 && activeStrokeIndex >= 0)
        {
            SerializedProperty points = strokes.GetArrayElementAtIndex(activeStrokeIndex).FindPropertyRelative("points");
            if (points.arraySize < 2)
            {
                strokes.DeleteArrayElementAtIndex(activeStrokeIndex);
            }

            activeStrokeIndex = -1;
            currentEvent.Use();
        }
    }

    private static bool ShouldAppendPoint(Rect rect, SerializedProperty points, Vector2 nextPoint)
    {
        if (points.arraySize == 0)
        {
            return true;
        }

        Vector2 previousPoint = points.GetArrayElementAtIndex(points.arraySize - 1).vector2Value;
        return Vector2.Distance(ShapeToGuiPoint(rect, previousPoint), ShapeToGuiPoint(rect, nextPoint)) >= MinPointDistancePixels;
    }

    private static void DrawStrokes(Rect rect, SerializedProperty strokes)
    {
        Handles.BeginGUI();
        for (int i = 0; i < strokes.arraySize; i++)
        {
            SerializedProperty points = strokes.GetArrayElementAtIndex(i).FindPropertyRelative("points");
            if (points == null || points.arraySize == 0)
            {
                continue;
            }

            List<Vector3> guiPoints = new();
            for (int j = 0; j < points.arraySize; j++)
            {
                guiPoints.Add(ShapeToGuiPoint(rect, points.GetArrayElementAtIndex(j).vector2Value));
            }

            Handles.color = Color.Lerp(new Color(0.5f, 0.9f, 1f, 1f), Color.white, i * 0.08f);
            if (guiPoints.Count >= 2)
            {
                Handles.DrawAAPolyLine(4f, guiPoints.ToArray());
            }

            for (int j = 0; j < guiPoints.Count; j++)
            {
                Rect pointRect = new(guiPoints[j].x - 2f, guiPoints[j].y - 2f, 4f, 4f);
                EditorGUI.DrawRect(pointRect, Color.white);
            }
        }

        Handles.EndGUI();
    }

    private static Vector2 GuiToShapePoint(Rect rect, Vector2 guiPoint)
    {
        Vector2 delta = guiPoint - rect.center;
        return new Vector2(delta.x / PixelsPerUnit, -delta.y / PixelsPerUnit);
    }

    private static Vector2 ShapeToGuiPoint(Rect rect, Vector2 shapePoint)
    {
        return rect.center + new Vector2(shapePoint.x * PixelsPerUnit, -shapePoint.y * PixelsPerUnit);
    }

    private static void AddPattern(SerializedProperty patterns)
    {
        int index = patterns.arraySize;
        patterns.InsertArrayElementAtIndex(index);
        SerializedProperty pattern = patterns.GetArrayElementAtIndex(index);
        pattern.FindPropertyRelative("patternId").stringValue = $"Pattern{index + 1}";
        pattern.FindPropertyRelative("strokes")?.ClearArray();
    }

    private static void AddPoint(SerializedProperty points, Vector2 point)
    {
        int index = points.arraySize;
        points.InsertArrayElementAtIndex(index);
        points.GetArrayElementAtIndex(index).vector2Value = point;
    }
}

[CustomPropertyDrawer(typeof(ConductorConductingPatternIdAttribute))]
internal sealed class ConductorConductingPatternIdDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        List<string> ids = GetPatternIds();
        if (property.propertyType != SerializedPropertyType.String || ids.Count == 0)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        List<string> values = new() { string.Empty };
        List<GUIContent> labels = new() { new GUIContent("<None>") };
        for (int i = 0; i < ids.Count; i++)
        {
            values.Add(ids[i]);
            labels.Add(new GUIContent(ids[i]));
        }

        if (!string.IsNullOrWhiteSpace(property.stringValue) && !values.Contains(property.stringValue))
        {
            values.Add(property.stringValue);
            labels.Add(new GUIContent($"{property.stringValue} (Missing)"));
        }

        int currentIndex = Mathf.Max(0, values.IndexOf(property.stringValue));
        int nextIndex = EditorGUI.Popup(position, label, currentIndex, labels.ToArray());
        property.stringValue = values[nextIndex];
    }

    private static List<string> GetPatternIds()
    {
        List<string> ids = new();
        Conductor conductor = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponentInParent<Conductor>()
            : null;
        if (conductor == null)
        {
            Conductor[] conductors = UnityEngine.Object.FindObjectsByType<Conductor>(FindObjectsSortMode.None);
            conductor = conductors != null && conductors.Length > 0 ? conductors[0] : null;
        }

        if (conductor?.ConductingPatterns == null)
        {
            return ids;
        }

        foreach (ConductorConductingPattern pattern in conductor.ConductingPatterns)
        {
            if (pattern != null && !string.IsNullOrWhiteSpace(pattern.PatternId) && !ids.Contains(pattern.PatternId))
            {
                ids.Add(pattern.PatternId);
            }
        }

        return ids;
    }
}
