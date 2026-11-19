using System;
using UnityEngine;

namespace DrawArrow
{
    public sealed partial class GameApp
    {
        Rect gameViewport;float scale;Vector2 screenOffset;bool listDragging;Vector2 listDragStart;

        Color FG=>CircuitStyle.Foreground;

        void OnGUI()
        {
            Ink.Init();GUI.depth=0;
            GUI.matrix=Matrix4x4.identity;Ink.Rect(new Rect(0,0,Screen.width,Screen.height),CircuitStyle.Background);
            var mainCamera=Camera.main;if(mainCamera)mainCamera.backgroundColor=CircuitStyle.Background;
            scale=Mathf.Min(Screen.width/Width,Screen.height/Height);screenOffset=new Vector2((Screen.width-Width*scale)*.5f,(Screen.height-Height*scale)*.5f);
            if(page==Page.Game)
            {
                var safe=Screen.safeArea;scale=Mathf.Min(safe.width/Width,safe.height/Height);
                screenOffset=new Vector2(safe.x+(safe.width-Width*scale)*.5f,Screen.height-safe.yMax+(safe.height-Height*scale)*.5f);
                gameViewport=BoardLayout.Viewport(safe,Screen.height,screenOffset,scale);
            }

            GUI.matrix=Matrix4x4.TRS(screenOffset,Quaternion.identity,new Vector3(scale,scale,1));Ink.Rect(page==Page.Game?gameViewport:new Rect(0,0,Width,Height),CircuitStyle.Background);
            GUI.enabled=!LevelToolsOpen;
            if(page==Page.Game)DrawGame();else if(page==Page.Home)DrawHome();else
            {if(page==Page.Settings)DrawSettings();else{DrawHeader();if(page==Page.Journey)DrawJourney();if(page==Page.Collection)DrawCollection();}}
            if(Time.unscaledTime<toastUntil&&!failed&&!won){CircuitStyle.Draw("Toast_bg",new Rect(105,1520,870,130),Ink.WithAlpha(Color.black,.85f),false,true);Ink.TextAt(new Rect(135,1530,810,110),toast,31,Color.white,TextAnchor.MiddleCenter);}
            if(modalHelp&&!won)DrawHelp();
            GUI.enabled=true;if(page!=Page.Game||!won)DrawLevelTestTools();
        }
        void DrawHeader()
        {
            if(CircuitStyle.IconButton(new Rect(52,64,92,92),"CommonIconBack")){page=Page.Home;scroll=Vector2.zero;}
            Ink.TextAt(new Rect(180,58,720,100),page==Page.Collection?"Games":page==Page.Journey?"Level Map":"Settings",56,FG,TextAnchor.MiddleCenter,true);
        }
        void DrawHome()
        {
            var accent = CircuitStyle.Accent;
            if (Ink.Button(new Rect(814, 72, 218, 82), "Settings", accent, Color.white, 31))
                page = Page.Settings;

            Ink.TextAt(new Rect(70, 260, 940, 148), "Arrow Circuit", 100, FG, TextAnchor.MiddleLeft, true);
            var line = new Vector2(104, 440);
            Ink.Line(line, line + Vector2.right * 200, 10, accent);
            CircuitStyle.ArrowHead(line + Vector2.right * 200, Vector2.right, 33, 10, accent);

            var card = new Rect(70, 630, 940, 660);
            Ink.Card(card, CircuitStyle.Panel, 38);
            Ink.TextAt(new Rect(130, 704, 820, 92), "Level " + (ActiveMainLevel + 1), 60, FG, TextAnchor.MiddleCenter, true);
            var play = new Rect(140, 1045, 800, 145);
            DrawMainLevelSetSelector(play);
            if (CircuitStyle.Button(play, "Play", accent, 54)) StartGame(GameMode.Classic);

            bool unlocked = FeatureUnlocks.MoreGamesUnlocked(Progress);
            if (Ink.Button(new Rect(70, 1400, 450, 128), "Level Map", accent, Color.white, 38))
            { page = Page.Journey; scroll = Vector2.zero; }
            if (Ink.Button(new Rect(560, 1400, 450, 128),
                unlocked ? "More Games" : "Games · Level " + FeatureUnlocks.MoreGamesLevel,
                accent, Color.white, 36)) TryOpenMoreGames();
            if (FeatureUnlocks.DailyUnlocked(Progress) &&
                Ink.Button(new Rect(70, 1560, 940, 116), "Daily Challenge", accent, Color.white, 38))
                StartGame(GameMode.Daily);
            var quickModes = new[] { GameMode.Arrows3D, GameMode.ArrowClash, GameMode.OctaArrows, GameMode.SharkRush };
            for (int i = 0; i < quickModes.Length; i++)
                if (Ink.Button(new Rect(70 + i * 238, 1708, 226, 90), Modes.Name(quickModes[i]),
                    accent, Color.white, 26)) StartGame(quickModes[i]);
        }
        void DrawMainLevelSetSelector(Rect play)
        {
            const float buttonWidth = 210;
            const float gap = 18;
            for (int i = 0; i < 3; i++)
            {
                var levelSet = (ClassicLevelSet)i;
                bool selected = Progress.SelectedLevelSet == levelSet;
                var rect = new Rect(play.center.x - buttonWidth * 1.5f - gap + i * (buttonWidth + gap),
                    play.y - 76, buttonWidth, 60);
                var background = selected ? CircuitStyle.Accent : Color.Lerp(CircuitStyle.Background, FG, .15f);
                if (Ink.Button(rect, "Level Set " + i, background, selected ? Color.white : FG, 30) && !selected)
                {
                    Progress.SelectedLevelSet = levelSet;
                    SaveStore.Save(Progress);
                }
            }
        }
        bool TryOpenMoreGames()
        {
            if(!FeatureUnlocks.MoreGamesUnlocked(Progress)){Notify("Unlock at level "+FeatureUnlocks.MoreGamesLevel);return false;}
            page=Page.Collection;scroll=Vector2.zero;return true;
        }

