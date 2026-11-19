using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DrawArrow.Editor
{
    public static class ProjectTools
    {
        const string ScenePath="Assets/Scenes/Main.unity";
        [MenuItem("Draw Arrow/Prepare project")]
        public static void Prepare()
        {
            Directory.CreateDirectory("Assets/Scenes");
            PlayerSettings.companyName="Draw Arrow Studio";PlayerSettings.productName="Arrow Circuit";
            PlayerSettings.defaultScreenWidth=540;PlayerSettings.defaultScreenHeight=960;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.Portrait;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,"com.drawarrow.circuit");
            PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.bundleVersion="1.1.0";PlayerSettings.Android.bundleVersionCode=2;
            var appIcon=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/CircuitAppIcon.png");if(appIcon)PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown,new[]{appIcon});
            if(!File.Exists(ScenePath))
            {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var camera = new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.backgroundColor=Ink.Paper;camera.clearFlags=CameraClearFlags.SolidColor;
                EditorSceneManager.SaveScene(scene,ScenePath);
            }
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            AssetDatabase.SaveAssets();
            Debug.Log("DRAW_ARROW_PROJECT_READY");
        }
        [MenuItem("Draw Arrow/Run validation")]
        public static void Validate()
        {
            Prepare();
            foreach (ClassicLevelSet set in Enum.GetValues(typeof(ClassicLevelSet)))
            {
                for (int index = 0; index < 24; index++)
                {
                    var level = CircuitLevels.Load(GameMode.Classic, index, set);
                    var board = new ArrowBoard(level, fitInitialGaps: true);
                    if (board.Solve().Length != board.TargetCount)
                        throw new Exception("Unsolvable campaign board: " + level.id);
                }
            }
            foreach (GameMode mode in Enum.GetValues(typeof(GameMode)))
                if (mode != GameMode.Schulte && CircuitLevels.Load(mode, 0) == null)
                    throw new Exception("Missing board: " + mode);

            CircuitBuildCheck.VerifyAssets();
            if (!Resources.Load<Shader>("Circuit/BoardMaterial"))
                throw new Exception("Missing board shader");
            Debug.Log("ARROW_CIRCUIT_VALIDATED");
        }
        [MenuItem("Draw Arrow/Build Windows")]
        public static void BuildWindows()
        {
            Validate();Directory.CreateDirectory("Builds/Windows");
            Build(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/Windows/ArrowCircuit.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        }
        [MenuItem("Draw Arrow/Build Android APK")]
        public static void BuildAndroid()
        {
            Validate();Directory.CreateDirectory("Builds/Android");
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            EditorUserBuildSettings.buildAppBundle=false;
            Build(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/Android/ArrowCircuit.apk",target=BuildTarget.Android,options=BuildOptions.None});
        }
        static void Build(BuildPlayerOptions options)
        {
            var report=BuildPipeline.BuildPlayer(options);if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+report.summary.result);
            Debug.Log("DRAW_ARROW_BUILD_SUCCEEDED "+report.summary.outputPath);
        }
    }

    sealed class CircuitBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) => VerifyAssets();

        internal static void VerifyAssets()
        {
            foreach (string directory in new[] { "OriginalArt", "Recovered", "Levels" })
                if (Directory.Exists("Assets/Resources/" + directory))
                    throw new BuildFailedException("Move the archived folder out of Assets/Resources: " + directory);
            if (File.Exists("Assets/OriginalAppIcon.png"))
                throw new BuildFailedException("Move the archived app icon out of Assets.");
        }
    }
}
