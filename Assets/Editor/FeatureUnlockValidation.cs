using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace DrawArrow.Editor
{
    public static class FeatureUnlockValidation
    {
        public static void Run()
        {
            int checks=0;
            void Check(bool value,string reason){if(!value)throw new Exception(reason);checks++;}
            var progress=new ProgressData();
            Check(!FeatureUnlocks.StarRaceUnlocked(progress)&&!FeatureUnlocks.DailyUnlocked(progress)&&!FeatureUnlocks.MoreGamesUnlocked(progress),"Fresh save locks all features");
            FeatureUnlocks.RecordMainWin(progress,9);
            Check(!FeatureUnlocks.StarRaceUnlocked(progress),"Winning level 9 keeps Star Race locked");
            FeatureUnlocks.RecordMainWin(progress,10);
            Check(FeatureUnlocks.StarRaceUnlocked(progress)&&!FeatureUnlocks.DailyUnlocked(progress),"Winning level 10 opens Star Race only");
            FeatureUnlocks.RecordMainWin(progress,14);
            FeatureUnlocks.RecordMainWin(progress,15);
            FeatureUnlocks.RecordMainWin(progress,1);
            Check(progress.mainLevelsCompleted==15,"Replaying an early level retains unlocks");
            const string key="drawarrow.progress.v1";
            bool hadSave=PlayerPrefs.HasKey(key);string previous=PlayerPrefs.GetString(key);
            var host=new GameObject("Feature unlock validation");
            try
            {
                SaveStore.Save(progress);
                var legacy=new ProgressData();legacy.levels[0]=20;
                string oldJson=JsonUtility.ToJson(legacy).Replace(",\"mainLevelsCompleted\":0","");
                Check(!oldJson.Contains("\"mainLevelsCompleted\""),"Migration fixture is an older save");
                PlayerPrefs.SetString(key,oldJson);
                Check(FeatureUnlocks.DailyUnlocked(SaveStore.Load()),"Existing level-21 save keeps its earned unlock");
                var app=host.AddComponent<GameApp>();app.Progress=new ProgressData{sound=false};
                var win=typeof(GameApp).GetMethod("Win",BindingFlags.Instance|BindingFlags.NonPublic);
                app.StartGame(GameMode.Classic,19);
                Check(!FeatureUnlocks.DailyUnlocked(app.Progress)&&app.Progress.mainLevelsCompleted==0,"Selecting level 20 does not grant a win");
                app.StartGame(GameMode.Daily);
                Check(app.Mode==GameMode.Classic&&app.LevelIndex==19,"Daily entry is blocked before the milestone");
                win.Invoke(app,null);
                Check(app.Progress.mainLevelsCompleted==20&&FeatureUnlocks.DailyUnlocked(SaveStore.Load()),"Actual Classic win records the Daily unlock immediately");
                app.StartGame(GameMode.Daily);
                Check(app.Mode==GameMode.Daily&&app.Level!=null,"Daily entry works after winning level 20");
                app.StartGame(GameMode.Classic,14);win.Invoke(app,null);
                app.StartGame(GameMode.Arrows3D,0);
                Check(app.Mode==GameMode.Classic&&app.LevelIndex==14,"Retired modes cannot replace the current board");
                app.StartGame(GameMode.Classic,23);win.Invoke(app,null);
                Check(!FeatureUnlocks.MoreGamesUnlocked(app.Progress),"Winning level 24 keeps More Games locked");
                app.StartGame(GameMode.Classic,24);
                app.StartGame(GameMode.ArrowSwitch,0);
                Check(app.Mode==GameMode.Classic&&app.LevelIndex==24,"Selecting level 25 does not unlock More Games");
                win.Invoke(app,null);
                Check(FeatureUnlocks.MoreGamesUnlocked(SaveStore.Load()),"Winning level 25 persists More Games unlock");
                app.StartGame(GameMode.ArrowSwitch,0);
                Check(app.Mode==GameMode.ArrowSwitch&&app.LevelIndex==0,"Arrow Switch starts after beating level 25");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                if(hadSave)PlayerPrefs.SetString(key,previous);else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
            Directory.CreateDirectory("Verification");
            File.WriteAllText("Verification/feature-unlocks.json","{\"passed\":true,\"checks\":"+checks+",\"starRaceUnlockLevel\":11,\"dailyUnlockLevel\":21,\"moreGamesUnlockLevel\":26}");
            Debug.Log("FEATURE_UNLOCK_VALIDATION_PASSED checks="+checks);
        }
    }
}
