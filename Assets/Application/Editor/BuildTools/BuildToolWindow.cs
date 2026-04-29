using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ghost.Editor.BuildTools
{
    /// <summary>
    /// Ghost / Build Tool — 안드로이드 빌드 (Dev/Release × APK/AAB) 통합 도구.
    /// EditorPrefs에 설정 저장 → 사용자별 PC에서 유지.
    /// </summary>
    public class BuildToolWindow : EditorWindow
    {
        private const string MenuPath = "Ghost/Build Tool";
        private const string PrefPrefix = "Ghost.BuildTool.";

        // EditorPrefs 키
        private const string KeyEnv = PrefPrefix + "Env";
        private const string KeyFormat = PrefPrefix + "Format";
        private const string KeyProductName = PrefPrefix + "ProductName";
        private const string KeyBasePackage = PrefPrefix + "BasePackage";
        private const string KeyBundleVersion = PrefPrefix + "BundleVersion";
        private const string KeyVersionCode = PrefPrefix + "VersionCode";
        private const string KeyAutoIncrement = PrefPrefix + "AutoIncrement";
        private const string KeyClearConsoleBeforeBuild = PrefPrefix + "ClearConsoleBeforeBuild";

        // 디폴트값
        private const string DefaultProductName = "Ghost Block";
        private const string DefaultBasePackage = "com.doubleugames.ghost";
        private const string DefaultBundleVersion = "0.1.0";
        private const int DefaultVersionCode = 1;

        // 상태
        private BuildEnv env = BuildEnv.Dev;
        private BuildFormat format = BuildFormat.APK;
        private string productName;
        private string basePackage;
        private string bundleVersion;
        private int versionCode;
        private bool autoIncrement;
        private bool clearConsoleBeforeBuild;

        private Vector2 scroll;
        private GUIStyle headerStyle;
        private GUIStyle previewLabelStyle;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            var win = GetWindow<BuildToolWindow>("Ghost Build Tool");
            win.minSize = new Vector2(440, 720);
            win.Show();
        }

        private void OnEnable()
        {
            LoadSettings();
        }

        private void OnDisable()
        {
            SaveSettings();
        }

        // ── 설정 영속화 ──────────────────────────────────────────

        private void LoadSettings()
        {
            env = (BuildEnv)EditorPrefs.GetInt(KeyEnv, (int)BuildEnv.Dev);
            format = (BuildFormat)EditorPrefs.GetInt(KeyFormat, (int)BuildFormat.APK);
            productName = EditorPrefs.GetString(KeyProductName, DefaultProductName);
            basePackage = EditorPrefs.GetString(KeyBasePackage, DefaultBasePackage);
            bundleVersion = EditorPrefs.GetString(KeyBundleVersion, DefaultBundleVersion);
            versionCode = EditorPrefs.GetInt(KeyVersionCode, DefaultVersionCode);
            autoIncrement = EditorPrefs.GetBool(KeyAutoIncrement, true);
            clearConsoleBeforeBuild = EditorPrefs.GetBool(KeyClearConsoleBeforeBuild, true);

            // 첫 사용 시 PlayerSettings에서 한 번 읽어오기
            if (!EditorPrefs.HasKey(KeyVersionCode))
                LoadFromPlayerSettings();
        }

        private void SaveSettings()
        {
            EditorPrefs.SetInt(KeyEnv, (int)env);
            EditorPrefs.SetInt(KeyFormat, (int)format);
            EditorPrefs.SetString(KeyProductName, productName);
            EditorPrefs.SetString(KeyBasePackage, basePackage);
            EditorPrefs.SetString(KeyBundleVersion, bundleVersion);
            EditorPrefs.SetInt(KeyVersionCode, versionCode);
            EditorPrefs.SetBool(KeyAutoIncrement, autoIncrement);
            EditorPrefs.SetBool(KeyClearConsoleBeforeBuild, clearConsoleBeforeBuild);
        }

        private void LoadFromPlayerSettings()
        {
            productName = string.IsNullOrEmpty(PlayerSettings.productName) ? DefaultProductName : PlayerSettings.productName;
            string currentPackage = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
            basePackage = string.IsNullOrEmpty(currentPackage) || currentPackage.Contains("UnityTechnologies")
                ? DefaultBasePackage
                : (currentPackage.EndsWith(".dev") ? currentPackage.Substring(0, currentPackage.Length - 4) : currentPackage);
            bundleVersion = string.IsNullOrEmpty(PlayerSettings.bundleVersion) ? DefaultBundleVersion : PlayerSettings.bundleVersion;
            versionCode = PlayerSettings.Android.bundleVersionCode;
            if (versionCode <= 0) versionCode = DefaultVersionCode;
        }

        // ── UI ────────────────────────────────────────────────────

        private void EnsureStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 13,
                    margin = new RectOffset(0, 0, 8, 4),
                };
            }
            if (previewLabelStyle == null)
            {
                previewLabelStyle = new GUIStyle(EditorStyles.label) { fontSize = 11 };
            }
        }

        private void OnGUI()
        {
            EnsureStyles();
            scroll = EditorGUILayout.BeginScrollView(scroll);

            DrawTitle();
            DrawEnvSection();
            DrawAppInfoSection();
            DrawSdkSection();
            DrawPreviewSection();
            DrawCacheSection();
            DrawBuildButton();

            EditorGUILayout.EndScrollView();
        }

        private void DrawTitle()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Ghost — Android 빌드 도구", new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 });
            EditorGUILayout.HelpBox(
                "1. 빌드 환경(Dev/Release) 선택\n" +
                "2. 빌드 포맷(APK/AAB) 선택\n" +
                "3. Product Name / Package / Version 확인·수정\n" +
                "4. '빌드 시작' 클릭\n\n" +
                "Dev: 디버깅 + IL2CPP (ARMv7 + ARM64, 최대 호환성)\n" +
                "Release: IL2CPP (ARM64만, 스토어 배포용)",
                MessageType.Info);
        }

        private void DrawEnvSection()
        {
            EditorGUILayout.LabelField("빌드 환경", headerStyle);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("환경", GUILayout.Width(80));
                env = (BuildEnv)EditorGUILayout.EnumPopup(env);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("포맷", GUILayout.Width(80));
                format = (BuildFormat)EditorGUILayout.EnumPopup(format);
            }
            EditorGUILayout.HelpBox(
                env == BuildEnv.Dev
                    ? "Dev: 개발/테스트용. 디버깅 + IL2CPP + 패키지명 .dev suffix"
                    : "Release: 스토어 배포용. IL2CPP + ARM64만",
                MessageType.None);
        }

        private void DrawAppInfoSection()
        {
            EditorGUILayout.LabelField("앱 정보", headerStyle);

            DrawTextFieldWithCurrent("Product Name", ref productName, () => productName = PlayerSettings.productName);
            DrawTextFieldWithCurrent("Base Package", ref basePackage, () =>
            {
                string p = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
                basePackage = p.EndsWith(".dev") ? p.Substring(0, p.Length - 4) : p;
            });
            DrawTextFieldWithCurrent("Bundle Version", ref bundleVersion, () => bundleVersion = PlayerSettings.bundleVersion);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Version Code", GUILayout.Width(110));
                versionCode = EditorGUILayout.IntField(versionCode);
                if (GUILayout.Button("현재값", GUILayout.Width(60)))
                    versionCode = PlayerSettings.Android.bundleVersionCode;
                if (GUILayout.Button("+1", GUILayout.Width(40)))
                    versionCode++;
            }

            autoIncrement = EditorGUILayout.ToggleLeft("빌드 성공 시 Version Code +1 자동", autoIncrement);
            clearConsoleBeforeBuild = EditorGUILayout.ToggleLeft("빌드 시작 직전 콘솔 클리어 (Unity 노이즈 제거)", clearConsoleBeforeBuild);
        }

        private void DrawTextFieldWithCurrent(string label, ref string value, System.Action loadCurrent)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(110));
                value = EditorGUILayout.TextField(value);
                if (GUILayout.Button("현재값", GUILayout.Width(60)))
                    loadCurrent?.Invoke();
            }
        }

        private void DrawSdkSection()
        {
            EditorGUILayout.LabelField("SDK 설정 (고정)", headerStyle);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Min SDK", 23);
                EditorGUILayout.IntField("Target SDK", 35);
            }
            EditorGUILayout.HelpBox(
                "Min SDK 23 (Android 6.0) — 시장 95%+ 커버\n" +
                "Target SDK 35 (Android 15) — Google Play 신규 앱 의무 정책\n" +
                "Scripting Backend: IL2CPP (모든 빌드)",
                MessageType.None);
        }

        private void DrawPreviewSection()
        {
            EditorGUILayout.LabelField("빌드 미리보기", headerStyle);

            string finalPackage = BuildExecutor.ResolvePackageName(basePackage, env);
            int activeSceneCount = EditorBuildSettings.scenes.Count(s => s.enabled);

            var req = new BuildExecutor.BuildRequest
            {
                env = env, format = format,
                productName = productName, basePackageName = basePackage,
                bundleVersion = bundleVersion, versionCode = versionCode
            };
            string outputPath = BuildExecutor.ResolveOutputPath(req);
            string relativeOutput = MakeRelativeToProject(outputPath);

            DrawPreviewRow("환경", env.ToString());
            DrawPreviewRow("포맷", format.ToString());
            DrawPreviewRow("Product Name", productName);
            DrawPreviewRow("Package Name", finalPackage);
            DrawPreviewRow("Version", bundleVersion);
            DrawPreviewRow("Version Code", versionCode.ToString());
            DrawPreviewRow("Min / Target SDK", "23 / 35");
            DrawPreviewRow("Scripting Backend", env == BuildEnv.Release ? "IL2CPP (ARM64)" : "IL2CPP (ARMv7 + ARM64)");
            DrawPreviewRow("활성 씬", activeSceneCount + "개");
            DrawPreviewRow("출력 경로", relativeOutput);
        }

        private void DrawPreviewRow(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(120));
                EditorGUILayout.SelectableLabel(value, previewLabelStyle, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }

        /// <summary>
        /// Unity 콘솔 클리어 (UnityEditor.LogEntries.Clear() 리플렉션 호출).
        /// 빌드 직전 호출해서 빌드 중 발생하는 Unity 자체 노이즈를 깨끗한 상태에서 시작.
        /// </summary>
        private static void ClearUnityConsole()
        {
            try
            {
                var assembly = Assembly.GetAssembly(typeof(SceneView));
                var logEntriesType = assembly.GetType("UnityEditor.LogEntries");
                if (logEntriesType == null) return;
                var clearMethod = logEntriesType.GetMethod("Clear",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                clearMethod?.Invoke(null, null);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[BuildTool] 콘솔 클리어 실패: {e.Message}");
            }
        }

        private string MakeRelativeToProject(string fullPath)
        {
            string projectRoot = Directory.GetCurrentDirectory();
            if (fullPath.StartsWith(projectRoot))
                return fullPath.Substring(projectRoot.Length).TrimStart('/', '\\');
            return fullPath;
        }

        private void DrawCacheSection()
        {
            EditorGUILayout.LabelField("유지보수", headerStyle);
            if (GUILayout.Button("캐시 클리어 (Library/Bee + ShaderCache)", GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog("캐시 클리어",
                    "에셋 캐시 + 빌드 캐시(Library/Bee) + 셰이더 캐시를 삭제합니다.\n다음 빌드가 처음부터라 느릴 수 있습니다. 계속할까요?",
                    "삭제", "취소"))
                {
                    BuildExecutor.ClearCaches();
                    EditorUtility.DisplayDialog("완료", "캐시 클리어 완료.", "확인");
                }
            }
        }

        private void DrawBuildButton()
        {
            EditorGUILayout.Space(8);
            string label = $"빌드 시작 ({env} {format})";
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = env == BuildEnv.Release ? new Color(0.4f, 0.7f, 1f) : new Color(0.4f, 0.9f, 0.4f);

            if (GUILayout.Button(label, GUILayout.Height(40)))
            {
                // OnGUI 안에서 BuildPlayer 직접 호출하면 36초 동안 GUI 레이아웃이 막혀서 EndLayoutGroup 에러 발생.
                // delayCall로 다음 프레임에 OnGUI 밖에서 실행하도록 분리.
                EditorApplication.delayCall += StartBuild;
            }
            GUI.backgroundColor = oldColor;
        }

        private void StartBuild()
        {
            // 검증
            if (string.IsNullOrWhiteSpace(productName))
            {
                EditorUtility.DisplayDialog("오류", "Product Name이 비어있습니다.", "확인");
                return;
            }
            if (string.IsNullOrWhiteSpace(basePackage) || !basePackage.Contains("."))
            {
                EditorUtility.DisplayDialog("오류", "Package Name 형식이 올바르지 않습니다 (예: com.company.appname).", "확인");
                return;
            }
            if (string.IsNullOrWhiteSpace(bundleVersion))
            {
                EditorUtility.DisplayDialog("오류", "Bundle Version이 비어있습니다.", "확인");
                return;
            }

            // 확정 다이얼로그
            string finalPackage = BuildExecutor.ResolvePackageName(basePackage, env);
            string msg = $"환경: {env}\n포맷: {format}\nPackage: {finalPackage}\nVersion: {bundleVersion} ({versionCode})\n\n빌드를 시작할까요?";
            if (!EditorUtility.DisplayDialog("빌드 확인", msg, "시작", "취소"))
                return;

            SaveSettings();

            // Unity 빌드 시 발생하는 SetSystemInterested 노이즈 제거를 위해 빌드 직전 콘솔 클리어
            if (clearConsoleBeforeBuild)
                ClearUnityConsole();

            var req = new BuildExecutor.BuildRequest
            {
                env = env,
                format = format,
                productName = productName,
                basePackageName = basePackage,
                bundleVersion = bundleVersion,
                versionCode = versionCode,
                autoIncrementOnSuccess = autoIncrement,
            };

            var result = BuildExecutor.Execute(req);

            if (result.succeeded)
            {
                if (autoIncrement) versionCode = PlayerSettings.Android.bundleVersionCode;
                SaveSettings();

                bool openFolder = EditorUtility.DisplayDialog("빌드 성공",
                    $"빌드 완료\n\n경로: {MakeRelativeToProject(result.outputPath)}\n사이즈: {result.totalSize / 1024 / 1024} MB\n시간: {result.duration.TotalSeconds:F1}초",
                    "폴더 열기", "닫기");
                if (openFolder)
                    EditorUtility.RevealInFinder(result.outputPath);
            }
            else
            {
                EditorUtility.DisplayDialog("빌드 실패", $"오류: {result.error}\n\n자세한 내용은 콘솔을 확인하세요.", "확인");
            }
        }
    }
}
