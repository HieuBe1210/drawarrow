#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace DrawArrow
{
    public sealed partial class GameApp
    {
        IEnumerator BlockedArrowPreviewSmoke(string directory)
        {
            Application.runInBackground=true;Directory.CreateDirectory(directory);
            while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            yield return new WaitForSecondsRealtime(1);
            Progress=new ProgressData{sound=false};CircuitStyle.Theme=0;
            StartGame(GameMode.Classic,14);
            int count=0;
            for(int i=0;i<Arrows.Active.Length;i++)
            {
                if(Arrows.CanExit(i))continue;
                count++;
                arrowPress.Begin(i,Vector2.zero,Time.unscaledTime-.5f);arrowPress.Tick(Time.unscaledTime);
                for(int frame=0;frame<4;frame++)yield return null;
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,"level15-blocked-"+i+".png"));
                yield return new WaitForSecondsRealtime(.12f);
                if(PreviewArrow!=i)throw new Exception("Blocked arrow hold lost: "+i);
                arrowPress.Cancel();
            }
            File.WriteAllText(Path.Combine(directory,"runtime.json"),"{\"level\":15,\"blockedArrows\":"+count+"}");
            Application.Quit();
        }
        IEnumerator InteractionSmoke(string directory)
        {
            Application.runInBackground=true;
            Directory.CreateDirectory(directory);
            while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            yield return new WaitForSecondsRealtime(1);
            Progress=new ProgressData{sound=false};CircuitStyle.Theme=0;
            StartGame(GameMode.Classic,0);
            for(int selectedArrow=0;selectedArrow<Arrows.Active.Length;selectedArrow++)
            {
                arrowPress.Begin(selectedArrow,Vector2.zero,Time.unscaledTime-.5f);arrowPress.Tick(Time.unscaledTime);
                for(int frame=0;frame<4;frame++)yield return null;
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,"held-direction-"+selectedArrow+".png"));
                yield return new WaitForSecondsRealtime(.2f);
                arrowPress.Cancel();
            }
            StartGame(GameMode.Classic,6);
            for(int frame=0;frame<8;frame++)yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"01-main.png"));
            yield return new WaitForSecondsRealtime(.4f);
            int selected=Arrows.Hint();
            for(int i=0;i<Arrows.Active.Length;i++)if(!Arrows.CanExit(i)){selected=i;break;}
            int before=Arrows.Remaining;int coins=Progress.coins;int lives=hearts;
            arrowPress.Begin(selected,Vector2.zero,Time.unscaledTime-.5f);arrowPress.Tick(Time.unscaledTime);
            for(int frame=0;frame<4;frame++)yield return null;
            if(PreviewArrow!=selected)throw new Exception("Runtime hold preview missing");
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"02-held-route.png"));
            yield return new WaitForSecondsRealtime(.4f);
            if(arrowPress.Release(Time.unscaledTime)!=-1||Arrows.Remaining!=before||Progress.coins!=coins||hearts!=lives)
                throw new Exception("Preview must not spend coins, move arrows or lose hearts");
            for(int n=0;n<5;n++)TapArrow(Arrows.Hint());
            yield return new WaitForSecondsRealtime(1.5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"03-cleared-dots.png"));
            yield return new WaitForSecondsRealtime(.4f);
            paused=true;yield return new WaitForSecondsRealtime(.5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"04-main-pause.png"));
            yield return new WaitForSecondsRealtime(.4f);
            paused=false;StartGame(GameMode.ArrowClash,0);
            for(int frame=0;frame<4;frame++)yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"05-other-mode-controls.png"));
            yield return new WaitForSecondsRealtime(.4f);
            File.WriteAllText(Path.Combine(directory,"runtime.json"),"{\"passed\":true,\"holdPreview\":true,\"holdDoesNotMoveOrCharge\":true,\"screenshots\":5}");
            Debug.Log("INGAME_INTERACTION_RUNTIME_PASSED");Application.Quit();
        }
    }
}
#endif