        void DrawJourney()
        {
            int at=ActiveMainLevel,start=Mathf.Max(0,at-3);for(int i=0;i<8&&(!SelectedFiniteSet||start+i<ActiveMainLevelCount);i++){int n=start+i;float y=330+i*185,x=540+Mathf.Sin(i*1.5f)*155;CircuitStyle.Draw("goal_icon_bg",new Rect(x-72,y-72,144,144),n<=at?CircuitStyle.Accent:Ink.Hex("D8CCB9"));Ink.TextAt(new Rect(x-65,y-65,130,130),n<at?"✓":(n+1).ToString(),49,Color.white,TextAnchor.MiddleCenter,true);if(n<=at&&Ink.HitButton(new Rect(x-75,y-75,150,150),GUIContent.none,GUIStyle.none))StartGame(GameMode.Classic,n);}
        }
        void DrawCollection()
        {
            if (!FeatureUnlocks.MoreGamesUnlocked(Progress)) { page = Page.Home; return; }
            var viewport = new Rect(55, 210, 970, 1610);
            var content = new Rect(0, 0, 940, 10 * 282);
            HandleScroll(viewport, content.height);
            scroll = GUI.BeginScrollView(viewport, scroll, content, false, false, GUIStyle.none, GUIStyle.none);
            for (int i = 0; i < Modes.Names.Length; i++)
            {
                var mode = (GameMode)i;
                var card = new Rect(i % 2 * 470 + 8, i / 2 * 282 + 8, 448, 255);
                Ink.Card(card, CircuitStyle.Panel, 22);
                Ink.TextAt(new Rect(card.x + 20, card.y + 18, 408, 54), Modes.Name(mode), 35, FG, TextAnchor.MiddleLeft, true);
                bool dailyLocked = mode == GameMode.Daily && !FeatureUnlocks.DailyUnlocked(Progress);
                int next = mode == GameMode.Classic ? ActiveMainLevel : Progress.levels[i];
                string label = dailyLocked ? "Unlock at level " + FeatureUnlocks.DailyLevel :
                    mode == GameMode.Daily ? "Play" : "Level " + (next + 1);
                if (CircuitStyle.Button(new Rect(card.x + 20, card.y + 128, 408, 96),
                    label, CircuitStyle.Accent, 30) && !listDragging) StartGame(mode);
            }
            GUI.EndScrollView();
        }
        void HandleScroll(Rect viewport,float total){var e=Event.current;if(e.type==EventType.MouseDown&&viewport.Contains(e.mousePosition)){listDragging=false;listDragStart=e.mousePosition;}if(e.type==EventType.MouseDrag&&viewport.Contains(e.mousePosition)){if(Vector2.Distance(listDragStart,e.mousePosition)>12)listDragging=true;scroll.y=Mathf.Clamp(scroll.y-e.delta.y,0,Mathf.Max(0,total-viewport.height));}}
        void DrawSettings()
        {
            var text=Ink.Hex("555B63");
            Ink.Card(new Rect(64,240,952,1050),Color.white,32);
            CircuitStyle.Draw("CommonIconBack",new Rect(121,307,58,58),text,true);
            if(Ink.HitButton(new Rect(105,290,90,90),GUIContent.none,GUIStyle.none)){ReturnHome();return;}
            Ink.TextAt(new Rect(230,295,620,80),"Settings",54,text,TextAnchor.MiddleCenter,true);
            Ink.Rect(new Rect(120,408,840,2),Ink.Hex("E7EBEF"));
            DrawCompactSettingSwitch("Sound","Sound",new Rect(120,444,840,112));
            DrawCompactSettingSwitch("Vibration","Vibration",new Rect(120,574,840,112));
            Ink.TextAt(new Rect(120,740,840,60),"Art theme",38,text,TextAnchor.MiddleLeft,true);
            string[] names={"Lavender","Mint","Night"};
            for(int i=0;i<3;i++)
            {
                var tile=new Rect(120+i*290,824,260,210);bool selected=CircuitStyle.Theme==i;
                Ink.Card(tile,selected?CircuitStyle.Accent:Ink.Hex("E7EBEF"),20);
                Ink.Card(new Rect(tile.x+3,tile.y+3,tile.width-6,tile.height-6),Color.white,18);
                Ink.Circle(new Vector2(tile.center.x,tile.y+72),38,CircuitStyle.ThemeBackground(i));
                Ink.TextAt(new Rect(tile.x+10,tile.y+132,tile.width-20,55),names[i],34,text,TextAnchor.MiddleCenter,selected);
                if(Ink.HitButton(tile,GUIContent.none,GUIStyle.none))CircuitStyle.Theme=i;
            }
            if(Ink.Button(new Rect(120,1130,840,100),"How to play",CircuitStyle.Accent,Color.white,38))modalHelp=true;
        }
        void DrawCompactSettingSwitch(string item,string label,Rect row)
        {
            bool enabled=item=="Sound"?Progress.sound:Progress.vibration;
            var text=Ink.Hex("555B63");
            Ink.Card(row,Ink.Hex("F3F5F7"),16);
            CircuitStyle.Draw(item=="Sound"?"Sound":"vibrations",new Rect(row.x+25,row.y+23,66,66),text,true);
            Ink.TextAt(new Rect(row.x+118,row.y+20,460,72),label,37,text,TextAnchor.MiddleLeft);
            var track=new Rect(row.xMax-160,row.center.y-35,130,70);
            Ink.Card(track,enabled?Ink.Hex("205BE0"):Ink.Hex("A5A7AA"),35);
            Ink.Circle(new Vector2(enabled?track.xMax-35:track.x+35,track.center.y),28,Color.white);
            if(Ink.HitButton(row,GUIContent.none,GUIStyle.none))ToggleSetting(item);
        }

