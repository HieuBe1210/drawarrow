using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DrawArrow
{
    public sealed partial class GameApp : MonoBehaviour
    {
        public const float Width=1080,Height=1920;
        public ProgressData Progress;public GameMode Mode;public int LevelIndex;
        bool SelectedGeneratedSet => Progress.SelectedLevelSet == ClassicLevelSet.Generated;
        bool SelectedFiniteSet => Progress.SelectedLevelSet != ClassicLevelSet.Original;
        int ActiveMainLevel => SelectedFiniteSet
            ? Mathf.Clamp(Progress.SelectedClassicIndex, 0, ActiveMainLevelCount - 1)
            : Progress.SelectedClassicIndex;
        int ActiveMainLevelCount => CircuitLevels.Count(Progress.SelectedLevelSet);
        bool FinishedLevelSet => Mode == GameMode.Classic && SelectedFiniteSet && won && LevelIndex == ActiveMainLevelCount - 1;
        public ArrowBoard Arrows;public OctaBoard Octa;public OneLineBoard OneLine;public LinkBoard Links;public SchulteBoard Schulte;public LevelData Level;
        MatchTray tray=new MatchTray();readonly Stack<UndoState> history=new Stack<UndoState>();
        sealed class UndoState {public int arrow;public List<int> colors;public int hearts;public int moves;public int urgent,deadline;}
        enum Page { Home, Game, Journey, Collection, Settings }
        Page page;bool paused,won,failed,modalHelp;int hearts,moves,hint=-1;float hintUntil,elapsed,timeLeft,botProgress;
        float yaw=-35,pitch=22,roll,zoom=1;Vector2 pan,scroll,lastPointer;bool draggingBoard;string toast="";float toastUntil;int urgentIndex=-1,countdownDeadline;
        AudioSource audioSource;readonly Dictionary<string,AudioClip> sounds=new Dictionary<string,AudioClip>();
        readonly List<FlyingArrow> flying=new List<FlyingArrow>();
        sealed class FlyingArrow{public int id;public float elapsed;}
        readonly HashSet<int> collidedArrows=new HashSet<int>();
        readonly Dictionary<int,BlockedFlight> blockedFlights=new Dictionary<int,BlockedFlight>();
        sealed class BlockedFlight
        {
            public float elapsed,distance;
            public float ImpactTime=>Mathf.Clamp(.14f+distance*.035f,.18f,.48f);
            public float Duration=>ImpactTime+.28f;
            public float Offset=>elapsed<ImpactTime
                ?distance*Mathf.SmoothStep(0,1,elapsed/ImpactTime)
                :distance*(1-Mathf.SmoothStep(0,1,(elapsed-ImpactTime)/.28f));
        }
        int screenshotFrames;string capturePath;bool captureStarted;
        bool hintAdPending;int hintRequestVersion,winHintReward;
        HintRewardedAds hintAds;
        readonly LevelResultTransition resultTransition=new LevelResultTransition();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap(){if(FindFirstObjectByType<GameApp>()==null)new GameObject("Draw Arrow").AddComponent<GameApp>();}
        void Awake()
        {
            Application.targetFrameRate=60;Screen.sleepTimeout=SleepTimeout.NeverSleep;Progress=SaveStore.Load();
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.spatialBlend=0;
            Ink.Clicked=PlayUiClick;
            var camera = Camera.main;if(!camera){camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";}if(FindFirstObjectByType<AudioListener>()==null)camera.gameObject.AddComponent<AudioListener>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Ink.Paper;
            OpenInitialPage();
            var args=Environment.GetCommandLineArgs();for(int i=0;i<args.Length;i++)
            {
                if(args[i]=="--mode"&&i+1<args.Length&&Enum.TryParse(args[i+1],out GameMode m))StartGame(m);
                if(args[i]=="--capture"&&i+1<args.Length)capturePath=args[i+1];
                if(args[i]=="--art-theme"&&i+1<args.Length&&int.TryParse(args[i+1],out int theme))CircuitStyle.Theme=theme;
                if(args[i]=="--smoke-dir"&&i+1<args.Length)StartCoroutine(SmokeAll(args[i+1]));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if(args[i]=="--interaction-smoke-dir"&&i+1<args.Length)StartCoroutine(InteractionSmoke(args[i+1]));
#endif
            }
        }
        void OpenInitialPage()
        {
            page=Page.Home;
            if(Progress.hasStartedGame)return;
            StartGame(GameMode.Classic,0);
            Progress.hasStartedGame=true;SaveStore.Save(Progress);
        }
        void Update()
        {
            if(resultTransition.Take(out var nextMode,out int nextIndex))
            {
                if(nextMode==GameMode.Classic&&SelectedFiniteSet&&nextIndex>=ActiveMainLevelCount)
                    ReturnHome();
                else StartGame(nextMode,nextIndex);
                return;
            }
            if(page==Page.Game&&CanInteract)arrowPress.Tick(Time.unscaledTime);
            if(page==Page.Game&&!paused&&!modalHelp&&!LevelToolsOpen)
                foreach(var arrow in flying)arrow.elapsed+=Time.unscaledDeltaTime;
            flying.RemoveAll(a=>a.elapsed>=FlightDuration(a.id));
            if(page==Page.Game&&!paused&&!modalHelp&&!LevelToolsOpen&&blockedFlights.Count>0)
            {
                var finished=new List<int>();
                foreach(var pair in blockedFlights)
                {
                    pair.Value.elapsed+=Time.unscaledDeltaTime;
                    if(pair.Value.elapsed>=pair.Value.ImpactTime)collidedArrows.Add(pair.Key);
                    if(pair.Value.elapsed>=pair.Value.Duration)finished.Add(pair.Key);
                }
                foreach(int id in finished)blockedFlights.Remove(id);
            }
            if(!Progress.sound&&audioSource.isPlaying)audioSource.Stop();
            if(page==Page.Game&&!paused&&!won&&!failed&&!LevelToolsOpen)
            {
                elapsed+=Time.unscaledDeltaTime;
                if(Modes.IsTimed(Mode))
                {
                    timeLeft-=Time.unscaledDeltaTime;
                    if(Mode==GameMode.Battle){botProgress=Mathf.Clamp01(elapsed/Mathf.Max(30,Arrows.Active.Length*2.8f));if(botProgress>=1)Lose("The local opponent finished first.");}
                    if(timeLeft<=0)Lose(Mode==GameMode.SharkRush?"The shark caught up. Try again!":"Time is up. Try a different route.");
                }
            }
            if(!string.IsNullOrEmpty(capturePath)&&!captureStarted&&++screenshotFrames>35){captureStarted=true;StartCoroutine(Capture());}
            if(Input.GetKeyDown(KeyCode.Escape)){if(LevelToolsOpen)CloseLevelTestTools();else ReturnHome();}
        }
        void ReturnHome()
        {
            resultTransition.Clear();
            hintRequestVersion++;
            CloseLevelTestTools();
            CancelArrowPress();paused=won=failed=modalHelp=false;
            page=Page.Home;scroll=Vector2.zero;SaveStore.Save(Progress);
        }
        IEnumerator Capture(){yield return null;ScreenCapture.CaptureScreenshot(capturePath);yield return new WaitForSeconds(1);Application.Quit();}
        IEnumerator SmokeAll(string directory)
        {
            Directory.CreateDirectory(directory);yield return null;
            page=Page.Home;yield return new WaitForSecondsRealtime(1.6f);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"00-Home.png"));yield return null;
            int checkedModes=0;
            foreach(GameMode mode in Enum.GetValues(typeof(GameMode)))
            {
                StartGame(mode,0);for(int f=0;f<4;f++)yield return null;
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,((int)mode+1).ToString("00")+"-"+mode+".png"));yield return null;
                if(Arrows!=null){int i=Arrows.Hint();if(i>=0){int before=Arrows.Remaining;TapArrow(i);if(Arrows.Remaining!=before-1)throw new Exception("Smoke removal "+mode);Undo();if(Arrows.Remaining!=before)throw new Exception("Smoke undo "+mode);}}
                if(OneLine!=null){foreach(int n in Level.solution)if(!OneLine.Visit(n))throw new Exception("Smoke Euler path");}
                if(Schulte!=null){for(int n=1;n<=Schulte.Numbers.Length;n++)if(!Schulte.Tap(Array.IndexOf(Schulte.Numbers,n)))throw new Exception("Smoke Schulte");}
                checkedModes++;Debug.Log("DRAW_ARROW_MODE_SMOKE "+mode);
            }
            page=Page.Collection;for(int part=0;part<5;part++){scroll=new Vector2(0,part*1320);for(int f=0;f<3;f++)yield return null;ScreenCapture.CaptureScreenshot(Path.Combine(directory,"Games-"+part+".png"));yield return null;}
            foreach(int theme in new[]{0,1,2})
            {
                CircuitStyle.Theme=theme;page=Page.Home;yield return new WaitForSecondsRealtime(1.6f);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"Theme-"+theme+"-Home.png"));yield return null;
                page=Page.Settings;for(int f=0;f<3;f++)yield return null;ScreenCapture.CaptureScreenshot(Path.Combine(directory,"Theme-"+theme+"-Settings.png"));yield return null;
                StartGame(GameMode.Classic,0);paused=true;yield return new WaitForSecondsRealtime(1.6f);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"Theme-"+theme+"-Pause.png"));yield return null;paused=false;
            }
            CircuitStyle.Theme=0;StartGame(GameMode.Classic,0);paused=true;yield return new WaitForSecondsRealtime(1.6f);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"Modal-Pause.png"));yield return null;
            paused=false;foreach(int i in Arrows.Solve())Arrows.Remove(i);won=true;yield return new WaitForSecondsRealtime(1.6f);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"Modal-Win.png"));yield return null;
            won=false;failed=true;toast="No hearts left.";yield return new WaitForSecondsRealtime(1.6f);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"Modal-Fail.png"));yield return null;
            File.WriteAllText(Path.Combine(directory,"smoke.json"),"{\"passed\":true,\"modes\":"+checkedModes+",\"artThemes\":3}");yield return new WaitForSeconds(1);Application.Quit();
        }
        public void StartGame(GameMode mode,int index=-1)
        {
            if(mode==GameMode.Daily&&!FeatureUnlocks.DailyUnlocked(Progress))
            {Notify("Unlock at level "+FeatureUnlocks.DailyLevel);return;}
            CloseLevelTestTools();
            hintRequestVersion++;winHintReward=0;
            resultTransition.Clear();
            CancelArrowPress();
            Mode=mode;LevelIndex=index<0?(mode==GameMode.Classic?ActiveMainLevel:Progress.levels[(int)mode]):index;
            if(mode==GameMode.Classic&&SelectedFiniteSet)LevelIndex=Mathf.Clamp(LevelIndex,0,ActiveMainLevelCount-1);
            if(mode==GameMode.Daily){int.TryParse(DateTime.Now.ToString("yyyyMMdd"),out int day);LevelIndex=day%CircuitLevels.CampaignCount;}
            Arrows=null;Octa=null;OneLine=null;Links=null;Schulte=null;history.Clear();flying.Clear();blockedFlights.Clear();collidedArrows.Clear();motionPaths.Clear();tray.Colors.Clear();
            won=failed=paused=false;hearts=mode==GameMode.OneLife?1:3;moves=0;elapsed=0;timeLeft=mode==GameMode.SharkRush?22:mode==GameMode.Battle?150:90;botProgress=0;hint=-1;zoom=1;pan=Vector2.zero;
            if(mode==GameMode.Schulte){Schulte=new SchulteBoard(3+Mathf.Min(LevelIndex/3,3),137+LevelIndex);Level=null;}
            else
            {
                Level=CircuitLevels.Load(mode,LevelIndex,Progress.SelectedLevelSet);
                if(mode==GameMode.OctaArrows)Octa=new OctaBoard(Level);
                else if(mode==GameMode.LinkDots)Links=new LinkBoard(Level);
                else if(mode==GameMode.OneLine)OneLine=new OneLineBoard(Level);
                else
                {
                    Arrows=new ArrowBoard(Level,mode==GameMode.Arrows3D,fitInitialGaps:true);
                }
                if(mode==GameMode.Arrows3D){var view=Level.initialView;yaw=view!=null&&view.Length>=3?view[1]:-45;pitch=view!=null&&view.Length>=3?-view[0]:18;roll=view!=null&&view.Length>=3?view[2]:18;}
            }
            urgentIndex=Arrows!=null?Arrows.Hint():-1;countdownDeadline=8;page=Page.Game;scroll=Vector2.zero;
        }
        void TapArrow(int i)
        {
            if(paused||won||failed)return;
            if(i<0||i>=Arrows.Active.Length||!Arrows.Active[i]||blockedFlights.ContainsKey(i))return;
            Play("arrow_click",.48f);
            if(Arrows.CanExit(i))
            {
                history.Push(new UndoState{arrow=i,colors=new List<int>(tray.Colors),hearts=hearts,moves=moves,urgent=urgentIndex,deadline=countdownDeadline});
                Arrows.Remove(i);moves++;hint=-1;flying.Add(new FlyingArrow{id=i,elapsed=0});Play("arrow_out_soft",.55f);
                if(Mode==GameMode.ArrowClash){if(tray.Add(Level.arrows[i].color))Play("arrow_collect");if(tray.Full){Lose("The seven slots are full. Match three of a color.");return;}}
                if(Mode==GameMode.SharkRush)timeLeft=Mathf.Min(35,timeLeft+2.5f);
                if(Mode==GameMode.Countdown&&i==urgentIndex){urgentIndex=Arrows.Hint();countdownDeadline=moves+8;}
                if(Mode==GameMode.Countdown&&moves>=countdownDeadline)
                {
                    int urgent=UrgentArrow();if(urgent>=0&&Arrows.Active[urgent]){Lose("The marked arrow ran out of moves.");return;}
                }
                if(Arrows.Remaining==0)
                {
                    if(Mode==GameMode.ArrowClash&&tray.Colors.Count>0){Lose("No arrows remain, but the tray still has unmatched colors.");return;}
                    Win();
                }
            }
            else
            {
                if(!Arrows.Cube)blockedFlights[i]=new BlockedFlight{distance=Arrows.BlockedTravel(i)};
                hearts--;Play("arrow_back");Notify("This arrow is blocked. Clear its path first.");Vibrate();if(hearts<=0)Lose("No hearts left. Every puzzle has another way.");
            }
        }
        int UrgentArrow()=>urgentIndex;
        void Win()
        {
            if(won)return;won=true;Progress.wins++;Progress.streak++;Progress.bestStreak=Mathf.Max(Progress.streak,Progress.bestStreak);Progress.coins+=50+hearts*10;
            resultTransition.Begin(Mode,LevelIndex,true);
            winHintReward=HintEconomy.RewardWin(Progress,Mode,LevelIndex+1);
            if(Mode==GameMode.Daily)
            {
                string today=DateTime.Now.ToString("yyyy-MM-dd");if(!Progress.dailyWins.Contains(today))Progress.dailyWins.Add(today);Progress.lastDaily=today;
            }
            else if(Mode==GameMode.Classic)
            {
                Progress.SelectedClassicIndex=Mathf.Max(Progress.SelectedClassicIndex,LevelIndex+1);
                FeatureUnlocks.RecordMainWin(Progress,LevelIndex+1);
            }
            else Progress.levels[(int)Mode]=Mathf.Max(Progress.levels[(int)Mode],LevelIndex+1);
            SaveStore.Save(Progress);Play("result_pass",.65f);
        }
        void Lose(string reason){if(failed||won)return;failed=true;resultTransition.Begin(Mode,LevelIndex,false);toast=reason;Progress.streak=0;SaveStore.Save(Progress);Play("result_fail",.6f);}
        void Notify(string text){toast=text;toastUntil=Time.unscaledTime+3;}
        void Undo()
        {
            if(OneLine!=null){OneLine.Undo();hint=-1;return;}
            if(history.Count==0){Notify("No move to undo.");return;}
            hint=-1;var state=history.Pop();Arrows.Restore(state.arrow);tray.Colors.Clear();tray.Colors.AddRange(state.colors);hearts=state.hearts;moves=state.moves;urgentIndex=state.urgent;countdownDeadline=state.deadline;failed=false;flying.Clear();blockedFlights.Clear();Play("arrow_tap");
        }
        void Hint()
        {
            if(!CanInteract)return;
            if(Links!=null){Notify("Connect two matching dots. Draw around the other paths.");return;}
            int found=FindHint();
            if(found<0){Notify("No simple hint on this board. Undo a move or restart.");return;}
            // Repeated taps on the currently highlighted target do not spend again.
            if(found==hint&&Time.unscaledTime<hintUntil)return;
            if(HintEconomy.SpendFree(Progress,Mode,LevelIndex)){ApplyHint(found);return;}
            if(!hintAds)hintAds=gameObject.AddComponent<HintRewardedAds>();
            int request=++hintRequestVersion;
            hintAdPending=true;paused=true;CancelArrowPress();
            hintAds.Show(rewarded=>
            {
                if(!this)return;
                hintAdPending=false;
                if(request!=hintRequestVersion)
                {if(rewarded){Progress.freeHints++;SaveStore.Save(Progress);}return;}
                paused=false;
                if(!rewarded){Notify("Ad not completed or unavailable. Try again.");return;}
                int target=FindHint();
                if(target<0||won||failed||page!=Page.Game)
                {Progress.freeHints++;SaveStore.Save(Progress);return;}
                ApplyHint(target);
            });
        }
        int FindHint()
        {
            if(Arrows!=null)
            {for(int i=0;i<Arrows.Active.Length;i++)if(!blockedFlights.ContainsKey(i)&&Arrows.CanExit(i))return i;return -1;}
            if(Octa!=null)return Octa.Hint();
            if(OneLine!=null)return OneLine.NextHint();
            if(Schulte!=null)return Array.IndexOf(Schulte.Numbers,Schulte.Next);
            return -1;
        }
        void ApplyHint(int target)
        {Progress.hintsUsed++;SaveStore.Save(Progress);hint=target;hintUntil=Time.unscaledTime+4;Play("arrow_tap");}
        void PlayUiClick()=>Play("common_click_soft",.42f);
        void OnDestroy(){if(Ink.Clicked==PlayUiClick)Ink.Clicked=null;}
        void Play(string name,float volume=.55f)
        {
            if(!Progress.sound)return;if(!sounds.TryGetValue(name,out var clip)){clip=CircuitAudio.Create(name);sounds[name]=clip;}if(clip)audioSource.PlayOneShot(clip,volume);
        }
        void Vibrate()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if(Progress.vibration)Handheld.Vibrate();
#endif
        }
        void OnApplicationPause(bool value){if(value){CancelArrowPress();if(page==Page.Game&&!won&&!failed)paused=true;}if(Progress!=null)SaveStore.Save(Progress);}
        void OnApplicationQuit(){if(Progress!=null)SaveStore.Save(Progress);}
    }
}
