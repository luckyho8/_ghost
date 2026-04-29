using UnityEditor;
using UnityEngine;

namespace Ghost.Editor
{
    /// <summary>
    /// LevelDifficultyData 시각 에디터.
    /// - Fall Speed / Duration / Tier Weights 미니 차트 3개
    /// - 차트 위 점 드래그로 값 직접 수정
    /// - 하단 인라인 행에서 정밀 입력 / 복제 / 삭제
    /// </summary>
    public class LevelDifficultyEditorWindow : EditorWindow
    {
        private const string MenuPath = "Ghost/Level Difficulty Editor";
        private const string DataFolderPath = "Assets/Application/Data";
        private const string DefaultDataFileName = "LevelDifficultyData.asset";

        // 차트 가시성 토글 (EditorPrefs 저장)
        private const string PrefsKeyShowFall = "Ghost.LevelDiff.ShowFall";
        private const string PrefsKeyShowDur = "Ghost.LevelDiff.ShowDur";
        private const string PrefsKeyShowTier = "Ghost.LevelDiff.ShowTier";
        private bool showFall = true;
        private bool showDur = true;
        private bool showTier = true;

        // ── 차트 / 드래그 ────────────────────────────────────────
        private enum DragSeries { None, FallSpeed, Duration, T1, T2, T3 }
        private DragSeries dragSeries = DragSeries.None;
        private int dragLevelIndex = -1;

        // 드래그/표시 가시 범위 (UI scale only)
        private const float MinFallSpeed = 0.05f;
        private const float MaxFallSpeed = 1.6f;
        private const float MinDuration = 1f;
        private const float MaxDuration = 75f;

        private const float ChartHeight = 110f;
        private const float ChartLeftPadding = 36f;
        private const float ChartRightPadding = 12f;
        private const float ChartTopPadding = 20f;
        private const float ChartBottomPadding = 18f;
        private const float PointRadius = 4f;
        private const float HitRadius = 11f;

        private static readonly Color BgColor = new Color(0.16f, 0.16f, 0.18f, 1f);
        private static readonly Color GridColor = new Color(1f, 1f, 1f, 0.07f);
        private static readonly Color AxisColor = new Color(1f, 1f, 1f, 0.22f);
        private static readonly Color FallSpeedColor = new Color(1f, 0.55f, 0.20f, 1f);
        private static readonly Color DurationColor = new Color(0.40f, 0.85f, 1.00f, 1f);
        private static readonly Color Tier1Color = new Color(0.40f, 0.95f, 0.50f, 1f);
        private static readonly Color Tier2Color = new Color(1.00f, 0.75f, 0.20f, 1f);
        private static readonly Color Tier3Color = new Color(1.00f, 0.40f, 0.55f, 1f);
        private static readonly Color HoverPointColor = Color.white;

        // ── 데이터 ───────────────────────────────────────────────
        private LevelDifficultyData data;
        private SerializedObject so;
        private SerializedProperty entriesProp;
        private Vector2 listScroll;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var w = GetWindow<LevelDifficultyEditorWindow>("Level Difficulty");
            w.minSize = new Vector2(640, 620);
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
            showFall = EditorPrefs.GetBool(PrefsKeyShowFall, true);
            showDur = EditorPrefs.GetBool(PrefsKeyShowDur, true);
            showTier = EditorPrefs.GetBool(PrefsKeyShowTier, true);
            LoadAsset();
        }

        private void OnDisable()
        {
            EditorPrefs.SetBool(PrefsKeyShowFall, showFall);
            EditorPrefs.SetBool(PrefsKeyShowDur, showDur);
            EditorPrefs.SetBool(PrefsKeyShowTier, showTier);
        }

        private void OnFocus() { LoadAsset(); }