        void ToggleSetting(string item)
        {
            if(item=="Sound")
            {
                Progress.sound=!Progress.sound;
                if(!Progress.sound&&audioSource)audioSource.Stop();
            }
            else if(item=="Vibration")Progress.vibration=!Progress.vibration;
            else return;
            SaveStore.Save(Progress);
        }
        void DrawGame()
        {
            if(won){DrawModal();return;}
            var mainHintRect=new Rect(940,160,94,94);
            bool showInstructions=Mode!=GameMode.Classic||LevelIndex<10;
            GUI.BeginGroup(new Rect(0,gameViewport.yMin,Width,340));
            if(CircuitStyle.IconButton(new Rect(48,58,94,94),"CommonIconBack"))
            {ReturnHome();GUI.EndGroup();return;}
            Ink.TextAt(new Rect(180,60,720,80),Mode==GameMode.Daily?DateTime.Now.ToString("MMM dd"):"Level "+(LevelIndex+1),58,FG,TextAnchor.MiddleCenter,true);if(CircuitStyle.IconButton(new Rect(940,58,94,94),"CommonIconSet"))paused=true;
            for(int i=0;i<(Mode==GameMode.OneLife?1:3);i++)CircuitStyle.Draw("CommonIconHeart1",new Rect(435+i*74,175,64,64),i<hearts?Color.white:Ink.WithAlpha(Color.white,.20f),true);
            if(Mode==GameMode.Classic)
            {
                GUI.enabled=CanInteract;
                DrawHintButton(mainHintRect);
                GUI.enabled=true;
            }
            if(Modes.IsTimed(Mode)||Schulte!=null||Octa!=null)Ink.TextAt(new Rect(160,249,760,70),Modes.IsTimed(Mode)?Mathf.CeilToInt(timeLeft)+"s":Schulte!=null?"Find "+Schulte.Next:Octa.MovesLeft+" moves",39,FG,TextAnchor.MiddleCenter,true);
            GUI.EndGroup();
            // Main-board bottom: 87.5% of the physical device height. Its
            // footer controls occupy the reserved strip below that boundary.
            float footerBottom=(Screen.height*.875f-screenOffset.y)/scale;
            float footerY=Mathf.Min(gameViewport.yMax-Height,footerBottom-1905);
            float footerLift=gameViewport.yMax-Height-footerY;
            float top=Mode==GameMode.Classic?gameViewport.yMin+mainHintRect.yMax+30/scale
                :gameViewport.yMin+(Modes.IsTimed(Mode)||Schulte!=null||Octa!=null?330:265);
            float bottom=Mode==GameMode.Classic?Mathf.Min(footerBottom,gameViewport.yMax)
                :gameViewport.yMax-footerLift-(Mode==GameMode.ArrowClash?590:455);
            var boardRect=new Rect(gameViewport.xMin+32,top,gameViewport.width-64,Mathf.Max(100,bottom-top));
            GUI.BeginGroup(boardRect);DrawBoard(new Rect(0,0,boardRect.width,boardRect.height));GUI.EndGroup();
            if(won){DrawModal();return;}
            if(Mode==GameMode.ArrowClash){GUI.BeginGroup(new Rect(0,footerY,Width,Height));DrawTray();GUI.EndGroup();}
            GUI.BeginGroup(new Rect(0,footerY,Width,Height));
            if(Mode==GameMode.Battle){Ink.TextAt(new Rect(78,1470,930,40),"Local opponent",27,FG);CircuitStyle.Draw("BarBg",new Rect(78,1520,924,18),Ink.Hex("CCBDA3"),false,true);Ink.Rect(new Rect(78,1520,924*botProgress,18),Ink.Red);}else if(showInstructions)Ink.TextAt(new Rect(78,Mode==GameMode.Classic?footerBottom-footerY+8:1480,924,70),BoardInstruction(),27,FG,TextAnchor.MiddleCenter);
            GUI.enabled=CanInteract;
            if(Mode!=GameMode.Classic)
            {
                if(CircuitStyle.IconButton(new Rect(170,1630,154,154),"CommonIconBack","Undo"))Undo();
                DrawHintButton(new Rect(463,1630,154,154));
                if(CircuitStyle.IconButton(new Rect(756,1630,154,154),"restart","Restart"))StartGame(Mode,LevelIndex);
            }
            float zoomY=Mode==GameMode.Classic?Mathf.Min(footerBottom-footerY+(showInstructions?100:20),gameViewport.yMax-footerY-73):1840;
            if(Arrows!=null){if(Ink.HitButton(new Rect(70,zoomY,190,65),GUIContent.none,GUIStyle.none))zoom=Mathf.Max(.6f,zoom-.25f);Ink.TextAt(new Rect(70,zoomY,190,65),"−",44,FG,TextAnchor.MiddleCenter);if(Ink.HitButton(new Rect(360,zoomY,360,65),GUIContent.none,GUIStyle.none)){zoom=1;pan=Vector2.zero;}Ink.TextAt(new Rect(360,zoomY,360,65),Arrows.Cube?"Rotate / Zoom":"Fit board",28,FG,TextAnchor.MiddleCenter);if(Ink.HitButton(new Rect(820,zoomY,190,65),GUIContent.none,GUIStyle.none))zoom=Mathf.Min(4,zoom+.25f);Ink.TextAt(new Rect(820,zoomY,190,65),"+",44,FG,TextAnchor.MiddleCenter);}
            GUI.EndGroup();
            GUI.enabled=true;
            if(CanInteract&&Arrows!=null&&!Arrows.Cube)DrawHeldArrowRoute(boardRect.position);
            if(hintAdPending)Ink.TextAt(new Rect(180,gameViewport.center.y-45,720,90),"Loading ad…",38,FG,TextAnchor.MiddleCenter,true);
            if(!hintAdPending&&(paused||won||failed))DrawModal();
        }
        string BoardStatus()=>Arrows!=null?Arrows.Remaining+" / "+Arrows.TargetCount+" arrows":Octa!=null?Octa.Remaining+" arrows":OneLine!=null?OneLine.Trail.Count+" steps":Links!=null?ConnectedCount()+" / "+Links.Connected.Length+" pairs":Mathf.FloorToInt(elapsed)+"s";
        int ConnectedCount(){int n=0;foreach(bool c in Links.Connected)if(c)n++;return n;}
        string BoardInstruction()=>Arrows!=null?(Arrows.Cube?"Drag to rotate. Tap an arrow to free it.":Mode==GameMode.Countdown?"Clear the marked arrow before its countdown expires.":Mode==GameMode.Reasoning?"Remember the paths. Tap to reveal an arrow.":"Tap to clear. Hold an arrow to preview its path."):Octa!=null?"Find a clear route through the arrows.":OneLine!=null?"Connect every line in one stroke.":Links!=null?"Connect matching dots. Do not cross lines.":"Find the numbers in order.";
        void DrawTray(){CircuitStyle.Draw("CommonAreaBg",new Rect(55,1370,970,120),Ink.Hex("D1C5AF"),false,true);for(int i=0;i<7;i++){var p=new Rect(105+i*132,1387,100,86);CircuitStyle.Draw("CommonBtnProps",p,Color.white,false,true);if(i<tray.Colors.Count)CircuitStyle.Draw("Arrow_head_black",new Rect(p.x+19,p.y+10,62,62),Ink.Palette[Mathf.Abs(tray.Colors[i])%7],true);}}
        void DrawModal()
        {
            if (won) { DrawWinPopup(); return; }
            if (paused && !failed) { DrawPauseArtwork(); return; }

            Ink.Rect(gameViewport, new Color(0, 0, 0, .55f));
            var panel = new Rect(110, gameViewport.center.y - 320, 860, 640);
            Ink.Card(panel, CircuitStyle.Panel, 32);
            Ink.TextAt(new Rect(panel.x + 45, panel.y + 70, 770, 160), toast, 46, FG, TextAnchor.MiddleCenter, true);
            if (CircuitStyle.Button(new Rect(panel.x + 90, panel.y + 320, 680, 110), "Try again", CircuitStyle.Accent, 42))
                RequestResultContinue();
            if (CircuitStyle.Button(new Rect(panel.x + 90, panel.y + 462, 680, 96), "Home", CircuitStyle.Accent, 36))
                ReturnHome();
        }
        void RequestResultContinue()
        {
            resultTransition.Request();CancelArrowPress();GUIUtility.hotControl=0;
            if(Event.current.type!=EventType.Used)Event.current.Use();
        }
        void DrawWinPopup()
        {
            // Fully cover gameplay, including the frame in which the last arrow
            // was cleared. No board, HUD or gameplay buttons are drawn behind it.
            GUI.enabled=!resultTransition.Pending;
            Ink.Rect(gameViewport,CircuitStyle.Background);
            DrawCelebration();
            float height=winHintReward>0?1380:1220;
            var panel=new Rect(gameViewport.center.x-450,gameViewport.center.y-height*.5f,900,height);
            Ink.Card(new Rect(panel.x,panel.y+12,panel.width,panel.height),Ink.WithAlpha(Color.black,.07f),40);
            Ink.Card(panel,Color.Lerp(CircuitStyle.Background,Color.white,CircuitStyle.Theme==2?.07f:.5f),40);
            int lostHearts=Mathf.Max(0,(Mode==GameMode.OneLife?1:3)-hearts);
            int earnedStars=Mathf.Clamp(3-lostHearts,0,3);
            for(int i=0;i<3;i++)
            {
                float size=i==1?154:106;
                var center=new Vector2(panel.center.x+(i-1)*150,panel.y+(i==1?146:166));
                var starColor=i<earnedStars?Ink.Gold:Ink.WithAlpha(FG,.16f);
                CircuitStyle.Draw("RiBtnStar",new Rect(center-Vector2.one*size*.5f,Vector2.one*size),starColor,true);
            }
            Ink.TextAt(new Rect(panel.x+60,panel.y+268,780,108),"Well Done!",76,FG,TextAnchor.MiddleCenter,true);
            Ink.TextAt(new Rect(panel.x+60,panel.y+380,780,64),"Level "+(LevelIndex+1)+" Completed!",34,FG,TextAnchor.MiddleCenter);
            var stats=new Rect(panel.x+70,panel.y+490,760,170);
            Ink.Card(stats,Ink.WithAlpha(CircuitStyle.Accent,.09f),24);
            Ink.Rect(new Rect(stats.center.x,stats.y+32,2,106),Ink.WithAlpha(CircuitStyle.Accent,.2f));
            Ink.TextAt(new Rect(stats.x,stats.y+25,380,40),"Time",27,FG,TextAnchor.MiddleCenter);
            Ink.TextAt(new Rect(stats.center.x,stats.y+25,380,40),"Moves",27,FG,TextAnchor.MiddleCenter);
            string time=Mathf.FloorToInt(elapsed/60).ToString("00")+":"+Mathf.FloorToInt(elapsed%60).ToString("00");
            Ink.TextAt(new Rect(stats.x,stats.y+70,380,70),time,48,FG,TextAnchor.MiddleCenter,true);
            Ink.TextAt(new Rect(stats.center.x,stats.y+70,380,70),moves.ToString(),48,FG,TextAnchor.MiddleCenter,true);
            if(winHintReward>0)
            {
                var reward=new Rect(panel.x+190,panel.y+710,520,108);
                Ink.Card(reward,Ink.WithAlpha(CircuitStyle.Accent,.12f),24);
                CircuitStyle.Draw("CommonIconHint",new Rect(reward.x+100,reward.y+21,66,66),CircuitStyle.Accent,true);
                Ink.TextAt(new Rect(reward.x+182,reward.y,250,108),"+"+winHintReward+" Hint",42,FG,TextAnchor.MiddleLeft,true);
            }
            float nextY=panel.y+(winHintReward>0?870:710);
            string nextLabel=FinishedLevelSet?"Finish Level Set "+(int)Progress.SelectedLevelSet:"Next Level";
            if(CircuitStyle.Button(new Rect(panel.x+70,nextY,760,280),nextLabel,CircuitStyle.Accent,FinishedLevelSet?58:64))
            {RequestResultContinue();GUI.enabled=true;return;}
            var home=new Rect(panel.x+220,nextY+315,460,90);
            Ink.Card(home,Ink.WithAlpha(CircuitStyle.Accent,.08f),22);
            Ink.TextAt(home,"Home",34,FG,TextAnchor.MiddleCenter,true);
            if(Ink.HitButton(home,GUIContent.none,GUIStyle.none))ReturnHome();
            GUI.enabled=true;
        }
        void DrawHintButton(Rect rect)
        {
            if(CircuitStyle.IconButton(rect,"CommonIconHint"))Hint();
            bool unlimited=HintEconomy.Unlimited(Mode,LevelIndex);
            string text=unlimited?"∞":Progress.freeHints>0?Progress.freeHints.ToString():"AD";
            float size=rect.width*.4f;
            Ink.TextAt(new Rect(rect.xMax-size,rect.yMax-size,size+8,size+8),text,
                Mathf.RoundToInt(rect.width*.33f),Ink.Red,TextAnchor.MiddleCenter,true);
        }
        void DrawPauseArtwork()
        {
            Ink.Rect(gameViewport, new Color(0, 0, 0, .55f));
            var panel = new Rect(70, gameViewport.center.y - 430, 940, 860);
            Ink.Card(panel, CircuitStyle.Panel, 32);
            Ink.TextAt(new Rect(110, panel.y + 48, 860, 90), "Paused", 60, FG, TextAnchor.MiddleCenter, true);
            DrawCompactSettingSwitch("Sound", "Sound", new Rect(120, panel.y + 190, 840, 112));
            DrawCompactSettingSwitch("Vibration", "Vibration", new Rect(120, panel.y + 318, 840, 112));
            if (CircuitStyle.Button(new Rect(120, panel.y + 485, 840, 102), "Continue", CircuitStyle.Accent, 40))
                paused = false;
            if (CircuitStyle.Button(new Rect(120, panel.y + 614, 400, 102), "Restart", CircuitStyle.Accent, 34))
                StartGame(Mode, LevelIndex);
            if (CircuitStyle.Button(new Rect(560, panel.y + 614, 400, 102), "Home", CircuitStyle.Accent, 34))
                ReturnHome();
        }
        void DrawCelebration(){for(int i=0;i<22;i++){float t=(Time.unscaledTime*.28f+i*.073f)%1;float x=95+(i*137%890)+Mathf.Sin(t*9+i)*30;CircuitStyle.Draw("RiBtnStar",new Rect(x,-100+t*2000,24+i%4*9,24+i%4*9),Ink.Palette[i%7],true);}}
        void DrawHelp()
        {
            Ink.Rect(new Rect(0,0,Width,Height),new Color(0,0,0,.65f));CircuitStyle.Draw("CommonDialogBg",new Rect(85,300,910,1270),Color.white,false,true);Ink.TextAt(new Rect(145,365,790,100),"How to play",64,Ink.Hex("30353C"),TextAnchor.MiddleCenter,true);
            Ink.TextAt(new Rect(165,530,750,760),"ARROWS\nTap an arrow with a clear route to the edge.\n\nARROW CLASH\nCollect three arrows of the same color. Keep the tray from filling up.\n\nLINK DOTS\nConnect matching colors without crossing paths.\n\nONE LINE\nTrace every edge exactly once.\n\n3D\nDrag to rotate the shape. Only the visible surface can be tapped.",34,Ink.Hex("60656E"));if(CircuitStyle.Button(new Rect(175,1380,730,120),"Got it",CircuitStyle.Accent,45))modalHelp=false;
        }
    }
}
