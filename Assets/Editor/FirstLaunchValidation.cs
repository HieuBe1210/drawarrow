using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace DrawArrow.Editor
{
    public static class FirstLaunchValidation
    {
        public static void Run()
        {
            int checks=0;
            void Check(bool value,string reason){if(!value)throw new Exception(reason);checks++;}
            const string key="drawarrow.progress.v1";
            bool hadSave=PlayerPrefs.HasKey(key);string previous=PlayerPrefs.GetString(key);
            var host=new GameObject("First launch validation");
            try
            {
                PlayerPrefs.DeleteKey(key);
                var app=host.AddComponent<GameApp>();app.Progress=SaveStore.Load();
                var open=typeof(GameApp).GetMethod("OpenInitialPage",BindingFlags.NonPublic|BindingFlags.Instance);
                var page=typeof(GameApp).GetField("page",BindingFlags.NonPublic|BindingFlags.Instance);
                Check(!app.Progress.hasStartedGame,"Fresh install is marked as a first launch");
                open.Invoke(app,null);
                Check(page.GetValue(app).ToString()=="Game"&&app.Mode==GameMode.Classic&&app.LevelIndex==0&&app.Level!=null,"First launch goes straight into main level one");
                Check(SaveStore.Load().hasStartedGame,"First-launch flag is saved before any level is won");
                app.Progress=SaveStore.Load();open.Invoke(app,null);
                Check(page.GetValue(app).ToString()=="Home","Reopening before winning level one goes to Home");
                app.Progress.levels[0]=15;SaveStore.Save(app.Progress);open.Invoke(app,null);
                Check(page.GetValue(app).ToString()=="Home"&&app.Progress.levels[0]==15,"Returning players keep progress and open Home");
                string legacy=JsonUtility.ToJson(app.Progress).Replace(",\"hasStartedGame\":true","");
                Check(!legacy.Contains("\"hasStartedGame\""),"Migration fixture predates the first-launch flag");
                PlayerPrefs.SetString(key,legacy);app.Progress=SaveStore.Load();open.Invoke(app,null);
                Check(app.Progress.hasStartedGame&&page.GetValue(app).ToString()=="Home"&&app.Progress.levels[0]==15,"Existing saves are not sent back to level one");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                if(hadSave)PlayerPrefs.SetString(key,previous);else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
            Directory.CreateDirectory("Verification");
            File.WriteAllText("Verification/first-launch.json","{\"passed\":true,\"checks\":"+checks+"}");
            Debug.Log("FIRST_LAUNCH_VALIDATION_PASSED checks="+checks);
        }
    }
}
