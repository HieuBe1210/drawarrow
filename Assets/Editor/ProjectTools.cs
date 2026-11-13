using System;
using System.IO;
using System.Linq;
using UnityEditor;
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
            PlayerSettings.companyName="Draw Arrow Studio";PlayerSettings.productName="Amaze Go Rebuilt";
            PlayerSettings.defaultScreenWidth=540;PlayerSettings.defaultScreenHeight=960;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.Portrait;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,"com.drawarrow.amazerebuilt");
            PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.bundleVersion="1.1.0";PlayerSettings.Android.bundleVersionCode=2;
            var appIcon=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/OriginalAppIcon.png");if(appIcon)PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown,new[]{appIcon});
            if(!File.Exists(ScenePath))
            {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var camera = new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.backgroundColor=Ink.Paper;camera.clearFlags=CameraClearFlags.SolidColor;
                EditorSceneManager.SaveScene(scene,ScenePath);
            }
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            ConfigureAssets();AssetDatabase.SaveAssets();
            Debug.Log("DRAW_ARROW_PROJECT_READY");
        }
        static void ConfigureAssets()
        {
            foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Resources/OriginalArt"}))
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(!importer)continue;
                var android=importer.GetPlatformTextureSettings("Android");
                if(importer.maxTextureSize==2048&&!importer.mipmapEnabled&&importer.wrapMode==TextureWrapMode.Clamp&&android.overridden&&android.format==TextureImporterFormat.ASTC_6x6&&android.compressionQuality==50)continue;
                importer.maxTextureSize=2048;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.compressionQuality=100;
                android.overridden=true;android.name="Android";android.maxTextureSize=2048;android.format=TextureImporterFormat.ASTC_6x6;android.compressionQuality=50;importer.SetPlatformTextureSettings(android);importer.SaveAndReimport();
            }
            foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Resources/Recovered"}))
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(!importer)continue;
                if(importer.maxTextureSize==1024&&importer.mipmapEnabled==false)continue;
                importer.maxTextureSize=1024;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Compressed;importer.SaveAndReimport();
            }
        }
        [MenuItem("Draw Arrow/Run validation")]
        public static void Validate()
        {
            Prepare();int checks=0;
            void Check(bool condition,string message){if(!condition)throw new Exception("Validation failed: "+message);checks++;}
            var repository=new LevelRepository();
            OriginalArt.Load();Check(OriginalArt.SpriteCount>=2138,"original sprite catalog");Check(OriginalArt.Find("CommonBtnBig")!=null,"original button artwork");Check(OriginalArt.GetLayout("HomeUI")!=null&&OriginalArt.GetLayout("HomeStyle3UI")!=null&&OriginalArt.GetLayout("HomeV2UI")!=null,"three source UI themes");Check(Resources.Load<Font>("OriginalArt/font_Inter_28pt_Bold")!=null,"recovered Inter font");Check(Resources.Load<Shader>("OriginalArt/ArtSurface")!=null,"opaque 3D surface shader");Check(ArtAnimation.Count>600,"recovered animation curves");
            var homeRect=new Rect(0,0,1080,1920);var design=new Vector2(1080,1920);var playBounds=OriginalArt.NodeRect("HomeUI","/Button/PlayBtn",homeRect,design);var playText=OriginalArt.NodeRect("HomeUI","/PlayBtn/PlayBtnBG/PlayText",homeRect,design);Check(playBounds.Contains(playText.center),"native layout group keeps Play label inside its button");Check(Resources.Load<Font>("OriginalArt/font_Mikado-Bold")!=null,"recovered logo typeface");
            Check(repository.Count("classic")==22543,"classic catalog count");Check(repository.Count("clash")==2500,"clash catalog count");Check(repository.Count("cube")==2619,"3D catalog count");
            Check(repository.Count("octa")==200,"octa catalog count");Check(repository.Count("links")==20,"link catalog count");Check(repository.Count("line")==20,"one-line catalog count");
            for(int n=0;n<100;n++)
            {
                var board=new ArrowBoard(repository.Load("classic",n));var solution=board.Solve();Check(solution.Length==board.Active.Length,"classic solution "+n);
                foreach(int i in solution)Check(board.Remove(i),"legal removal "+n+":"+i);Check(board.Remaining==0,"complete classic "+n);
                int arrow=solution[0];board.Restore(arrow);Check(board.Remaining==1&&board.Active[arrow],"undo restores occupancy "+n);
            }
            var tray=new MatchTray();Check(!tray.Add(2)&&!tray.Add(2)&&tray.Add(2)&&tray.Colors.Count==0,"triple matching");
            for(int i=0;i<7;i++)tray.Add(i);Check(tray.Full,"tray capacity");
            for(int n=0;n<20;n++)
            {
                var line=new OneLineBoard(repository.Load("line",n));foreach(int v in line.Level.solution)Check(line.Visit(v),"original Euler solution "+n);Check(line.Complete,"complete line "+n);
                line.Undo();Check(!line.Complete,"undo line "+n);
                var link=new LinkBoard(repository.Load("links",n));Check(link.Level.pairs.All(p=>p.points.Length==2),"link endpoint pairs "+n);
            }
            Check(LinkBoard.Intersect(Vector2.zero,Vector2.one,new Vector2(0,1),new Vector2(1,0)),"link crossing detected");
            Check(!LinkBoard.Intersect(Vector2.zero,Vector2.right,Vector2.up,new Vector2(1,1)),"parallel separated links allowed");
            Check(LinkBoard.Intersect(Vector2.zero,new Vector2(2,0),Vector2.right,new Vector2(3,0)),"collinear overlapping links rejected");
            var s=new SchulteBoard(5,42);Check(s.Numbers.Distinct().Count()==25,"Schulte permutation");for(int v=1;v<=25;v++)Check(s.Tap(Array.IndexOf(s.Numbers,v)),"Schulte sequence");Check(s.Complete,"Schulte victory");
            var small=new LevelData{width=3,height=3,moveLimit=10,arrows=new[]{new ArrowData{points=new[]{0},dx=1,dy=0,kind="normal"}}};
            var octa=new OctaBoard(small);Check(octa.CanExit(0)&&octa.Tap(0)&&octa.Remaining==0,"octa legal escape");
            for(int n=0;n<20;n++){var cube=new ArrowBoard(repository.Load("cube",n),true);Check(cube.Paths.All(p=>p.Count>0)&&cube.TargetCount>0,"3D paths and mechanism tiles "+n);Check(cube.Hint()>=0,"3D starts with an exit "+n);}
            foreach(GameMode m in Enum.GetValues(typeof(GameMode)))if(m!=GameMode.Schulte)Check(repository.Load(m,0)!=null,"mode level data "+m);
            Directory.CreateDirectory("Verification");File.WriteAllText("Verification/validation.json","{\"passed\":true,\"checks\":"+checks+",\"classicSolved\":100,\"oneLineSolved\":20,\"cubeSamples\":20,\"unityVersion\":\""+Application.unityVersion+"\"}");
            Debug.Log("DRAW_ARROW_VALIDATION_PASSED checks="+checks);
        }
        [MenuItem("Draw Arrow/Build Windows")]
        public static void BuildWindows()
        {
            Validate();Directory.CreateDirectory("Builds/Windows");
            Build(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/Windows/AmazeGo.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        }
        [MenuItem("Draw Arrow/Build Android APK")]
        public static void BuildAndroid()
        {
            Validate();Directory.CreateDirectory("Builds/Android");
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            EditorUserBuildSettings.buildAppBundle=false;
            Build(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/Android/AmazeGo-Rebuilt.apk",target=BuildTarget.Android,options=BuildOptions.None});
        }
        static void Build(BuildPlayerOptions options)
        {
            var report=BuildPipeline.BuildPlayer(options);if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+report.summary.result);
            Debug.Log("DRAW_ARROW_BUILD_SUCCEEDED "+report.summary.outputPath);
        }
    }
}
