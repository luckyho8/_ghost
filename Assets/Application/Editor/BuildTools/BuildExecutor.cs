using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Ghost.Editor.BuildTools
{
    public enum BuildEnv { Dev, Release }
    public enum BuildFormat { APK, AAB }

    /// <summary>
    /// PlayerSettings 적용 + BuildPipeline.BuildPlayer 호출 + 빌드 후처리.
    /// </summary>
    public static class BuildExecutor
    {
        // SDK 정책: Min은 시장 호환성 위해 23 (Android 6.0 Marshmallow, 95%+ 커버),
        // Target은 Google Play 요구사항으로 35 (Android 15)
        private const int MinSdkVersion = 23;
        private const int TargetSdkVersion = 35;

        public class BuildRequest
        {
            public BuildEnv env;
            public BuildFormat format;
            public string productName;
            public string basePackageName;     // 예: com.doubleugames.ghost  (Dev면 .dev 자동 부여)
            public string bundleVersion;       // 예: 0.1.0
            public int versionCode;            // 예: 1
            public bool autoIncrementOnSuccess;
        }

        public class BuildResultInfo
        {
            public bool succeeded;
            public string outputPath;
            public string error;
            public int totalSize;
            public TimeSpan duration;
        }

        public static BuildResultInfo Execute(BuildRequest req)
        {
            var result = new BuildResultInfo();

            try
            {
                // 1) PlayerSettings 적용
                ApplyPlayerSettings(req);

                // 2) 출력 경로 결정 + 폴더 생성
                string outputPath = ResolveOutputPath(req);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                result.outputPath = outputPath;

                // 3) 활성 씬 수집
                string[] scenes = EditorBuildSettings.scenes
                    .Where(s => s.enabled)
                    .Select(s => s.path)
                    .ToArray();

                if (scenes.Length == 0)
                {
                    result.error = "활성화된 씬이 없습니다. Build Settings → Scenes In Build를 확인하세요.";
                    return result;
                }

                // 4) BuildPlayerOptions 구성
                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = req.env == BuildEnv.Dev
                        ? (BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.ConnectWithProfiler)
                        : BuildOptions.None,
                };

                // AAB 여부
                EditorUserBuildSettings.buildAppBundle = req.format == BuildFormat.AAB;

                // 5) 빌드 실행
                var report = BuildPipeline.BuildPlayer(options);
                var summary = report.summary;
                result.succeeded = summary.result == BuildResult.Succeeded;
                result.totalSize = (int)summary.totalSize;
                result.duration = summary.totalTime;

                if (!result.succeeded)
                {
                    result.error = $"BuildResult: {summary.result}, Errors: {summary.totalErrors}, Warnings: {summary.totalWarnings}";
                    return result;
                }

                // 6) 빌드 성공 시 Version Code +1
                if (req.autoIncrementOnSuccess)
                {
                    PlayerSettings.Android.bundleVersionCode = req.versionCode + 1;
                    AssetDatabase.SaveAssets();
                }

                Debug.Log($"[BuildTool] 빌드 성공: {outputPath} ({summary.totalSize / 1024 / 1024} MB, {summary.totalTime.TotalSeconds:F1}s)");
            }
            catch (Exception e)
            {
                result.succeeded = false;
                result.error = e.Message;
                Debug.LogError($"[BuildTool] 빌드 실패: {e}");
            }

            return result;
        }

        private static void ApplyPlayerSettings(BuildRequest req)
        {
            // Product Name
            PlayerSettings.productName = req.productName;

            // Package Name (Dev면 .dev 자동 suffix)
            string finalPackage = ResolvePackageName(req.basePackageName, req.env);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, finalPackage);

            // Version
            PlayerSettings.bundleVersion = req.bundleVersion;
            PlayerSettings.Android.bundleVersionCode = req.versionCode;

            // SDK 정책: Min 23 (호환성), Target 35 (Play 정책)
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)MinSdkVersion;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)TargetSdkVersion;

            // Scripting Backend: Dev/Release 모두 IL2CPP
            //  - Mono는 신형 디바이스 호환성 이슈 + Google Play 거부 (2019년부터)
            //  - 하이퍼캐주얼 표준: Dev부터 IL2CPP로 통일
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);

            // Target Architectures
            //  - Dev: ARMv7 + ARM64 (최대 호환성, 모든 폰 커버)
            //  - Release: ARM64만 (Play Store는 ARM64 필수, 사이즈 절감)
            if (req.env == BuildEnv.Release)
            {
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            }
            else
            {
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            }

            // 변경사항 즉시 저장
            AssetDatabase.SaveAssets();
        }

        public static string ResolvePackageName(string basePackage, BuildEnv env)
        {
            if (string.IsNullOrEmpty(basePackage)) return basePackage;
            // 이미 .dev로 끝나면 중복 부여 방지
            if (env == BuildEnv.Dev)
                return basePackage.EndsWith(".dev") ? basePackage : basePackage + ".dev";
            return basePackage.EndsWith(".dev") ? basePackage.Substring(0, basePackage.Length - 4) : basePackage;
        }

        public static string ResolveOutputPath(BuildRequest req)
        {
            string ext = req.format == BuildFormat.APK ? "apk" : "aab";
            string envStr = req.env.ToString();          // Dev / Release
            string formatStr = req.format.ToString();    // APK / AAB
            string product = SanitizeFileName(req.productName);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"{product}_v{req.bundleVersion}_{req.versionCode}_{timestamp}.{ext}";

            string projectRoot = Directory.GetCurrentDirectory();
            return Path.Combine(projectRoot, "Builds", "Android", $"{envStr}_{formatStr}", fileName);
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Game";
            char[] invalid = Path.GetInvalidFileNameChars();
            string safe = string.Concat(name.Where(c => !invalid.Contains(c)));
            return safe.Replace(' ', '_');
        }

        // ── Cache Clear ──────────────────────────────────────────

        public static void ClearCaches()
        {
            UnityEngine.Caching.ClearCache();

            // Library/Bee (빌드 캐시)
            string beePath = Path.Combine(Directory.GetCurrentDirectory(), "Library", "Bee");
            if (Directory.Exists(beePath))
            {
                try { Directory.Delete(beePath, true); }
                catch (Exception e) { Debug.LogWarning($"[BuildTool] Library/Bee 삭제 실패: {e.Message}"); }
            }

            // Library/ShaderCache
            string shaderCache = Path.Combine(Directory.GetCurrentDirectory(), "Library", "ShaderCache");
            if (Directory.Exists(shaderCache))
            {
                try { Directory.Delete(shaderCache, true); }
                catch (Exception e) { Debug.LogWarning($"[BuildTool] ShaderCache 삭제 실패: {e.Message}"); }
            }

            AssetDatabase.Refresh();
            Debug.Log("[BuildTool] 캐시 클리어 완료 (다음 빌드는 처음이라 느릴 수 있음)");
        }
    }
}
