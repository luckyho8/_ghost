using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BlockData))]
public class BlockDataEditor : Editor
{
    private const int GridSize = 4;
    private const int CellSize = 36;
    private const int PivotIndex = 0; // 좌측 하단 = 피벗

    private static readonly Color PivotTint = new Color(1f, 1f, 0.4f, 1f);
    private static readonly Color EmptyColor = new Color(0.22f, 0.22f, 0.22f, 0.8f);

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        BlockData data = (BlockData)target;
        SerializedProperty blockID = serializedObject.FindProperty("blockID");
        SerializedProperty blockName = serializedObject.FindProperty("blockName");
        SerializedProperty blockPrefab = serializedObject.FindProperty("blockPrefab");
        SerializedProperty tier = serializedObject.FindProperty("tier");
        SerializedProperty blockColor = serializedObject.FindProperty("blockColor");
        SerializedProperty shapeData = serializedObject.FindProperty("shapeData");

        EditorGUILayout.PropertyField(blockID);
        EditorGUILayout.PropertyField(blockName);
        EditorGUILayout.PropertyField(blockPrefab);
        EditorGUILayout.PropertyField(tier);
        EditorGUILayout.PropertyField(blockColor);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Shape (4×4)", EditorStyles.boldLabel);

        if (shapeData.arraySize != 16)
            shapeData.arraySize = 16;

        DrawShapeGrid(shapeData, data.blockColor);

        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox("클릭하여 셀을 토글. P = 피벗(좌측 하단, Index 0).", MessageType.None);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawShapeGrid(SerializedProperty shapeData, Color blockColor)
    {
        Rect gridRect = GUILayoutUtility.GetRect(
            GridSize * CellSize + (GridSize - 1) * 2 + 20,
            GridSize * CellSize + (GridSize - 1) * 2 + 20
        );

        float startX = gridRect.x + 10f;
        float startY = gridRect.y + 10f;

        Event e = Event.current;

        float cellStep = CellSize + 2;
        for (int row = 0; row < GridSize; row++)
        {
            for (int col = 0; col < GridSize; col++)
            {
                int index = row * GridSize + col;
                float x = startX + col * cellStep;
                float y = startY + (GridSize - 1 - row) * cellStep;
                Rect cellRect = new Rect(x, y, CellSize, CellSize);

                bool isPivot = (index == PivotIndex);
                bool filled = shapeData.GetArrayElementAtIndex(index).boolValue;

                Color bg = filled ? new Color(blockColor.r, blockColor.g, blockColor.b, 0.95f) : EmptyColor;
                if (isPivot)
                    bg = Color.Lerp(bg, PivotTint, 0.5f);

                EditorGUI.DrawRect(cellRect, bg);

                if (isPivot)
                {
                    float t = 2f;
                    EditorGUI.DrawRect(new Rect(cellRect.x - t, cellRect.y - t, cellRect.width + t * 2, t), Color.yellow);
                    EditorGUI.DrawRect(new Rect(cellRect.x - t, cellRect.yMax, cellRect.width + t * 2, t), Color.yellow);
                    EditorGUI.DrawRect(new Rect(cellRect.x - t, cellRect.y - t, t, cellRect.height + t * 2), Color.yellow);
                    EditorGUI.DrawRect(new Rect(cellRect.xMax, cellRect.y - t, t, cellRect.height + t * 2), Color.yellow);
                    var pivotStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                    pivotStyle.normal.textColor = Color.black;
                    EditorGUI.LabelField(cellRect, "P", pivotStyle);
                }

                if (e.type == EventType.MouseDown && e.button == 0 && cellRect.Contains(e.mousePosition))
                {
                    shapeData.GetArrayElementAtIndex(index).boolValue = !filled;
                    e.Use();
                    GUI.changed = true;
                }
            }
        }
    }
}
