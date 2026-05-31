using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace KingOfTheHill.Editor
{
    public static class AndroidBuildTool
    {
        private const string OutputDirectory = "Builds/Android";
        private const string ApkPath = OutputDirectory + "/Dominio.apk";

        [MenuItem("KingOfTheHill/Build/Android APK", priority = 100)]
        public static void BuildAndroidApk()
        {
            Directory.CreateDirectory(OutputDirectory);

            PlayerSettings.companyName = "Allanix";
            PlayerSettings.productName = "Dominio";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.allanix.dominio");
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.forceInternetPermission = true;
            EditorUserBuildSettings.buildAppBundle = false;

            string[] scenes =
            {
                "Assets/Scenes/Bootstrap_Scene.unity",
                "Assets/Scenes/MainMenu_Scene.unity",
                "Assets/Scenes/Dev/Gameplay_Scene.unity"
            };

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[AndroidBuildTool] APK generado en: {Path.GetFullPath(ApkPath)} ({summary.totalSize / (1024f * 1024f):0.0} MB)");
                EditorUtility.RevealInFinder(ApkPath);
            }
            else
            {
                Debug.LogError($"[AndroidBuildTool] Error al generar APK. Resultado: {summary.result}");
            }
        }
    }
}