        private void LoadAsset()
        {
            string p = $"{DataFolderPath}/{DefaultDataFileName}";
            var found = AssetDatabase.LoadAssetAtPath<LevelDifficultyData>(p);
            if (found == null)
            {
                var guids = AssetDatabase.FindAssets("t:LevelDifficultyData");
                if (guids.Length > 0)
                    found = AssetDatabase.LoadAssetAtPath<LevelDifficultyData>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            if (found != data)
                Bind(found);
            else if (data != null && so == null)
                Bind(data);
        }

        private void Bind(LevelDifficultyData d)
        {
            data = d;
            so = d != null ? new SerializedObject(d) : null;
            entriesProp = so?.FindProperty("entries");
        }

        private void OnGUI()
        {
            DrawTopBar();

            if (data == null)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox("LevelDifficultyData 자산이 없습니다. 새로 만들거나 위 슬롯에 드래그하세요.", MessageType.Info);
                if (GUILayout.Button("새 LevelDifficultyData 만들기 (Assets/Application/Data/)", GUILayout.Height(28)))
                    CreateNewAsset();
                return;
            }

            so.Update();

            int count = entriesProp.arraySize;
            EditorGUILayout.LabelField($"레벨 수: {count}", EditorStyles.miniLabel);

            int visible = (showFall ? 1 : 0) + (showDur ? 1 : 0) + (showTier ? 1 : 0);
            float chartH = visible > 0 ? (ChartHeight * 3f) / visible : 0f;

            if (showFall)
                DrawChartFor(DragSeries.FallSpeed, "Fall Speed (낮을수록 빠름)", MinFallSpeed, MaxFallSpeed, FallSpeedColor, "fallSpeed", "F2", chartH);
            if (showDur)
                DrawChartFor(DragSeries.Duration, "Duration (다음 레벨까지 / 초)", MinDuration, MaxDuration, DurationColor, "duration", "F0", chartH);
            if (showTier)
                DrawTierChart(chartH);

            EditorGUILayout.Space(6);
            DrawEntriesList();

            so.ApplyModifiedProperties();

            // 자동 저장: 드래그 중이 아닐 때만 (드래그 매 프레임 디스크 쓰는 비용 회피).
            // SaveAssetIfDirty는 dirty 아닐 때 no-op이라 호출 자체는 가벼움.
            if (data != null && dragSeries == DragSeries.None)
                AssetDatabase.SaveAssetIfDirty(data);

            if (Event.current.type == EventType.MouseMove) Repaint();
        }

        // ─────────────────────────────────────────────────────────
        // 상단 툴바
        // ─────────────────────────────────────────────────────────
        private void DrawTopBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUI.BeginChangeCheck();
            var newAsset = (LevelDifficultyData)EditorGUILayout.ObjectField(
                data, typeof(LevelDifficultyData), false, GUILayout.MinWidth(220));
            if (EditorGUI.EndChangeCheck()) Bind(newAsset);

