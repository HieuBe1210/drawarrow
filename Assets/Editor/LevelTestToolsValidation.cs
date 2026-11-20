using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DrawArrow.Editor
{
    public static class LevelTestToolsValidation
    {
        public static void Run()
        {
            int checks=0;
            void Check(bool value,string reason){if(!value)throw new Exception(reason);checks++;}
            var settings=Resources.Load<LevelTestToolsSettings>("LevelTestToolsSettings");
            Check(settings!=null,"Test tools settings asset exists");
            bool previousEnabled=settings.enableLevelTools;
            const string key="drawarrow.progress.v1";
            bool hadSave=PlayerPrefs.HasKey(key);string previous=PlayerPrefs.GetString(key);
            var host=new GameObject("Level tools validation");
            try
            {
                settings.enableLevelTools=true;Check(LevelTestToolsSettings.Enabled,"Enabled in Editor");
                var app=host.AddComponent<GameApp>();
                app.Progress=new ProgressData{coins=321,wins=17,sound=false,vibration=false};
                app.Progress.levels[(int)GameMode.ArrowClash]=7;
                var jump=typeof(GameApp).GetMethod("TryJumpToMainLevel",BindingFlags.NonPublic|BindingFlags.Instance);
                var reset=typeof(GameApp).GetMethod("ResetMainLevelProgress",BindingFlags.NonPublic|BindingFlags.Instance);
                bool Jump(string value)=>(bool)jump.Invoke(app,new object[]{value});
                Check(Jump("6")&&app.LevelIndex==5&&app.Level.id==CircuitLevels.Load(GameMode.Classic,5).id,"Level input uses the actual one-based main-level number");
                Check(SaveStore.Load().levels[0]==5,"Selected main level is saved for Home Play");
                int max=CircuitLevels.MainSetCount;
                foreach(string invalid in new[]{"0","-1","text","",(max+1).ToString(),"99999999999999999999"})
                    Check(!Jump(invalid)&&app.LevelIndex==5&&app.Progress.levels[0]==5,"Invalid input cannot change current progress");
                Check(Jump("3")&&app.Level.id==CircuitLevels.Load(GameMode.Classic,2).id,"Jump follows the active main level catalog");
                app.Progress.mainLevelsCompleted=15;
                reset.Invoke(app,null);Check(app.Progress.levels[0]==0&&SaveStore.Load().levels[0]==0&&app.Progress.mainLevelsCompleted==0,"Reset returns main progress to level one and clears unlock milestones");
                Check(app.Progress.coins==321&&app.Progress.wins==17&&!app.Progress.sound&&!app.Progress.vibration&&app.Progress.levels[1]==7,"Reset preserves coins, settings and other modes");
                settings.enableLevelTools=false;Check(!LevelTestToolsSettings.Enabled,"Disable switch hides tools");
                Check(!Jump("100")&&app.Progress.levels[0]==0,"Disabled tools cannot jump");
                app.Progress.levels[0]=9;reset.Invoke(app,null);
                Check(app.Progress.levels[0]==9,"Disabled tools cannot reset progress");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);settings.enableLevelTools=previousEnabled;
                if(hadSave)PlayerPrefs.SetString(key,previous);else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
            Directory.CreateDirectory("Verification");
            File.WriteAllText("Verification/level-test-tools.json","{\"passed\":true,\"checks\":"+checks+"}");
            Debug.Log("LEVEL_TEST_TOOLS_VALIDATION_PASSED checks="+checks);
        }
    }
}
