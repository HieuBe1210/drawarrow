using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DrawArrow.Editor
{
    public static class IngameInteractionValidation
    {
        [MenuItem("Draw Arrow/Validate arrow hold interaction")]
        public static void Run()
        {
            int checks=0;
            void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
            var press=new ArrowPressGesture();
            press.Begin(3,Vector2.zero,10);press.Tick(10.2f);
            Check(!press.Held&&press.Release(10.2f)==3,"Quick tap selects pressed arrow");
            press.Begin(3,Vector2.zero,10);press.Tick(10.5f);
            Check(press.Held&&press.Arrow==3,"Hold previews pressed arrow");
            Check(press.Release(10.6f)==-1&&!press.Held,"Hold release does not become a tap");
            press.Begin(3,Vector2.zero,10);
            Check(press.Release(10.5f)==-1,"Long release without an intermediate frame is still a hold");
            press.Begin(3,Vector2.zero,10);press.Move(new Vector2(30,0));press.Tick(11);
            Check(!press.Held&&press.Release(11)==-1,"Dragging cancels hold and tap");
            press.Begin(3,Vector2.zero,10);press.Move(new Vector2(3,2));press.Tick(10.5f);
            Check(press.Held,"Small finger movement preserves hold");
            press.Begin(3,Vector2.zero,10);press.Move(new Vector2(50,0),true);press.Tick(10.5f);
            Check(press.Held&&press.Arrow==3,"Drifting along the selected arrow preserves its hold");
            press.Move(new Vector2(55,3));
            Check(press.Held,"Small movement after sliding along the arrow preserves its hold");
            press.Move(new Vector2(90,30));
            Check(!press.Held&&press.Release(11)==-1,"Dragging away from the selected arrow cancels without a tap");
            press.Cancel();Check(press.Arrow==-1&&!press.Held&&press.Release(11)==-1,"Cancel resets pointer state");
            press.Begin(-1,Vector2.zero,10);press.Tick(11);
            Check(!press.Held&&press.Release(11)==-1,"Holding empty space cannot select an arrow");
            press.Begin(3,Vector2.zero,10);press.Tick(11);press.Cancel();press.Begin(4,Vector2.zero,12);
            Check(press.Release(12.1f)==4,"New press works after a cancelled hold");
            foreach(float screenScale in new[]{.3f,.5f,1f})
            {
                press.Begin(3,Vector2.zero,10,18/screenScale);
                press.Move(new Vector2(8/screenScale,4/screenScale));press.Tick(10.5f);
                Check(press.Held&&press.Release(10.6f)==-1,"Finger jitter preserves hold consistently at each screen scale");
                press.Begin(3,Vector2.zero,10,18/screenScale);press.Move(new Vector2(25/screenScale,0));press.Tick(11);
                Check(!press.Held&&press.Release(11)==-1,"A deliberate drag still cancels the hold");
            }
            var previewViewport=new Rect(0,0,1080,1920);
            foreach(var direction in new[]{Vector2.right,Vector2.left,Vector2.up,Vector2.down})
            {
                var origin=new Vector2(540,960);float distance=BoardLayout.RayDistanceToEdge(origin,direction,previewViewport);
                Check(distance>0&&!float.IsInfinity(distance)&&!float.IsNaN(distance),"Every cardinal preview ray reaches an edge");
                var end=origin+direction*distance;
                Check(Mathf.Abs(end.x-previewViewport.xMin)<.01f||Mathf.Abs(end.x-previewViewport.xMax)<.01f||Mathf.Abs(end.y-previewViewport.yMin)<.01f||Mathf.Abs(end.y-previewViewport.yMax)<.01f,"Preview extends to the screen edge instead of the board clip");
                var side=new Vector2(-direction.y,direction.x);
                Check(BoardLayout.ArrowHeadDistance(origin-direction*40+side*10,origin,direction,50)==0,"Arrowhead wings are inside the hit area");
            }
            Check(BoardLayout.RayDistanceToEdge(Vector2.one,Vector2.zero,previewViewport)==0,"Invalid direction cannot produce an infinite preview loop");
            Check(BoardLayout.RayDistanceToEdge(new Vector2(0,200),new Vector2(-.0000001f,1),previewViewport)>1700,"Off-axis projection noise cannot erase a vertical preview at the left edge");
            Check(BoardLayout.RayDistanceToEdge(new Vector2(200,0),new Vector2(1,-.0000001f),previewViewport)>800,"Off-axis projection noise cannot erase a horizontal preview at the top edge");
            var initialGapBoard=new ArrowBoard(new LevelData{width=6,height=4,arrows=new[]{
                new ArrowData{points=new[]{7,6}},
                new ArrowData{points=new[]{15,3}}
            }},fitInitialGaps:true);
            Check(initialGapBoard.Head(0)-initialGapBoard.Paths[0][0]==Vector3.right*2,"The initial head extends toward the blocker");
            Check(!initialGapBoard.CanExit(0)&&initialGapBoard.CanExit(1),"Closing the initial gap preserves the exit order");
            var spreadLevel=new LevelData{width=8,height=8,arrows=new[]{
                new ArrowData{points=new[]{1,0}},
                new ArrowData{points=new[]{45,44}}
            }};
            var spread=new ArrowBoard(spreadLevel,fitInitialGaps:true);
            var compact=new ArrowBoard(spreadLevel,fitInitialGaps:true,compactLayout:true);
            Check(compact.ContentBounds.width*compact.ContentBounds.height<
                spread.ContentBounds.width*spread.ContentBounds.height,"Compaction shrinks the occupied area");
            for(int i=0;i<compact.Paths.Length;i++)
                Check(compact.Head(i)-compact.Paths[i][0]==spread.Head(i)-spread.Paths[i][0]&&
                    compact.CanExit(i)==spread.CanExit(i),"Compaction preserves unit lengths and exit dependencies");
            foreach(var direction in new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down})
            {
                int tail=12-direction.y+direction.x*5;
                var boardWithTurn=new ArrowBoard(new LevelData{width=5,height=5,arrows=new[]{
                    new ArrowData{points=new[]{12,tail},dx=direction.x,dy=direction.y}
                }},fitInitialGaps:true);
                var path=boardWithTurn.Paths[0];
                var lastStep=path[path.Count-1]-path[path.Count-2];
                Check(lastStep==new Vector3(direction.x,direction.y),"An arrow ends with a full step in the requested direction");
                var motion=new ArrowMotionPath(path,lastStep,0);
                Check(motion.Sample(motion.Length)==path[path.Count-1],"The rendered tip is the exact path endpoint");
                var shaft=new System.Collections.Generic.List<Vector3>();
                motion.Slice(0,shaft,.4f);
                Check((shaft[shaft.Count-1]+lastStep*.4f-motion.Sample(motion.Length)).sqrMagnitude<.000001f,
                    "The shaft ends behind the tip on the same straight segment");
            }
            var crossingBoard=new ArrowBoard(new LevelData{width=7,height=6,arrows=new[]{
                new ArrowData{points=new[]{22,21}},
                new ArrowData{points=new[]{33,19}},
                new ArrowData{points=new[]{10,3,6,41,40}}
            }},fitInitialGaps:true);
            for(int i=0;i<crossingBoard.Paths.Length;i++)
            {
                if(crossingBoard.CanExit(i))continue;
                var next=ArrowBoard.Key(crossingBoard.Head(i)+crossingBoard.Direction(i));
                Check(crossingBoard.Occupancy.TryGetValue(next,out int neighbor)&&neighbor!=i,
                    "A blocked arrow ends one step before its obstacle");
            }
            Check(crossingBoard.Solve().Length==crossingBoard.Paths.Length,"Closing crossing routes must leave a solution");
            var repository=new LevelRepository();
            var fifthLevel=repository.Load("lengthmatched",4);
            var fifthPacked=new ArrowBoard(fifthLevel,fitInitialGaps:true,compactLayout:true);
            var packedHead=fifthPacked.Head(40);
            Check(packedHead.x>fifthPacked.ContentBounds.xMin&&packedHead.x<fifthPacked.ContentBounds.xMax&&
                packedHead.y>fifthPacked.ContentBounds.yMin&&packedHead.y<fifthPacked.ContentBounds.yMax,
                "The isolated short arrow in Level 5 fits inside the packed board");
            Check(fifthPacked.Solve().Length==fifthPacked.Paths.Length,"Packing Level 5 preserves a full solution");
            var openingLevel=repository.Load("lengthmatched",0);
            var openingBoard=new ArrowBoard(openingLevel,fitInitialGaps:true);
            Check(openingBoard.DotPositions[3].Count==3,"Level 1 keeps the lower arrow's elbow on the visual grid");
            var lowerHead=openingBoard.Head(3);
            var nextNode=ArrowBoard.Key(lowerHead+openingBoard.Direction(3));
            Check(openingBoard.Occupancy.TryGetValue(nextNode,out int openingBlocker)&&openingBlocker==1,
                "The lower arrow starts one grid step before the upper body");
            var oversizedBoard=new ArrowBoard(new LevelData{width=250,height=20,arrows=new[]{
                new ArrowData{points=new[]{0,19*250+249}},
                new ArrowData{points=new[]{18*250+245,17*250+245}}
            }},fitInitialGaps:true);
            Check(oversizedBoard.ContentBounds.width==249,"A long layout keeps its span on the extended grid");
            foreach(var testBoard in new[]{openingBoard,oversizedBoard})
            foreach(var path in testBoard.Paths)
            {
                for(int i=0;i<path.Count;i++)
                {
                    var point=path[i];
                    Check(point==(Vector3)ArrowBoard.Key(point)&&point.x>=0&&point.y>=0&&
                        point.x<=ArrowBoard.ExtendedGridSteps&&point.y<=ArrowBoard.FlatGridSteps,
                        "Oversized source paths use integer nodes within the fixed board");
                    if(i==0)continue;
                    var step=point-path[i-1];
                    Check(Mathf.Abs(step.x)+Mathf.Abs(step.y)==1,"Arrow bodies use cardinal unit steps");
                }
            }
            Check(oversizedBoard.Solve().Length==oversizedBoard.Paths.Length,"The fixed-grid board remains solvable");
            var thirdLevel=repository.Load(GameMode.Classic,2);
            Check(thirdLevel.id==repository.Load("classic",2).id,"Level 3 loads its base layout");
            var thirdBase=new ArrowBoard(thirdLevel);
            var thirdBoard=new ArrowBoard(thirdLevel,fitInitialGaps:true,compactLayout:true,preserveSourceLengths:true);
            var levelThreeLengths=new[]{3,7,8,3};
            var originShift=thirdBoard.Paths[0][0]-thirdBase.Paths[0][0];
            for(int i=0;i<levelThreeLengths.Length;i++)
            {
                Check(thirdBoard.Paths[i].Count-1==levelThreeLengths[i],"Level 3 keeps each original arrow length");
                for(int j=0;j<thirdBoard.Paths[i].Count;j++)
                    Check(thirdBoard.Paths[i][j]-thirdBase.Paths[i][j]==originShift,"Level 3 keeps its original geometry");
            }
            Check(BoardLayout.FitArrowScale(previewViewport,thirdBoard)==BoardLayout.FitArrowScale(previewViewport,thirdBase),
                "Level 3 keeps its original display scale");
            Check((thirdBoard.ContentBounds.width+1)*(thirdBoard.ContentBounds.height+1)==thirdBoard.Occupancy.Count,
                "Packing leaves no unused nodes in Level 3");
            var tenthLevel=repository.Load(GameMode.Classic,9);
            var tenthBase=new ArrowBoard(tenthLevel,fitInitialGaps:true,preserveSourceLengths:true);
            var tenthPacked=new ArrowBoard(tenthLevel,fitInitialGaps:true,compactLayout:true,preserveSourceLengths:true);
            float tenthEmpty=(tenthPacked.ContentBounds.width+1)*(tenthPacked.ContentBounds.height+1)-tenthPacked.Occupancy.Count;
            Check(tenthEmpty<=506&&tenthPacked.Occupancy.Count==tenthBase.Occupancy.Count,
                "Packing shared edges removes empty nodes in Level 10");
            Check(tenthPacked.Solve().Length==tenthPacked.Paths.Length,"Packing shared edges preserves a solution");
            var twentiethLevel=repository.Load(GameMode.Classic,19);
            var twentiethPacked=new ArrowBoard(twentiethLevel,fitInitialGaps:true,compactLayout:true,preserveSourceLengths:true);
            float twentiethEmpty=(twentiethPacked.ContentBounds.width+1)*(twentiethPacked.ContentBounds.height+1)-
                twentiethPacked.Occupancy.Count;
            Check(twentiethLevel.packedFitBounds?.Length==2&&twentiethEmpty<=173,
                "Level 20 loads its precomputed routes through the interior holes");
            Check(twentiethPacked.Solve().Length==twentiethPacked.Paths.Length,"The packed Level 20 remains solvable");
            var board=new ArrowBoard(repository.Load(GameMode.Classic,6));
            Check(board.ClearedCells.Count==0,"Fresh board has no footprint dots");
            int arrow=board.Hint();int active=board.Remaining;
            Check(arrow>=0&&board.Remove(arrow)&&board.Remaining==active-1,"Clear an arrow for dot footprints");
            foreach(var cell in board.DotPositions[arrow])Check(!board.Occupancy.ContainsKey(ArrowBoard.Key(cell))&&board.ClearedCells.Contains(cell),"Original segment vertices become visible dots");
            Check(board.ClearedCells.Count==board.DotPositions[arrow].Count,"Only the segment vertices leave dots");
            board.Restore(arrow);
            foreach(var cell in board.Paths[arrow])Check(board.Occupancy.ContainsKey(ArrowBoard.Key(cell)),"Undo covers the dots again in modes that support it");
            Check(board.ClearedCells.Count==0,"Undo removes the footprint dots");
            for(int i=0;i<5;i++)Check(board.Remove(board.Hint()),"Remove several arrows for footprints");
            Check(board.ClearedCells.Count>0,"Footprints persist after multiple removals");
            foreach(var cell in board.ClearedCells)Check(!board.Occupancy.ContainsKey(ArrowBoard.Key(cell)),"Dots never cover an active arrow");
            Check(new ArrowBoard(repository.Load(GameMode.Classic,6)).ClearedCells.Count==0,"Next level clears old dots");
            foreach(var fixture in new[]{
                new[]{0,1,2,3,4}, // one straight segment despite five grid cells
                new[]{24,20,0},   // two straight segments
                new[]{54,50,20,22} // three straight segments
            })
            {
                int expected=fixture.Length==5?2:fixture.Length;
                var sample=new ArrowBoard(new LevelData{width=10,height=10,arrows=new[]{new ArrowData{points=fixture}}});
                Check(sample.DotPositions[0].Count==expected,"X straight segments have X+1 dots");
                Check(sample.Remove(0)&&sample.ClearedCells.Count==expected,"Removal leaves exactly X+1 dots");
                foreach(int point in fixture)
                    if(fixture.Length!=5||point==0||point==4)Check(sample.ClearedCells.Contains(new Vector3Int(point%10,point/10,0)),"Dot remains at an original endpoint or corner");
                sample.Restore(0);Check(sample.ClearedCells.Count==0,"Restart/undo removes all original vertex dots");
            }
            var elbow=new ArrowBoard(new LevelData{width=10,height=10,arrows=new[]{new ArrowData{points=new[]{54,50,20,22}}}});
            elbow.Remove(0);Check(!elbow.ClearedCells.Contains(new Vector3Int(2,5,0)),"No intermediate grid-cell dots along a straight segment");
            foreach(var direction in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down})
            {
                var single=new ArrowBoard(new LevelData{width=5,height=5,arrows=new[]{new ArrowData{points=new[]{12},dx=direction.x,dy=direction.y}}},fitInitialGaps:true);
                Check(single.Paths[0].Count==2&&single.DotPositions[0].Count==2,"Shortest arrow is one segment with two distinct endpoints");
                Check(single.Occupancy.Count==2&&
                    (single.Paths[0][1]-single.Paths[0][0]).sqrMagnitude==1,
                    "Shortest arrow spans one grid step");
                Check(single.Direction(0)==new Vector3(direction.x,direction.y,0),"Shortest arrow preserves its direction");
                Check(single.Remove(0)&&single.ClearedCells.Count==2,"Shortest arrow leaves exactly two dots at its initial endpoints");
            }
            Ink.Init();OriginalArt.Load();
            foreach(string icon in new[]{"CommonIconHeart1","CommonIconHeart2","CommonIconHeart3","CommonIconHeart4","IconLifeV2","Style3HeartWater"})
                Check(OriginalArt.IsLifeIcon(OriginalArt.Find(icon).name),"Every legacy life icon renders as a red heart");
            var heartTexture=(Texture2D)typeof(Ink).GetField("heart",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
            Check(heartTexture.GetPixel(64,120).a<.1f&&heartTexture.GetPixel(40,120).a>.9f&&heartTexture.GetPixel(88,120).a>.9f,"Heart silhouette has two lobes and a central notch");
            var bounds=typeof(GameApp).GetMethod("SettingsToggleBounds",BindingFlags.NonPublic|BindingFlags.Static);
            foreach(string layout in new[]{"GameSettingUI","GameSettingV2UI","GameSettingStyle3UI"})
            {
                var hitboxes=new Rect[2];int k=0;
                foreach(string item in new[]{"Sound","Vibration"})
                {
                    var hit=(Rect)bounds.Invoke(null,new object[]{layout,item,null});hitboxes[k++]=hit;
                    var toggle=OriginalArt.NodeRect(layout,"/AreaBg/"+item+"/TogglePar",new Rect(0,0,1080,1920),new Vector2(1080,1920));
                    Check(hit.Contains(toggle.center)&&hit.Contains(toggle.min+Vector2.one)&&hit.Contains(toggle.max-Vector2.one),layout+" "+item+" switch is clickable across its entire area");
                }
                Check(!hitboxes[0].Overlaps(hitboxes[1]),layout+" sound and vibration hitboxes do not overlap");
                var restart=OriginalArt.NodeRect(layout,layout=="GameSettingStyle3UI"?"/BtnOk/ContinueBtn":"/DlgBtn/RestartBtn",new Rect(0,0,1080,1920),new Vector2(1080,1920));
                Check(restart.width>0&&restart.height>0,layout+" restart button has a usable hitbox");
            }
            const string key="drawarrow.progress.v1";
            bool hadSave=PlayerPrefs.HasKey(key);string previous=PlayerPrefs.GetString(key);
            var host=new GameObject("Settings validation");
            try
            {
                var app=host.AddComponent<GameApp>();app.Progress=new ProgressData();
                var toggle=typeof(GameApp).GetMethod("ToggleSetting",BindingFlags.NonPublic|BindingFlags.Instance);
                toggle.Invoke(app,new object[]{"Sound"});
                Check(!app.Progress.sound&&!SaveStore.Load().sound&&app.Progress.vibration,"Sound OFF is independent and saved");
                toggle.Invoke(app,new object[]{"Vibration"});
                Check(!app.Progress.vibration&&!SaveStore.Load().vibration&&!app.Progress.sound,"Vibration OFF is independent and saved");
                toggle.Invoke(app,new object[]{"Sound"});toggle.Invoke(app,new object[]{"Vibration"});
                Check(SaveStore.Load().sound&&SaveStore.Load().vibration,"Both switches can turn back ON");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                if(hadSave)PlayerPrefs.SetString(key,previous);else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
            var pickHost=new GameObject("Arrow picking validation");
            try
            {
                var app=pickHost.AddComponent<GameApp>();
                var pick=typeof(GameApp).GetMethod("PickArrow",BindingFlags.NonPublic|BindingFlags.Instance);
                var project=typeof(GameApp).GetMethod("Project",BindingFlags.NonPublic|BindingFlags.Instance);
                void Set(string name,object value)=>typeof(GameApp).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(app,value);
                foreach(float screenScale in new[]{.3f,.5f,1f})
                {
                    Set("scale",screenScale);Set("boardCell",80f);Set("projectedCenter",new Vector2(540,800));
                    foreach(int sample in new[]{0,4,6,9,19})
                    {
                        app.Level=repository.Load(GameMode.Classic,sample);app.Arrows=new ArrowBoard(app.Level);
                        for(int i=0;i<app.Arrows.Active.Length;i++)
                        {
                            var head=(Vector2)project.Invoke(app,new object[]{app.Arrows.Head(i)});
                            Check((int)pick.Invoke(app,new object[]{head})==i,"Each active main-level arrow head can start a hold");
                        }
                    }
                }
                // Use the fitted phone viewport, including rounded bends rather than
                // only testing widely spaced arrow tips at a fixed cell size.
                var fittedViewport=new Rect(0,0,1016,1435);
                Set("scale",.3f);Set("projectedCenter",fittedViewport.center);
                for(int sample=0;sample<200;sample++)
                {
                    app.Level=repository.Load(GameMode.Classic,sample);app.Arrows=new ArrowBoard(app.Level);
                    Set("boardCell",BoardLayout.FitScale(fittedViewport,BoardLayout.OrientedBounds(app.Arrows.ContentBounds)));
                    for(int i=0;i<app.Arrows.Active.Length;i++)
                    {
                        var path=app.Arrows.Paths[i];
                        var route=new ArrowMotionPath(path,app.Arrows.Direction(i));
                        for(int position=0;position<=4;position++)
                        {
                            var point=(Vector2)project.Invoke(app,new object[]{route.Sample(route.Length*position/4)});
                            Check((int)pick.Invoke(app,new object[]{point})==i,"Level "+(sample+1)+" arrow "+i+" body sample "+position+" receives hold");
                        }
                        var head=(Vector2)project.Invoke(app,new object[]{app.Arrows.Head(i)});
                        var next=(Vector2)project.Invoke(app,new object[]{app.Arrows.Head(i)+app.Arrows.Direction(i)});
                        Check(BoardLayout.RayDistanceToEdge(head,(next-head).normalized,fittedViewport)>0,"Every fitted arrow has a visible preview ray");
                    }
                }
                app.Level=new LevelData{width=5,height=5,arrows=new[]{new ArrowData{points=new[]{12},dx=1,dy=0}}};app.Arrows=new ArrowBoard(app.Level);
                Set("scale",.5f);Set("boardCell",20f);var shortHead=(Vector2)project.Invoke(app,new object[]{app.Arrows.Head(0)});
                Check((int)pick.Invoke(app,new object[]{shortHead+new Vector2(0,20)})==0,"A near-head touch on the shortest arrow has a usable physical hit area");
            }
            finally{UnityEngine.Object.DestroyImmediate(pickHost);}
            int previousTheme=OriginalArt.Theme;
            var accent=OriginalArt.Accent;
            try
            {
                for(int theme=0;theme<3;theme++)
                {
                    OriginalArt.Theme=theme;
                    Check(OriginalArt.Background==OriginalArt.ThemeBackground(theme),"Theme selects only its intended background palette");
                    Check(OriginalArt.Accent==accent,"Theme keeps the same button and heart accent");
                    var ink=OriginalArt.ArrowInk.linear;var bg=OriginalArt.Background.linear;
                    float a=.2126f*ink.r+.7152f*ink.g+.0722f*ink.b,b=.2126f*bg.r+.7152f*bg.g+.0722f*bg.b;
                    Check((Mathf.Max(a,b)+.05f)/(Mathf.Min(a,b)+.05f)>7,"Arrows have strong contrast on each background");
                }
            }
            finally{OriginalArt.Theme=previousTheme;}
            Directory.CreateDirectory("Verification");
            File.WriteAllText("Verification/ingame-interaction-validation.json","{\"passed\":true,\"checks\":"+checks+"}");
            Debug.Log("INGAME_INTERACTION_VALIDATION_PASSED checks="+checks);
        }
        public static void BuildSmokePlayer()
        {
            Run();LevelTestToolsValidation.Run();FeatureUnlockValidation.Run();
            Directory.CreateDirectory("Assets/Scenes");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene,"Assets/Scenes/InteractionSmoke.unity");
            PlayerSettings.defaultScreenWidth=540;PlayerSettings.defaultScreenHeight=960;
            PlayerSettings.resizableWindow=true;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.SplashScreen.show=false;
            string output=Path.GetFullPath("../InteractionSmokePlayer/Game.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=new[]{"Assets/Scenes/InteractionSmoke.unity"},locationPathName=output,
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Smoke build failed");
            Debug.Log("INGAME_INTERACTION_SMOKE_PLAYER_BUILT "+output);
        }
        public static void BuildReleaseToolsProbe()
        {
            LevelTestToolsValidation.Run();
            string output=Path.GetFullPath("../LevelToolsReleaseProbe/Game.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=new[]{"Assets/Scenes/InteractionSmoke.unity"},locationPathName=output,
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Release tools probe build failed");
            Debug.Log("LEVEL_TOOLS_RELEASE_PROBE_BUILT "+output);
        }
    }
}