            GUILayout.Space(8);
            // 차트 가시성 토글
            var prevColor = GUI.backgroundColor;
            GUI.backgroundColor = showFall ? FallSpeedColor : Color.white;
            showFall = GUILayout.Toggle(showFall, "Fall", EditorStyles.toolbarButton, GUILayout.Width(46));
            GUI.backgroundColor = showDur ? DurationColor : Color.white;
            showDur = GUILayout.Toggle(showDur, "Dur", EditorStyles.toolbarButton, GUILayout.Width(46));
            GUI.backgroundColor = showTier ? new Color(0.7f, 1f, 0.7f) : Color.white;
            showTier = GUILayout.Toggle(showTier, "Tier", EditorStyles.toolbarButton, GUILayout.Width(46));
            GUI.backgroundColor = prevColor;

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(data == null))
            {
                if (GUILayout.Button("+ Add Level", EditorStyles.toolbarButton, GUILayout.Width(90)))
                    AddLevelCopyLast();
                if (GUILayout.Button("- Remove Last", EditorStyles.toolbarButton, GUILayout.Width(100)))
                    RemoveLastLevel();
                if (GUILayout.Button("Reset Defaults", EditorStyles.toolbarButton, GUILayout.Width(110)))
                {
                    if (EditorUtility.DisplayDialog("기본 레벨 테이블 복원",
                        "현재 데이터를 기본값(10레벨)으로 덮어씁니다. 되돌리려면 Ctrl+Z.", "복원", "취소"))
                    {
                        Undo.RecordObject(data, "Reset Level Difficulty");
                        data.PopulateDefaults();
                        EditorUtility.SetDirty(data);
                        Bind(data);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        // ─────────────────────────────────────────────────────────
        // 차트 (단일 시리즈)
        // ─────────────────────────────────────────────────────────
        private void DrawChartFor(DragSeries series, string title, float yMin, float yMax, Color color, string propName, string fmt, float height)
        {
            var fullRect = GUILayoutUtility.GetRect(0, height, GUILayout.ExpandWidth(true));
            fullRect.x += 6; fullRect.width -= 12;
            EditorGUI.DrawRect(fullRect, BgColor);

            // 제목 / 색상 라벨
            GUI.contentColor = color;
            GUI.Label(new Rect(fullRect.x + 6, fullRect.y + 2, fullRect.width - 12, 16),
                "● " + title, EditorStyles.miniBoldLabel);
            GUI.contentColor = Color.white;

            var chart = GetInnerRect(fullRect);
            DrawAxes(chart, yMin, yMax, 4, fmt, color);

            int count = entriesProp.arraySize;
            if (count == 0) return;

            // 점 위치 계산
            var points = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float v = entriesProp.GetArrayElementAtIndex(i).FindPropertyRelative(propName).floatValue;
                points[i] = LevelToPoint(chart, i, count, v, yMin, yMax);
            }

            // 라인
            DrawLine(points, color, 2.0f);

            // 점들
            for (int i = 0; i < count; i++)
                DrawPoint(points[i], color);

            // x축 레벨 라벨
            DrawLevelTicks(chart, count);

            // 입력 처리
            HandleDrag(chart, points, count, series, propName, yMin, yMax);

            // 호버 툴팁
            DrawHoverTooltip(chart, points, count, propName, fmt, color);
        }

        // ─────────────────────────────────────────────────────────
        // Tier Weights 차트 (3시리즈, 정규화 % 표시)
        // ─────────────────────────────────────────────────────────
        private void DrawTierChart(float height)
        {
            var fullRect = GUILayoutUtility.GetRect(0, height + 6, GUILayout.ExpandWidth(true));
            fullRect.x += 6; fullRect.width -= 12;
            EditorGUI.DrawRect(fullRect, BgColor);

            float lx = fullRect.x + 6;
            DrawColorChip(ref lx, fullRect.y + 2, "T1", Tier1Color);
            DrawColorChip(ref lx, fullRect.y + 2, "T2", Tier2Color);
            DrawColorChip(ref lx, fullRect.y + 2, "T3", Tier3Color);
            GUI.Label(new Rect(lx + 6, fullRect.y + 2, 240, 16), "(정규화된 %)", EditorStyles.miniLabel);

            var chart = GetInnerRect(fullRect);
            DrawAxes(chart, 0f, 1f, 4, "P0", new Color(0.7f, 0.7f, 0.7f, 1f));

            int count = entriesProp.arraySize;
            if (count == 0) return;

            var t1Pts = new Vector2[count];
            var t2Pts = new Vector2[count];
            var t3Pts = new Vector2[count];

            for (int i = 0; i < count; i++)
            {
                var w = GetTierWeightsNormalized(i);
                t1Pts[i] = LevelToPoint(chart, i, count, w[0], 0f, 1f);
                t2Pts[i] = LevelToPoint(chart, i, count, w[1], 0f, 1f);
                t3Pts[i] = LevelToPoint(chart, i, count, w[2], 0f, 1f);
            }

            DrawLine(t1Pts, Tier1Color, 2f);
            DrawLine(t2Pts, Tier2Color, 2f);
            DrawLine(t3Pts, Tier3Color, 2f);
            for (int i = 0; i < count; i++)
            {
                DrawPoint(t1Pts[i], Tier1Color);
                DrawPoint(t2Pts[i], Tier2Color);
                DrawPoint(t3Pts[i], Tier3Color);
            }

            DrawLevelTicks(chart, count);

            HandleTierDrag(chart, t1Pts, t2Pts, t3Pts, count);
            DrawTierHoverTooltip(chart, t1Pts, t2Pts, t3Pts, count);
        }

        private void DrawColorChip(ref float x, float y, string label, Color c)
        {
            var chip = new Rect(x, y + 4, 10, 10);
            EditorGUI.DrawRect(chip, c);
            GUI.Label(new Rect(x + 13, y + 1, 22, 16), label, EditorStyles.miniBoldLabel);
            x += 38;
        }

        private float[] GetTierWeightsNormalized(int levelIdx)
        {
            var w = new float[3];
            var arr = entriesProp.GetArrayElementAtIndex(levelIdx).FindPropertyRelative("tierWeights");
            for (int t = 0; t < 3; t++)
            {
                if (arr.arraySize > t) w[t] = Mathf.Max(0f, arr.GetArrayElementAtIndex(t).floatValue);
            }
            float sum = w[0] + w[1] + w[2];
            if (sum > 0f) { w[0] /= sum; w[1] /= sum; w[2] /= sum; }
            return w;
        }

        // ─────────────────────────────────────────────────────────
        // 드래그
        // ─────────────────────────────────────────────────────────
        private void HandleDrag(Rect chart, Vector2[] points, int count, DragSeries series, string propName, float yMin, float yMax)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && chart.Contains(e.mousePosition))
            {
                int hit = NearestPointIndex(points, e.mousePosition);
                if (hit >= 0)
                {
                    dragSeries = series;
                    dragLevelIndex = hit;
                    e.Use();
                }
            }
            else if ((e.type == EventType.MouseDrag) && dragSeries == series && dragLevelIndex >= 0 && dragLevelIndex < count)
            {
                float v = MouseYToValue(e.mousePosition.y, chart, yMin, yMax);
                if (e.control) v = SnapTo(v, (yMax - yMin) / 20f);
                entriesProp.GetArrayElementAtIndex(dragLevelIndex)
                    .FindPropertyRelative(propName).floatValue = Mathf.Clamp(v, yMin, yMax);
                Repaint();
                e.Use();
            }
            else if (e.type == EventType.MouseUp && dragSeries == series)
            {
                dragSeries = DragSeries.None;
                dragLevelIndex = -1;
                e.Use();
            }

            if (chart.Contains(e.mousePosition))
                EditorGUIUtility.AddCursorRect(chart, MouseCursor.MoveArrow);
        }

        private void HandleTierDrag(Rect chart, Vector2[] t1, Vector2[] t2, Vector2[] t3, int count)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && chart.Contains(e.mousePosition))
            {
                int h1 = NearestPointIndex(t1, e.mousePosition);
                int h2 = NearestPointIndex(t2, e.mousePosition);
                int h3 = NearestPointIndex(t3, e.mousePosition);

                // 가장 가까운 시리즈 선택
                float d1 = h1 >= 0 ? Vector2.Distance(t1[h1], e.mousePosition) : float.MaxValue;
                float d2 = h2 >= 0 ? Vector2.Distance(t2[h2], e.mousePosition) : float.MaxValue;
                float d3 = h3 >= 0 ? Vector2.Distance(t3[h3], e.mousePosition) : float.MaxValue;
                float best = Mathf.Min(d1, Mathf.Min(d2, d3));

                if (best <= HitRadius)
                {
                    if (best == d1) { dragSeries = DragSeries.T1; dragLevelIndex = h1; }
                    else if (best == d2) { dragSeries = DragSeries.T2; dragLevelIndex = h2; }
                    else { dragSeries = DragSeries.T3; dragLevelIndex = h3; }
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseDrag &&
                (dragSeries == DragSeries.T1 || dragSeries == DragSeries.T2 || dragSeries == DragSeries.T3) &&
                dragLevelIndex >= 0 && dragLevelIndex < count)
            {
                int tIdx = dragSeries == DragSeries.T1 ? 0 : dragSeries == DragSeries.T2 ? 1 : 2;
                float pct = MouseYToValue(e.mousePosition.y, chart, 0f, 1f);
                if (e.control) pct = SnapTo(pct, 0.05f);
                pct = Mathf.Clamp01(pct);

                // 정규화된 % 그래프에서 드래그한 점 = 그 시리즈의 비율을 pct로 만들고 나머지 둘은 비례로 축소/확대
                SetTierFromNormalizedPercent(dragLevelIndex, tIdx, pct);
                Repaint();
                e.Use();
            }
            else if (e.type == EventType.MouseUp &&
                (dragSeries == DragSeries.T1 || dragSeries == DragSeries.T2 || dragSeries == DragSeries.T3))
            {
                dragSeries = DragSeries.None;
                dragLevelIndex = -1;
                e.Use();
            }

            if (chart.Contains(e.mousePosition))
                EditorGUIUtility.AddCursorRect(chart, MouseCursor.MoveArrow);
        }

        /// <summary>
        /// 드래그된 시리즈가 정규화 후 pct 가 되도록 weights 재계산.
        /// 나머지 두 weight는 기존 비율 유지하며 (1-pct)를 나눠가짐.
        /// </summary>
        private void SetTierFromNormalizedPercent(int levelIdx, int tIdx, float pct)
        {
            var arr = entriesProp.GetArrayElementAtIndex(levelIdx).FindPropertyRelative("tierWeights");
            while (arr.arraySize < 3) arr.InsertArrayElementAtIndex(arr.arraySize);

            float[] cur = new float[3];
            for (int i = 0; i < 3; i++) cur[i] = Mathf.Max(0f, arr.GetArrayElementAtIndex(i).floatValue);

            float others = 0f;
            for (int i = 0; i < 3; i++) if (i != tIdx) others += cur[i];

            float[] next = new float[3];
            next[tIdx] = pct;
            float remain = 1f - pct;
            if (others > 0.0001f)
            {
                for (int i = 0; i < 3; i++)
                    if (i != tIdx) next[i] = remain * (cur[i] / others);
            }
            else
            {
                // 다른 둘 모두 0이었으면 절반씩 나눔
                for (int i = 0; i < 3; i++)
                    if (i != tIdx) next[i] = remain * 0.5f;
            }

            for (int i = 0; i < 3; i++)
                arr.GetArrayElementAtIndex(i).floatValue = next[i];
        }

        // ─────────────────────────────────────────────────────────
        // 인라인 행 에디터
        // ─────────────────────────────────────────────────────────
        private void DrawEntriesList()
        {
            EditorGUILayout.LabelField("Levels (정밀 입력)", EditorStyles.boldLabel);
            listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.MinHeight(180));

            int count = entriesProp.arraySize;
            for (int i = 0; i < count; i++)
            {
                var entry = entriesProp.GetArrayElementAtIndex(i);
                var dur = entry.FindPropertyRelative("duration");
                var fall = entry.FindPropertyRelative("fallSpeed");
                var weights = entry.FindPropertyRelative("tierWeights");
                while (weights.arraySize < 3) weights.InsertArrayElementAtIndex(weights.arraySize);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"L{i + 1}", GUILayout.Width(28));

                GUILayout.Label("Dur", EditorStyles.miniLabel, GUILayout.Width(24));
                dur.floatValue = EditorGUILayout.FloatField(dur.floatValue, GUILayout.Width(46));

                GUILayout.Label("Fall", EditorStyles.miniLabel, GUILayout.Width(24));
                fall.floatValue = EditorGUILayout.FloatField(fall.floatValue, GUILayout.Width(46));

                GUI.color = Tier1Color;
                GUILayout.Label("T1", EditorStyles.miniBoldLabel, GUILayout.Width(20));
                GUI.color = Color.white;
                weights.GetArrayElementAtIndex(0).floatValue = EditorGUILayout.FloatField(weights.GetArrayElementAtIndex(0).floatValue, GUILayout.Width(42));

                GUI.color = Tier2Color;
                GUILayout.Label("T2", EditorStyles.miniBoldLabel, GUILayout.Width(20));
                GUI.color = Color.white;
                weights.GetArrayElementAtIndex(1).floatValue = EditorGUILayout.FloatField(weights.GetArrayElementAtIndex(1).floatValue, GUILayout.Width(42));

                GUI.color = Tier3Color;
                GUILayout.Label("T3", EditorStyles.miniBoldLabel, GUILayout.Width(20));
                GUI.color = Color.white;
                weights.GetArrayElementAtIndex(2).floatValue = EditorGUILayout.FloatField(weights.GetArrayElementAtIndex(2).floatValue, GUILayout.Width(42));

                var w = GetTierWeightsNormalized(i);
                GUILayout.Label($"= {w[0] * 100f:F0}% / {w[1] * 100f:F0}% / {w[2] * 100f:F0}%",
                    EditorStyles.miniLabel, GUILayout.Width(120));

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("↑ Copy", EditorStyles.miniButton, GUILayout.Width(54)))
                    DuplicateEntryBefore(i);
                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22)))
                {
                    entriesProp.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        // ─────────────────────────────────────────────────────────
        // 데이터 조작
        // ─────────────────────────────────────────────────────────
        private void AddLevelCopyLast()
        {
            int n = entriesProp.arraySize;
            entriesProp.InsertArrayElementAtIndex(n);
            // InsertArrayElementAtIndex는 직전 요소를 복제 — 마지막에 추가했으므로 마지막 값 그대로 복사됨
            so.ApplyModifiedProperties();
        }

        private void RemoveLastLevel()
        {
            int n = entriesProp.arraySize;
            if (n > 0) entriesProp.DeleteArrayElementAtIndex(n - 1);
            so.ApplyModifiedProperties();
        }

        private void DuplicateEntryBefore(int index)
        {
            entriesProp.InsertArrayElementAtIndex(index);
            so.ApplyModifiedProperties();
        }

        private void CreateNewAsset()
        {
            if (!AssetDatabase.IsValidFolder(DataFolderPath))
            {
                System.IO.Directory.CreateDirectory(DataFolderPath);
                AssetDatabase.Refresh();
            }
            string p = AssetDatabase.GenerateUniqueAssetPath($"{DataFolderPath}/{DefaultDataFileName}");
            var asset = ScriptableObject.CreateInstance<LevelDifficultyData>();
            asset.PopulateDefaults();
            AssetDatabase.CreateAsset(asset, p);
            AssetDatabase.SaveAssets();
            Bind(asset);
        }

        // ─────────────────────────────────────────────────────────
        // 차트 그리기 헬퍼
        // ─────────────────────────────────────────────────────────
        private static Rect GetInnerRect(Rect outer)
        {
            return new Rect(
                outer.x + ChartLeftPadding,
                outer.y + ChartTopPadding,
                outer.width - ChartLeftPadding - ChartRightPadding,
                outer.height - ChartTopPadding - ChartBottomPadding);
        }

        private static Vector2 LevelToPoint(Rect chart, int i, int count, float v, float yMin, float yMax)
        {
            float xT = count <= 1 ? 0.5f : (float)i / (count - 1);
            float yT = Mathf.InverseLerp(yMin, yMax, v);
            return new Vector2(
                Mathf.Lerp(chart.xMin, chart.xMax, xT),
                Mathf.Lerp(chart.yMax, chart.yMin, yT));
        }

        private static float MouseYToValue(float my, Rect chart, float yMin, float yMax)
        {
            float t = 1f - Mathf.InverseLerp(chart.yMin, chart.yMax, my);
            return Mathf.Clamp(Mathf.Lerp(yMin, yMax, t), yMin, yMax);
        }

        private static float SnapTo(float v, float step)
        {
            if (step <= 0f) return v;
            return Mathf.Round(v / step) * step;
        }

        private void DrawAxes(Rect chart, float yMin, float yMax, int gridSteps, string fmt, Color labelColor)
        {
            // 가로 그리드 + Y축 라벨
            for (int g = 0; g <= gridSteps; g++)
            {
                float t = (float)g / gridSteps;
                float y = Mathf.Lerp(chart.yMax, chart.yMin, t);
                EditorGUI.DrawRect(new Rect(chart.xMin, y, chart.width, 1f), GridColor);
                float v = Mathf.Lerp(yMin, yMax, t);
                var prevColor = GUI.contentColor;
                GUI.contentColor = labelColor;
                GUI.Label(new Rect(chart.xMin - ChartLeftPadding + 4, y - 7, ChartLeftPadding - 6, 14),
                    v.ToString(fmt), EditorStyles.miniLabel);
                GUI.contentColor = prevColor;
            }
            // 좌측/하단 축
            EditorGUI.DrawRect(new Rect(chart.xMin, chart.yMin, 1f, chart.height), AxisColor);
            EditorGUI.DrawRect(new Rect(chart.xMin, chart.yMax, chart.width, 1f), AxisColor);
        }

        private void DrawLevelTicks(Rect chart, int count)
        {
            // 너무 많으면 일부만 라벨링
            int labelEvery = count <= 12 ? 1 : Mathf.CeilToInt(count / 12f);
            for (int i = 0; i < count; i++)
            {
                float xT = count <= 1 ? 0.5f : (float)i / (count - 1);
                float x = Mathf.Lerp(chart.xMin, chart.xMax, xT);
                EditorGUI.DrawRect(new Rect(x, chart.yMax, 1f, 3f), AxisColor);
                if (i % labelEvery == 0 || i == count - 1)
                {
                    var prev = GUI.contentColor;
                    GUI.contentColor = new Color(0.7f, 0.7f, 0.7f, 1f);
                    GUI.Label(new Rect(x - 12, chart.yMax + 2, 24, 14), (i + 1).ToString(), EditorStyles.miniLabel);
                    GUI.contentColor = prev;
                }
            }
        }

        private static void DrawLine(Vector2[] pts, Color c, float thickness)
        {
            if (pts == null || pts.Length < 2) return;
            var v3 = new Vector3[pts.Length];
            for (int i = 0; i < pts.Length; i++)
                v3[i] = new Vector3(pts[i].x, pts[i].y, 0f);
            Handles.BeginGUI();
            Handles.color = c;
            Handles.DrawAAPolyLine(thickness, v3);
            Handles.EndGUI();
        }

        private static void DrawPoint(Vector2 p, Color c)
        {
            EditorGUI.DrawRect(new Rect(p.x - PointRadius, p.y - PointRadius, PointRadius * 2, PointRadius * 2), c);
            // 가운데 작은 흰점으로 가시성 ↑
            EditorGUI.DrawRect(new Rect(p.x - 1, p.y - 1, 2, 2), HoverPointColor);
        }

        private int NearestPointIndex(Vector2[] pts, Vector2 m)
        {
            int best = -1;
            float bestD = HitRadius;
            for (int i = 0; i < pts.Length; i++)
            {
                float d = Vector2.Distance(pts[i], m);
                if (d <= bestD) { bestD = d; best = i; }
            }
            return best;
        }

        // 호버 툴팁
        private void DrawHoverTooltip(Rect chart, Vector2[] pts, int count, string propName, string fmt, Color color)
        {
            var m = Event.current.mousePosition;
            if (!chart.Contains(m)) return;
            int hit = NearestPointIndex(pts, m);
            if (hit < 0) return;
            float v = entriesProp.GetArrayElementAtIndex(hit).FindPropertyRelative(propName).floatValue;
            DrawTooltip(pts[hit], $"L{hit + 1}\n{v.ToString(fmt)}", color);
        }

        private void DrawTierHoverTooltip(Rect chart, Vector2[] t1, Vector2[] t2, Vector2[] t3, int count)
        {
            var m = Event.current.mousePosition;
            if (!chart.Contains(m)) return;
            int h1 = NearestPointIndex(t1, m);
            int h2 = NearestPointIndex(t2, m);
            int h3 = NearestPointIndex(t3, m);
            float d1 = h1 >= 0 ? Vector2.Distance(t1[h1], m) : float.MaxValue;
            float d2 = h2 >= 0 ? Vector2.Distance(t2[h2], m) : float.MaxValue;
            float d3 = h3 >= 0 ? Vector2.Distance(t3[h3], m) : float.MaxValue;
            float best = Mathf.Min(d1, Mathf.Min(d2, d3));
            if (best > HitRadius) return;

            int idx;
            Color c;
            string label;
            Vector2 anchor;
            if (best == d1) { idx = h1; c = Tier1Color; label = "T1"; anchor = t1[h1]; }
            else if (best == d2) { idx = h2; c = Tier2Color; label = "T2"; anchor = t2[h2]; }
            else { idx = h3; c = Tier3Color; label = "T3"; anchor = t3[h3]; }
            var w = GetTierWeightsNormalized(idx);
            float pct = label == "T1" ? w[0] : label == "T2" ? w[1] : w[2];
            DrawTooltip(anchor, $"L{idx + 1} {label}\n{pct * 100f:F1}%", c);
        }

        private void DrawTooltip(Vector2 anchor, string text, Color accent)
        {
            var size = EditorStyles.miniLabel.CalcSize(new GUIContent(text));
            var box = new Rect(anchor.x + 8, anchor.y - size.y - 8, size.x + 10, size.y + 6);
            EditorGUI.DrawRect(box, new Color(0.05f, 0.05f, 0.05f, 0.92f));
            EditorGUI.DrawRect(new Rect(box.x, box.y, 2f, box.height), accent);
            var prev = GUI.contentColor;
            GUI.contentColor = Color.white;
            GUI.Label(new Rect(box.x + 6, box.y + 2, box.width - 6, box.height - 4), text, EditorStyles.miniLabel);
            GUI.contentColor = prev;
        }
    }
}
