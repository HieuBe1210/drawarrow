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
            Check(!FeatureUnlocks.DailyUnlocked(progress)&&!FeatureUnlocks.MoreGamesUnlocked(progress),"Fresh save locks both features");
            FeatureUnlocks.RecordMainWin(progress,9);
            Check(!FeatureUnlocks.DailyUnlocked(progress),"Winning level 9 does not unlock Daily");
            FeatureUnlocks.RecordMainWin(progress,10);
            Check(FeatureUnlocks.DailyUnlocked(progress)&&!FeatureUnlocks.MoreGamesUnlocked(progress),"Winning level 10 unlocks Daily only");
            FeatureUnlocks.RecordMainWin(progress,14);
            Check(!FeatureUnlocks.MoreGamesUnlocked(progress),"Winning level 14 does not unlock More Games");
            FeatureUnlocks.RecordMainWin(progress,15);
            Check(FeatureUnlocks.MoreGamesUnlocked(progress),"Winning level 15 unlocks More Games");
            FeatureUnlocks.RecordMainWin(progress,1);
            Check(progress.mainLevelsCompleted==15,"Replaying an early level retains unlocks");
            const string key="drawarrow.progress.v1";
            bool hadSave=PlayerPrefs.HasKey(key);string previous=PlayerPrefs.GetString(key);
            var host=new GameObject("Feature unlock validation");
            try
            {
                SaveStore.Save(progress);
                Check(FeatureUnlocks.MoreGamesUnlocked(SaveStore.Load()),"Unlock milestones survive save/reload");
                var legacy=new ProgressData();legacy.levels[0]=10;
                string oldJson=JsonUtility.ToJson(legacy).Replace(",\"mainLevelsCompleted\":0","");
                Check(!oldJson.Contains("\"mainLevelsCompleted\""),"Migration fixture is an older save");
                PlayerPrefs.SetString(key,oldJson);
                Check(FeatureUnlocks.DailyUnlocked(SaveStore.Load())&&!FeatureUnlocks.MoreGamesUnlocked(SaveStore.Load()),"Existing level-11 save keeps its earned unlock");
                legacy.levels[0]=15;oldJson=JsonUtility.ToJson(legacy).Replace(",\"mainLevelsCompleted\":0","");PlayerPrefs.SetString(key,oldJson);
                Check(FeatureUnlocks.MoreGamesUnlocked(SaveStore.Load()),"Existing level-16 save keeps More Games");
                var app=host.AddComponent<GameApp>();app.Repository=new LevelRepository();app.Progress=new ProgressData{sound=false};
                var win=typeof(GameApp).GetMethod("Win",BindingFlags.Instance|BindingFlags.NonPublic);
                var more=typeof(GameApp).GetMethod("TryOpenMoreGames",BindingFlags.Instance|BindingFlags.NonPublic);
                app.StartGame(GameMode.Classic,9);
                Check(!FeatureUnlocks.DailyUnlocked(app.Progress)&&app.Progress.mainLevelsCompleted==0,"Selecting level 10 does not grant a win");
                app.StartGame(GameMode.Daily);
                Check(app.Mode==GameMode.Classic&&app.LevelIndex==9,"Daily entry is blocked before the milestone");
                Check(!(bool)more.Invoke(app,null),"More Games entry is blocked before the milestone");
                win.Invoke(app,null);
                Check(app.Progress.mainLevelsCompleted==10&&FeatureUnlocks.DailyUnlocked(SaveStore.Load()),"Actual Classic win records the Daily unlock immediately");
                app.StartGame(GameMode.Daily);
                Check(app.Mode==GameMode.Daily&&app.Level!=null,"Daily entry works after winning level 10");
                app.StartGame(GameMode.Classic,14);win.Invoke(app,null);
                Check((bool)more.Invoke(app,null)&&FeatureUnlocks.MoreGamesUnlocked(SaveStore.Load()),"More Games opens and stays unlocked after winning level 15");
                app.Progress=new ProgressData{sound=false};app.StartGame(GameMode.Arrows3D,0);win.Invoke(app,null);
                Check(app.Progress.mainLevelsCompleted==0,"Wins in another mode do not unlock main-level milestones");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                if(hadSave)PlayerPrefs.SetString(key,previous);else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
            Directory.CreateDirectory("Verification");
            File.WriteAllText("Verification/feature-unlocks.json","{\"passed\":true,\"checks\":"+checks+",\"dailyUnlockLevel\":11,\"moreGamesUnlockLevel\":16}");
            Debug.Log("FEATURE_UNLOCK_VALIDATION_PASSED checks="+checks);
        }
    }
}
