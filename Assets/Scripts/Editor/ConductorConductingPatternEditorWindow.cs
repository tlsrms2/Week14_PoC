using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Week14.Enemy;

public sealed class ConductorConductingPatternEditorWindow : EditorWindow
{
    private enum DragHandle
    {
        None,
        Start,
        End,
        Control
    }

    private const string PatternsPropertyName = "conductingPatterns";
    private const float SidebarWidth = 248f;
    private const float CanvasPadding = 12f;
    private const float PixelsPerUnit = 120f;
    private const float HandleHitRadius = 9f;
    private const float StrokeHitDistance = 9f;
    private const float MinStrokeLengthPixels = 8f;
    private const int CurveSegments = 28;

    private readonly List<Vector3> drawPoints = new();

    private UnityEngine.Object owner;
    private SerializedObject ownerObject;
    private int selectedPatternIndex;
    private int selectedStrokeIndex = -1;
    private int draggingStrokeIndex = -1;
    private DragHandle draggingHandle;

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
                    selectedStrokeIndex = -1;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+"))
                {
                    AddPattern(patterns);
                    selectedPatternIndex = patterns.arraySize - 1;
                    selectedStrokeIndex = -1;
                }

                using (new EditorGUI.DisabledScope(patterns.arraySize == 0))
                {
                    if (GUILayout.Button("-"))
                    {
                        patterns.DeleteArrayElementAtIndex(selectedPatternIndex);
                        selectedPatternIndex = Mathf.Clamp(selectedPatternIndex, 0, Mathf.Max(0, patterns.arraySize - 1));
                        selectedStrokeIndex = -1;
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
            if (strokes == null)
            {
                return;
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Stroke Order", EditorStyles.boldLabel);
            selectedStrokeIndex = Mathf.Clamp(selectedStrokeIndex, -1, strokes.arraySize - 1);

            for (int i = 0; i < strokes.arraySize; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Toggle(selectedStrokeIndex == i, $"Stroke {i + 1}", "Button", GUILayout.Width(84f)))
                    {
                        selectedStrokeIndex = i;
                    }

                    using (new EditorGUI.DisabledScope(i <= 0))
                    {
                        if (GUILayout.Button("Up", GUILayout.Width(34f)))
                        {
                            strokes.MoveArrayElement(i, i - 1);
                            selectedStrokeIndex = i - 1;
                        }
                    }

                    using (new EditorGUI.DisabledScope(i >= strokes.arraySize - 1))
                    {
                        if (GUILayout.Button("Dn", GUILayout.Width(34f)))
                        {
                            strokes.MoveArrayElement(i, i + 1);
                            selectedStrokeIndex = i + 1;
                        }
                    }

                    if (GUILayout.Button("X", GUILayout.Width(24f)))
                    {
                        strokes.DeleteArrayElementAtIndex(i);
                        selectedStrokeIndex = Mathf.Clamp(selectedStrokeIndex, -1, strokes.arraySize - 1);
                        break;
                    }
                }
            }

            if (GUILayout.Button("Clear Strokes"))
            {
                strokes.ClearArray();
                selectedStrokeIndex = -1;
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

        DrawStrokes(canvasRect, strokes);
        HandleCanvasInput(canvasRect, strokes);
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
        if (!rect.Contains(currentEvent.mousePosition)
            && currentEvent.type != EventType.MouseDrag
            && currentEvent.type != EventType.MouseUp)
        {
            return;
        }

        Vector2 shapePoint = GuiToShapePoint(rect, currentEvent.mousePosition);
        if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
        {
            if (TryBeginExistingStrokeDrag(rect, strokes, currentEvent.mousePosition))
            {
                currentEvent.Use();
                return;
            }

            if (currentEvent.clickCount == 2 && TryCreateControlPoint(rect, strokes, currentEvent.mousePosition))
            {
                currentEvent.Use();
                return;
            }

            CreateStroke(strokes, shapePoint);
            selectedStrokeIndex = strokes.arraySize - 1;
            draggingStrokeIndex = selectedStrokeIndex;
            draggingHandle = DragHandle.End;
            currentEvent.Use();
        }
        else if (currentEvent.type == EventType.MouseDrag && currentEvent.button == 0 && draggingStrokeIndex >= 0)
        {
            SetStrokeHandle(strokes.GetArrayElementAtIndex(draggingStrokeIndex), draggingHandle, shapePoint);
            currentEvent.Use();
            Repaint();
        }
        else if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0 && draggingStrokeIndex >= 0)
        {
            SerializedProperty stroke = strokes.GetArrayElementAtIndex(draggingStrokeIndex);
            if (draggingHandle == DragHandle.End && GetStrokeLengthPixels(rect, stroke) < MinStrokeLengthPixels)
            {
                strokes.DeleteArrayElementAtIndex(draggingStrokeIndex);
                selectedStrokeIndex = Mathf.Clamp(selectedStrokeIndex - 1, -1, strokes.arraySize - 1);
            }

            draggingStrokeIndex = -1;
            draggingHandle = DragHandle.None;
            currentEvent.Use();
        }
    }

