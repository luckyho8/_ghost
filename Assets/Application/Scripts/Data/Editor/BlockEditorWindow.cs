using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Ghost.Editor
{
    /// <summary>
    /// Ghost Piece 통합 블록 에디터. 단일 AllBlockData SO 저장, ReorderableList, 피벗 Index 0.
    /// </summary>
    public class BlockEditorWindow : EditorWindow
    {
        private const string MenuPath = "Ghost/Block Editor";
        private const string DataFolderPath = "Assets/Application/Data";
        private const string DefaultDataFileName = "AllBlockData.asset";
        private const string EditorPrefsKeyPrefab = "Ghost.BlockEditor.PrefabPath";
        private const string EditorPrefsKeyLastDataPath = "Ghost.BlockEditor.LastAllBlockDataPath";
        private const int GridSize = 4;
        private const int GridCellSize = 36;
        private const int PivotIndex = 0;
        private const int ThumbnailSize = 48;
        private const float GridLineWidth = 1f;

        private AllBlockData allBlockData;
        private SerializedObject serializedObject;
        private SerializedProperty blockListProp;
        private ReorderableList reorderableList;

        private Vector2 listScroll;
        private Vector2 leftScroll;
        private int currentIndex = -1;

        private int editTier = 1;
        private Color editBlockColor = Color.white;
        private bool[] editShapeData = new bool[16];
        private GameObject editPrefab;

        private string saveAsFileName = "NewBlockData";

        private static readonly Color EmptyColor = new Color(0.22f, 0.22f, 0.22f, 0.8f);
        private static readonly Color PivotTint = new Color(1f, 1f, 0.4f, 1f);
        private static readonly Color GridLineColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);

        private readonly Dictionary<string, Texture2D> thumbnailCache = new Dictionary<string, Texture2D>();

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var w = GetWindow<BlockEditorWindow>("Block Editor");
            w.minSize = new Vector2(520, 420);
        }

        private void OnEnable()
        {
            LoadPrefabFromEditorPrefs();
            LoadOrCreateAllBlockData();
            thumbnailCache.Clear();
        }

        private void OnFocus()
        {
            if (allBlockData != null && serializedObject != null && serializedObject.targetObject != null)
                serializedObject.Update();
        }

        private void LoadPrefabFromEditorPrefs()
        {
            string path = EditorPrefs.GetString(EditorPrefsKeyPrefab, "");
            if (string.IsNullOrEmpty(path)) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
                editPrefab = prefab;
        }

        private void SavePrefabToEditorPrefs()
        {
            if (editPrefab == null)
                EditorPrefs.DeleteKey(EditorPrefsKeyPrefab);
            else
            {
                string path = AssetDatabase.GetAssetPath(editPrefab);
                if (!string.IsNullOrEmpty(path))
                    EditorPrefs.SetString(EditorPrefsKeyPrefab, path);
            }
        }

        private void LoadOrCreateAllBlockData()
        {
            EnsureDataFolderExists();
            string path = EditorPrefs.GetString(EditorPrefsKeyLastDataPath, DataFolderPath + "/" + DefaultDataFileName);
            allBlockData = AssetDatabase.LoadAssetAtPath<AllBlockData>(path);
            if (allBlockData == null)
            {
                path = DataFolderPath + "/" + DefaultDataFileName;
                allBlockData = AssetDatabase.LoadAssetAtPath<AllBlockData>(path);
            }
            if (allBlockData == null)
            {
                allBlockData = CreateInstance<AllBlockData>();
                allBlockData.blockList = new List<BlockDataContents>();
                AssetDatabase.CreateAsset(allBlockData, path);
                AssetDatabase.SaveAssets();
            }
            EditorPrefs.SetString(EditorPrefsKeyLastDataPath, AssetDatabase.GetAssetPath(allBlockData));
            BuildSerializedList();
        }

        private void BuildSerializedList()
        {
            if (allBlockData == null) return;
            serializedObject = new SerializedObject(allBlockData);
            blockListProp = serializedObject.FindProperty("blockList");
            if (blockListProp == null) return;

            reorderableList = new ReorderableList(serializedObject, blockListProp, true, true, true, true)
            {
                elementHeight = ThumbnailSize + 10,
                drawHeaderCallback = r => EditorGUI.LabelField(r, "Block List"),
                drawElementCallback = DrawListElement,
                onSelectCallback = list =>
                {
                    currentIndex = list.index;
                    LoadFromElement(currentIndex);
                    Repaint();
                },
                onReorderCallback = list =>
                {
                    SyncBlockIdsToIndices();
                    Repaint();
                }
            };
            currentIndex = -1;
            thumbnailCache.Clear();
        }

        private void SwitchToAllBlockData(AllBlockData data)
        {
            if (data == null || data == allBlockData) return;
            allBlockData = data;
            EditorPrefs.SetString(EditorPrefsKeyLastDataPath, AssetDatabase.GetAssetPath(allBlockData));
            BuildSerializedList();
            LoadFromElement(-1);
            Repaint();
        }

        private static void EnsureDataFolderExists()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Application"))
            {
                if (!AssetDatabase.IsValidFolder("Assets")) return;
                AssetDatabase.CreateFolder("Assets", "Application");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Application/Data"))
                AssetDatabase.CreateFolder("Assets/Application", "Data");
        }

        private static BlockDataContents CloneBlockContents(BlockDataContents src)
        {
            var d = new BlockDataContents();
            d.blockID = src.blockID;
            d.blockName = src.blockName ?? "";
            d.blockPrefab = src.blockPrefab;
            d.tier = src.tier;
            d.blockColor = src.blockColor;
            d.shapeData = new bool[16];
            if (src.shapeData != null)
                for (int i = 0; i < 16 && i < src.shapeData.Length; i++)
                    d.shapeData[i] = src.shapeData[i];
            return d;
        }

        private void SyncBlockIdsToIndices()
        {
            if (blockListProp == null) return;
            for (int i = 0; i < blockListProp.arraySize; i++)
            {
                var el = blockListProp.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("blockID").intValue = i;
                el.FindPropertyRelative("blockName").stringValue = AllBlockData.BlockIdToDisplay(i);
            }
            serializedObject.ApplyModifiedProperties();
            thumbnailCache.Clear();
        }

        private void DrawListElement(Rect rect, int index, bool active, bool focused)
        {
            if (index < 0 || index >= blockListProp.arraySize) return;
            var element = blockListProp.GetArrayElementAtIndex(index);
            Rect thumbRect = new Rect(rect.x + 4, rect.y + 2, ThumbnailSize, ThumbnailSize);
            Texture2D thumb = GetThumbnailFromElement(element);
            if (thumb != null)
                GUI.DrawTexture(thumbRect, thumb);
            else
                EditorGUI.DrawRect(thumbRect, new Color(0.2f, 0.2f, 0.2f));

            int id = element.FindPropertyRelative("blockID").intValue;
            EditorGUI.LabelField(new Rect(thumbRect.xMax + 6, rect.y + 2, rect.width - thumbRect.width - 14, ThumbnailSize), AllBlockData.BlockIdToDisplay(id), EditorStyles.miniLabel);
        }

        private Texture2D GetThumbnailFromElement(SerializedProperty element)
        {
            if (element == null) return null;
            var shapeProp = element.FindPropertyRelative("shapeData");
            var colorProp = element.FindPropertyRelative("blockColor");
            Color fill = colorProp != null ? colorProp.colorValue : Color.gray;
            string key = element.propertyPath + "_" + (shapeProp != null && shapeProp.arraySize == 16 ? GetShapeKey(shapeProp) : "");
            if (thumbnailCache.TryGetValue(key, out var tex) && tex != null)
                return tex;

            Texture2D t = new Texture2D(ThumbnailSize, ThumbnailSize);
            Color[] pixels = new Color[ThumbnailSize * ThumbnailSize];
            int cellPixels = ThumbnailSize / GridSize;
            // Unity Texture2D: 픽셀 배열 첫 행(py=0)이 텍스처 하단에 그려짐 → 데이터 row 0(피벗)이 썸네일 하단에 오도록 row = rowVisual
            for (int py = 0; py < ThumbnailSize; py++)
            {
                for (int px = 0; px < ThumbnailSize; px++)
                {
                    int row = py / cellPixels;
                    int col = px / cellPixels;
                    if (row >= GridSize) row = GridSize - 1;
                    if (col >= GridSize) col = GridSize - 1;
                    int idx = row * GridSize + col;
                    bool filled = shapeProp != null && shapeProp.arraySize > idx && shapeProp.GetArrayElementAtIndex(idx).boolValue;
                    pixels[py * ThumbnailSize + px] = filled ? fill : EmptyColor;
                }
            }
            t.SetPixels(pixels);
            t.Apply();
            thumbnailCache[key] = t;
            return t;
        }

        private static string GetShapeKey(SerializedProperty shapeProp)
        {
            var sb = new System.Text.StringBuilder(16);
            for (int i = 0; i < shapeProp.arraySize && i < 16; i++)
                sb.Append(shapeProp.GetArrayElementAtIndex(i).boolValue ? "1" : "0");
            return sb.ToString();
        }

        private void LoadFromElement(int index)
        {
            if (blockListProp == null || index < 0 || index >= blockListProp.arraySize)
            {
                editTier = 1;
                editBlockColor = Color.white;
                if (editShapeData == null) editShapeData = new bool[16];
                for (int i = 0; i < 16; i++) editShapeData[i] = false;
                return;
            }
            var el = blockListProp.GetArrayElementAtIndex(index);
            editTier = Mathf.Clamp(el.FindPropertyRelative("tier").intValue, 1, 3);
            editBlockColor = el.FindPropertyRelative("blockColor").colorValue;
            editPrefab = el.FindPropertyRelative("blockPrefab").objectReferenceValue as GameObject ?? editPrefab;
            var shapeProp = el.FindPropertyRelative("shapeData");
            if (editShapeData == null) editShapeData = new bool[16];
            for (int i = 0; i < 16 && i < shapeProp.arraySize; i++)
                editShapeData[i] = shapeProp.GetArrayElementAtIndex(i).boolValue;
            for (int i = shapeProp.arraySize; i < 16; i++)
                editShapeData[i] = false;
        }

        private void ClearDesignOnly()
        {
            if (editShapeData == null) editShapeData = new bool[16];
            for (int i = 0; i < 16; i++)
                editShapeData[i] = false;
            if (currentIndex >= 0 && blockListProp != null && currentIndex < blockListProp.arraySize)
            {
                serializedObject.Update();
                var el = blockListProp.GetArrayElementAtIndex(currentIndex);
                var shapeProp = el.FindPropertyRelative("shapeData");
                if (shapeProp.arraySize != 16) shapeProp.arraySize = 16;
                for (int i = 0; i < 16; i++)
                    shapeProp.GetArrayElementAtIndex(i).boolValue = false;
                serializedObject.ApplyModifiedProperties();
                thumbnailCache.Clear();
            }
            Repaint();
        }

        private void OnGUI()
        {
            if (allBlockData == null || serializedObject == null || blockListProp == null)
            {
                LoadOrCreateAllBlockData();
                if (blockListProp == null) return;
            }
            serializedObject.Update();

            EditorGUILayout.BeginHorizontal();
            DrawLeftPanel();
            DrawBlockListPanel();
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawLeftPanel()
        {
            leftScroll = EditorGUILayout.BeginScrollView(leftScroll, GUILayout.Width(320));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Data File (AllBlockData)", EditorStyles.miniLabel);
            AllBlockData newData = (AllBlockData)EditorGUILayout.ObjectField(allBlockData, typeof(AllBlockData), false);
            if (newData != allBlockData)
                SwitchToAllBlockData(newData);
            if (allBlockData != null)
                EditorGUILayout.HelpBox(AssetDatabase.GetAssetPath(allBlockData), MessageType.None);
            EditorGUILayout.Space(4);

            int displayId = currentIndex >= 0 && currentIndex < blockListProp.arraySize ? currentIndex : blockListProp.arraySize;
            EditorGUILayout.LabelField($"Block {AllBlockData.BlockIdToDisplay(displayId)}", EditorStyles.boldLabel);
            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("Prefab (box_1x1)", EditorStyles.miniLabel);
            GameObject newPrefab = (GameObject)EditorGUILayout.ObjectField(editPrefab, typeof(GameObject), false);
            if (newPrefab != editPrefab)
            {
                editPrefab = newPrefab;
                SavePrefabToEditorPrefs();
                WriteCurrentPrefab(editPrefab);
            }
            if (editPrefab == null)
                EditorGUILayout.HelpBox("프리팹을 할당해 주세요.", MessageType.Warning);
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("Tier", EditorStyles.miniLabel);
            int newTier = EditorGUILayout.IntSlider(editTier, 1, 3);
            if (newTier != editTier)
            {
                editTier = newTier;
                WriteCurrentTier(editTier);
            }
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("Color (URP Base Map)", EditorStyles.miniLabel);
            Color newColor = EditorGUILayout.ColorField(editBlockColor);
            if (newColor != editBlockColor)
            {
                editBlockColor = newColor;
                WriteCurrentColor(editBlockColor);
            }
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("Shape (4×4)", EditorStyles.boldLabel);
            DrawShapeGrid();
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox("클릭하여 셀 토글. P = 피벗(좌측 하단, Index 0).", MessageType.None);
            EditorGUILayout.Space(8);

            GUI.enabled = editPrefab != null;
            if (GUILayout.Button("Save", GUILayout.Height(28)))
                SaveAll();
            GUI.enabled = true;

            EditorGUILayout.Space(6);
            if (GUILayout.Button("Clear Design", GUILayout.Height(22)))
                ClearDesignOnly();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Save As (새 파일)", EditorStyles.miniLabel);
            saveAsFileName = EditorGUILayout.TextField(saveAsFileName);
            if (GUILayout.Button("Save As", GUILayout.Height(22)))
                SaveAsNewFile();

            EditorGUILayout.EndScrollView();
        }

        private void WriteCurrentPrefab(GameObject prefab)
        {
            if (currentIndex < 0 || blockListProp == null || currentIndex >= blockListProp.arraySize) return;
            blockListProp.GetArrayElementAtIndex(currentIndex).FindPropertyRelative("blockPrefab").objectReferenceValue = prefab;
        }

        private void WriteCurrentTier(int tier)
        {
            if (currentIndex < 0 || blockListProp == null || currentIndex >= blockListProp.arraySize) return;
            blockListProp.GetArrayElementAtIndex(currentIndex).FindPropertyRelative("tier").intValue = tier;
        }

        private void WriteCurrentColor(Color c)
        {
            if (currentIndex < 0 || blockListProp == null || currentIndex >= blockListProp.arraySize) return;
            blockListProp.GetArrayElementAtIndex(currentIndex).FindPropertyRelative("blockColor").colorValue = c;
        }

        private void DrawShapeGrid()
        {
            float totalSize = GridSize * GridCellSize + (GridSize - 1) * 2 + 8;
            Rect gridRect = GUILayoutUtility.GetRect(totalSize + 8, totalSize + 8);
            float startX = gridRect.x + 4f;
            float startY = gridRect.y + 4f;
            Event e = Event.current;

            // row 0을 아래에 그려서 Index 0(피벗)이 좌측 하단에 오도록 함
            float cellStep = GridCellSize + 2;
            for (int row = 0; row < GridSize; row++)
            {
                for (int col = 0; col < GridSize; col++)
                {
                    int index = row * GridSize + col;
                    float x = startX + col * cellStep;
                    float y = startY + (GridSize - 1 - row) * cellStep;
                    Rect cellRect = new Rect(x, y, GridCellSize, GridCellSize);

                    bool isPivot = (index == PivotIndex);
                    bool filled = editShapeData[index];

                    Color bg = filled ? new Color(editBlockColor.r, editBlockColor.g, editBlockColor.b, 0.95f) : EmptyColor;
                    if (isPivot) bg = Color.Lerp(bg, PivotTint, 0.5f);
                    EditorGUI.DrawRect(cellRect, bg);

                    if (isPivot)
                    {
                        float t = 2f;
                        EditorGUI.DrawRect(new Rect(cellRect.x - t, cellRect.y - t, cellRect.width + t * 2, t), Color.yellow);
                        EditorGUI.DrawRect(new Rect(cellRect.x - t, cellRect.yMax, cellRect.width + t * 2, t), Color.yellow);
                        EditorGUI.DrawRect(new Rect(cellRect.x - t, cellRect.y - t, t, cellRect.height + t * 2), Color.yellow);
                        EditorGUI.DrawRect(new Rect(cellRect.xMax, cellRect.y - t, t, cellRect.height + t * 2), Color.yellow);
                        var style = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                        style.normal.textColor = Color.black;
                        EditorGUI.LabelField(cellRect, "P", style);
                    }

                    if (e.type == EventType.MouseDown && e.button == 0 && cellRect.Contains(e.mousePosition))
                    {
                        editShapeData[index] = !filled;
                        WriteCurrentShapeData();
                        e.Use();
                        Repaint();
                    }
                }
            }

            float totalW = GridSize * cellStep + 2;
            float totalH = totalW;
            for (int i = 0; i <= GridSize; i++)
            {
                float lineX = startX + i * cellStep;
                float lineY = startY + i * cellStep;
                EditorGUI.DrawRect(new Rect(lineX, startY, GridLineWidth, totalH), GridLineColor);
                EditorGUI.DrawRect(new Rect(startX, lineY, totalW, GridLineWidth), GridLineColor);
            }
        }

        private void WriteCurrentShapeData()
        {
            if (currentIndex < 0 || blockListProp == null || currentIndex >= blockListProp.arraySize) return;
            var el = blockListProp.GetArrayElementAtIndex(currentIndex);
            var shapeProp = el.FindPropertyRelative("shapeData");
            if (shapeProp.arraySize != 16) shapeProp.arraySize = 16;
            for (int i = 0; i < 16; i++)
                shapeProp.GetArrayElementAtIndex(i).boolValue = editShapeData[i];
            thumbnailCache.Clear();
        }

        private void DrawBlockListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(220));
            if (reorderableList != null)
                reorderableList.DoList(GUILayoutUtility.GetRect(220, reorderableList.GetHeight()));
            EditorGUILayout.Space(4);
            if (GUILayout.Button("Add Block"))
            {
                if (blockListProp != null)
                {
                    blockListProp.arraySize++;
                    int newIdx = blockListProp.arraySize - 1;
                    var el = blockListProp.GetArrayElementAtIndex(newIdx);
                    el.FindPropertyRelative("blockID").intValue = newIdx;
                    el.FindPropertyRelative("blockName").stringValue = AllBlockData.BlockIdToDisplay(newIdx);
                    el.FindPropertyRelative("tier").intValue = 1;
                    el.FindPropertyRelative("blockColor").colorValue = Color.white;
                    el.FindPropertyRelative("blockPrefab").objectReferenceValue = null;
                    var sp = el.FindPropertyRelative("shapeData");
                    if (sp.arraySize != 16) sp.arraySize = 16;
                    for (int i = 0; i < 16; i++) sp.GetArrayElementAtIndex(i).boolValue = false;
                    SyncBlockIdsToIndices();
                    currentIndex = newIdx;
                    LoadFromElement(currentIndex);
                }
                Repaint();
            }
            EditorGUILayout.EndVertical();
        }

        private void SaveAll()
        {
            if (blockListProp != null)
            {
                if (currentIndex >= 0 && currentIndex < blockListProp.arraySize)
                {
                    var el = blockListProp.GetArrayElementAtIndex(currentIndex);
                    el.FindPropertyRelative("tier").intValue = editTier;
                    el.FindPropertyRelative("blockColor").colorValue = editBlockColor;
                    el.FindPropertyRelative("blockPrefab").objectReferenceValue = editPrefab;
                    el.FindPropertyRelative("blockID").intValue = currentIndex;
                    el.FindPropertyRelative("blockName").stringValue = AllBlockData.BlockIdToDisplay(currentIndex);
                    var shapeProp = el.FindPropertyRelative("shapeData");
                    if (shapeProp.arraySize != 16) shapeProp.arraySize = 16;
                    for (int i = 0; i < 16; i++)
                        shapeProp.GetArrayElementAtIndex(i).boolValue = editShapeData[i];
                }
                else if (currentIndex < 0)
                {
                    blockListProp.arraySize++;
                    int newIdx = blockListProp.arraySize - 1;
                    var el = blockListProp.GetArrayElementAtIndex(newIdx);
                    el.FindPropertyRelative("blockID").intValue = newIdx;
                    el.FindPropertyRelative("blockName").stringValue = AllBlockData.BlockIdToDisplay(newIdx);
                    el.FindPropertyRelative("tier").intValue = editTier;
                    el.FindPropertyRelative("blockColor").colorValue = editBlockColor;
                    el.FindPropertyRelative("blockPrefab").objectReferenceValue = editPrefab;
                    var sp = el.FindPropertyRelative("shapeData");
                    if (sp.arraySize != 16) sp.arraySize = 16;
                    for (int i = 0; i < 16; i++) sp.GetArrayElementAtIndex(i).boolValue = editShapeData[i];
                    SyncBlockIdsToIndices();
                    currentIndex = newIdx;
                }
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(allBlockData);
            AssetDatabase.SaveAssets();

            string path = AssetDatabase.GetAssetPath(allBlockData);
            ShowNotification(new GUIContent("Saved: " + path));
            thumbnailCache.Clear();
            Repaint();
        }

        private void SaveAsNewFile()
        {
            if (string.IsNullOrWhiteSpace(saveAsFileName))
            {
                ShowNotification(new GUIContent("파일 이름을 입력하세요."));
                return;
            }
            EnsureDataFolderExists();
            string name = saveAsFileName.Trim();
            if (!name.EndsWith(".asset")) name += ".asset";
            string path = DataFolderPath + "/" + name;
            path = AssetDatabase.GenerateUniqueAssetPath(path);

            AllBlockData newAsset = CreateInstance<AllBlockData>();
            newAsset.blockList = new List<BlockDataContents>();
            if (allBlockData != null && allBlockData.blockList != null)
            {
                foreach (var item in allBlockData.blockList)
                    newAsset.blockList.Add(CloneBlockContents(item));
            }
            AssetDatabase.CreateAsset(newAsset, path);
            AssetDatabase.SaveAssets();

            SwitchToAllBlockData(newAsset);
            ShowNotification(new GUIContent("Saved As: " + path));
            Repaint();
        }
    }
}