    private bool TryBeginExistingStrokeDrag(Rect rect, SerializedProperty strokes, Vector2 mousePosition)
    {
        for (int i = strokes.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty stroke = strokes.GetArrayElementAtIndex(i);
            if (TryHitHandle(rect, stroke, mousePosition, out DragHandle handle))
            {
                selectedStrokeIndex = i;
                draggingStrokeIndex = i;
                draggingHandle = handle;
                return true;
            }
        }

        return false;
    }

    private bool TryCreateControlPoint(Rect rect, SerializedProperty strokes, Vector2 mousePosition)
    {
        int hitIndex = FindStrokeNearPoint(rect, strokes, mousePosition);
        if (hitIndex < 0)
        {
            return false;
        }

        SerializedProperty stroke = strokes.GetArrayElementAtIndex(hitIndex);
        selectedStrokeIndex = hitIndex;
        SetBool(stroke, "hasControlPoint", true);
        SetVector2(stroke, "controlPoint", GuiToShapePoint(rect, mousePosition));
        draggingStrokeIndex = hitIndex;
        draggingHandle = DragHandle.Control;
        return true;
    }

    private int FindStrokeNearPoint(Rect rect, SerializedProperty strokes, Vector2 mousePosition)
    {
        float bestDistance = StrokeHitDistance;
        int bestIndex = -1;
        for (int i = 0; i < strokes.arraySize; i++)
        {
            float distance = GetDistanceToStroke(rect, strokes.GetArrayElementAtIndex(i), mousePosition);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static bool TryHitHandle(Rect rect, SerializedProperty stroke, Vector2 mousePosition, out DragHandle handle)
    {
        handle = DragHandle.None;
        if (Vector2.Distance(ShapeToGuiPoint(rect, GetVector2(stroke, "start")), mousePosition) <= HandleHitRadius)
        {
            handle = DragHandle.Start;
            return true;
        }

        if (Vector2.Distance(ShapeToGuiPoint(rect, GetVector2(stroke, "end")), mousePosition) <= HandleHitRadius)
        {
            handle = DragHandle.End;
            return true;
        }

        if (GetBool(stroke, "hasControlPoint")
            && Vector2.Distance(ShapeToGuiPoint(rect, GetVector2(stroke, "controlPoint")), mousePosition) <= HandleHitRadius)
        {
            handle = DragHandle.Control;
            return true;
        }

        return false;
    }

    private static void CreateStroke(SerializedProperty strokes, Vector2 point)
    {
        int index = strokes.arraySize;
        strokes.InsertArrayElementAtIndex(index);
        SerializedProperty stroke = strokes.GetArrayElementAtIndex(index);
        SetVector2(stroke, "start", point);
        SetVector2(stroke, "end", point);
        SetBool(stroke, "hasControlPoint", false);
        SetVector2(stroke, "controlPoint", point);
        SetLegacyPointsEmpty(stroke);
        strokes.serializedObject.ApplyModifiedProperties();
    }

    private void DrawStrokes(Rect rect, SerializedProperty strokes)
    {
        Handles.BeginGUI();
        for (int i = 0; i < strokes.arraySize; i++)
        {
            SerializedProperty stroke = strokes.GetArrayElementAtIndex(i);
            BuildStrokeGuiPoints(rect, stroke, drawPoints);
            if (drawPoints.Count < 2)
            {
                continue;
            }

            bool selected = selectedStrokeIndex == i;
            Handles.color = selected ? new Color(1f, 0.92f, 0.3f, 1f) : new Color(0.5f, 0.9f, 1f, 1f);
            Handles.DrawAAPolyLine(selected ? 5f : 4f, drawPoints.ToArray());
            DrawStrokeHandles(rect, stroke, selected);
        }

        Handles.EndGUI();
    }

    private static void DrawStrokeHandles(Rect rect, SerializedProperty stroke, bool selected)
    {
        Vector2 start = ShapeToGuiPoint(rect, GetVector2(stroke, "start"));
        Vector2 end = ShapeToGuiPoint(rect, GetVector2(stroke, "end"));
        DrawHandle(start, selected ? Color.yellow : Color.white, 4f);
        DrawHandle(end, selected ? Color.yellow : Color.white, 4f);

        if (!GetBool(stroke, "hasControlPoint"))
        {
            return;
        }

        Vector2 control = ShapeToGuiPoint(rect, GetVector2(stroke, "controlPoint"));
        Handles.color = new Color(1f, 1f, 1f, 0.32f);
        Handles.DrawLine(start, control);
        Handles.DrawLine(control, end);
        DrawHandle(control, new Color(1f, 0.55f, 0.2f, 1f), 5f);
    }

    private static void DrawHandle(Vector2 position, Color color, float size)
    {
        Rect rect = new(position.x - size, position.y - size, size * 2f, size * 2f);
        EditorGUI.DrawRect(rect, color);
    }

    private static void BuildStrokeGuiPoints(Rect rect, SerializedProperty stroke, List<Vector3> results)
    {
        results.Clear();
        Vector2 start = GetVector2(stroke, "start");
        Vector2 end = GetVector2(stroke, "end");
        if (!GetBool(stroke, "hasControlPoint"))
        {
            results.Add(ShapeToGuiPoint(rect, start));
            results.Add(ShapeToGuiPoint(rect, end));
            return;
        }

        Vector2 control = GetVector2(stroke, "controlPoint");
        for (int i = 0; i <= CurveSegments; i++)
        {
            float t = (float)i / CurveSegments;
            results.Add(ShapeToGuiPoint(rect, EvaluateQuadratic(start, control, end, t)));
        }
    }

    private static float GetDistanceToStroke(Rect rect, SerializedProperty stroke, Vector2 mousePosition)
    {
        Vector2 start = ShapeToGuiPoint(rect, GetVector2(stroke, "start"));
        Vector2 end = ShapeToGuiPoint(rect, GetVector2(stroke, "end"));
        if (!GetBool(stroke, "hasControlPoint"))
        {
            return DistanceToSegment(mousePosition, start, end);
        }

        Vector2 control = GetVector2(stroke, "controlPoint");
        float bestDistance = float.MaxValue;
        Vector2 previous = start;
        for (int i = 1; i <= CurveSegments; i++)
        {
            float t = (float)i / CurveSegments;
            Vector2 current = ShapeToGuiPoint(rect, EvaluateQuadratic(GetVector2(stroke, "start"), control, GetVector2(stroke, "end"), t));
            bestDistance = Mathf.Min(bestDistance, DistanceToSegment(mousePosition, previous, current));
            previous = current;
        }

        return bestDistance;
    }

    private static float GetStrokeLengthPixels(Rect rect, SerializedProperty stroke)
    {
        return Vector2.Distance(
            ShapeToGuiPoint(rect, GetVector2(stroke, "start")),
            ShapeToGuiPoint(rect, GetVector2(stroke, "end")));
    }

    private static void SetStrokeHandle(SerializedProperty stroke, DragHandle handle, Vector2 value)
    {
        switch (handle)
        {
            case DragHandle.Start:
                SetVector2(stroke, "start", value);
                break;
            case DragHandle.End:
                SetVector2(stroke, "end", value);
                break;
            case DragHandle.Control:
                SetBool(stroke, "hasControlPoint", true);
                SetVector2(stroke, "controlPoint", value);
                break;
        }
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

    private static Vector2 EvaluateQuadratic(Vector2 start, Vector2 control, Vector2 end, float t)
    {
        float inverse = 1f - t;
        return inverse * inverse * start + 2f * inverse * t * control + t * t * end;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSqr = segment.sqrMagnitude;
        if (lengthSqr <= 0.0001f)
        {
            return Vector2.Distance(point, start);
        }

        float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSqr);
        return Vector2.Distance(point, start + segment * t);
    }

    private static void AddPattern(SerializedProperty patterns)
    {
        int index = patterns.arraySize;
        patterns.InsertArrayElementAtIndex(index);
        SerializedProperty pattern = patterns.GetArrayElementAtIndex(index);
        pattern.FindPropertyRelative("patternId").stringValue = $"Pattern{index + 1}";
        pattern.FindPropertyRelative("strokes")?.ClearArray();
    }

    private static Vector2 GetVector2(SerializedProperty root, string propertyName)
    {
        return root.FindPropertyRelative(propertyName)?.vector2Value ?? Vector2.zero;
    }

    private static bool GetBool(SerializedProperty root, string propertyName)
    {
        return root.FindPropertyRelative(propertyName)?.boolValue == true;
    }

    private static void SetVector2(SerializedProperty root, string propertyName, Vector2 value)
    {
        SerializedProperty property = root.FindPropertyRelative(propertyName);
        if (property != null)
        {
            property.vector2Value = value;
        }
    }

    private static void SetBool(SerializedProperty root, string propertyName, bool value)
    {
        SerializedProperty property = root.FindPropertyRelative(propertyName);
        if (property != null)
        {
            property.boolValue = value;
        }
    }

    private static void SetLegacyPointsEmpty(SerializedProperty stroke)
    {
        SerializedProperty points = stroke.FindPropertyRelative("points");
        points?.ClearArray();
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
