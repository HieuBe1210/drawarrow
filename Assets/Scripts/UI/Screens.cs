using System;
using UnityEngine;

namespace DrawArrow
{
    public sealed partial class GameApp
    {
        Rect gameViewport;float scale;Vector2 screenOffset;float carousel;bool carouselDragging,listDragging;float carouselStart;Vector2 listDragStart;float artEntered;Page lastArtPage;int modalArtState;float modalArtEntered;
        readonly GameMode[] homeModes={GameMode.Daily,GameMode.Arrows3D,GameMode.ArrowClash,GameMode.OctaArrows,GameMode.SharkRush};
        Color FG=>OriginalArt.Foreground;
        string HomeLayout=>OriginalArt.Theme==1?"HomeV2UI":OriginalArt.Theme==2?"HomeStyle3UI":"HomeUI";
        void OnGUI()
        {
            Ink.Init();OriginalArt.Load();GUI.depth=0;scale=Mathf.Min(Screen.width/Width,Screen.height/Height);screenOffset=new Vector2((Screen.width-Width*scale)*.5f,(Screen.height-Height*scale)*.5f);
            if(page==Page.Game)
            {
                var safe=Screen.safeArea;scale=Mathf.Min(safe.width/Width,safe.height/Height);
                screenOffset=new Vector2(safe.x+(safe.width-Width*scale)*.5f,Screen.height-safe.yMax+(safe.height-Height*scale)*.5f);
                gameViewport=BoardLayout.Viewport(safe,Screen.height,screenOffset,scale);
            }
            if(page!=lastArtPage){lastArtPage=page;artEntered=Time.unscaledTime;}
            GUI.matrix=Matrix4x4.TRS(screenOffset,Quaternion.identity,new Vector3(scale,scale,1));Ink.Rect(page==Page.Game?gameViewport:new Rect(0,0,Width,Height),OriginalArt.Background);
            if(page==Page.Game)DrawGame();else if(page==Page.Home)DrawHome();else
            {DrawHeader();if(page==Page.Journey)DrawJourney();if(page==Page.Collection)DrawCollection();if(page==Page.Settings)DrawSettings();}
            if(Time.unscaledTime<toastUntil&&!failed){OriginalArt.Draw("Toast_bg",new Rect(105,1520,870,130),Ink.WithAlpha(Color.black,.85f),false,true);Ink.TextAt(new Rect(135,1530,810,110),toast,31,Color.white,TextAnchor.MiddleCenter);}
            if(modalHelp)DrawHelp();
        }
        void DrawHeader()
        {
            if(OriginalArt.IconButton(new Rect(52,64,92,92),"CommonIconBack")){page=Page.Home;scroll=Vector2.zero;}
            Ink.TextAt(new Rect(180,58,720,100),page==Page.Collection?"Games":page==Page.Journey?"Level Map":"Settings",56,FG,TextAnchor.MiddleCenter,true);
        }
        void DrawHome()
        {
            string layout=HomeLayout;
            var binding=new ArtBinding
            {
                Clip=OriginalArt.Theme==2?"ea_HomeUI_Style3":"ea_HomeUI",ClipTime=Time.unscaledTime-artEntered,
                Visible=(path,active)=>
                {
                    if(path.Contains("/TopBanner")||path.Contains("/Effect/")||path.EndsWith("/Effect")||path.Contains("/NoNetwork")||path.Contains("/NotNetWork"))return false;
                    if(path.Contains("/Card/CardView/"))return false;
                    if(path.Contains("/Button/TarotBtn")||path.Contains("/Button/ContinueBtn")||path.Contains("/Button/RitualisticBtn")||path.Contains("/Button/OneLifeBtn")||path.Contains("/Arrow3DNode"))return false;
                    if(path.Contains("/TopLeft/BattleItem")||path.Contains("/TopLeft/FeedBackBtn")||path.Contains("/TopLeft/WinStreakItem")||path.Contains("/PlayerInfoHomeItem"))return false;
                    if(path.Contains("/GoalMapItem/")&&(path.Contains("/Zoo")||path.Contains("/TopicChess")||path.Contains("/Tarot")))return false;
                    if(path.Contains("/GoalMapItem/")&&path.EndsWith("/Normal"))return true;
                    if(path.EndsWith("/Container/Level0"))return true;
                    if(path.Contains("/PlayBtnBGFor3D")||path.Contains("/DoubleBtnItem")||path.Contains("/GalleryNewBadge"))return false;
                    if(path.Contains("/PlayBtn/Zoo")||path.Contains("/PlayBtn/WinStreakBtn"))return false;
                    if(path.EndsWith("/Content/Bottom/Button/PlayBtn")||path.EndsWith("/Content/ContentBg/Bottom/Button/PlayBtn"))return true;
                    return active;
                },
                Color=(path,color)=>OriginalArt.Theme==1&&path.EndsWith("/PlayBtn/PlayBtnBG")?Ink.Hex("315CE8"):color,
                Text=(path,text)=>
                {
                    if(path.EndsWith("/LogoText"))return "Amaze GO!";
                    if(path.Contains("/GalleryBtn/")&&path.EndsWith("Text"))return "More Games";
                    if(path.EndsWith("/PlayText"))return "Play";
                    if(path.EndsWith("/LevelText"))
                    {if(path.Contains("/GoalMap")){int n=0;for(int k=0;k<6;k++)if(path.Contains("/Level"+k+"/"))n=k;return (Progress.levels[0]+n+1).ToString();}return "Level "+(Progress.levels[0]+1);}
                    if(path.Contains("WinstreakText"))return Progress.streak+" wins";
                    if(path.EndsWith("/Tips"))return "";return text;
                }
            };
            OriginalArt.Layout(layout,new Rect(0,0,Width,Height),new Vector2(Width,Height),binding);
            var cardRect=OriginalArt.NodeRect(layout,"/Card/CardView",new Rect(0,0,Width,Height),new Vector2(Width,Height));if(cardRect.width<100||cardRect.height<100)cardRect=new Rect(40,278,1000,600);DrawCarousel(cardRect);
            var play=OriginalArt.NodeRect(layout,"/Button/PlayBtn",new Rect(0,0,Width,Height),new Vector2(Width,Height));if(play.width<100)play=new Rect(140,1330,800,174);if(Ink.HitButton(play,GUIContent.none,GUIStyle.none))StartGame(GameMode.Classic);
            var gallery=OriginalArt.NodeRect(layout,"/GalleryButton/GalleryBtn",new Rect(0,0,Width,Height),new Vector2(Width,Height));
            if(gallery.width>100){if(Ink.HitButton(gallery,GUIContent.none,GUIStyle.none)){page=Page.Collection;scroll=Vector2.zero;}}
            else {var more=new Rect(140,1862,800,50);Ink.TextAt(more,"More Games",30,FG,TextAnchor.MiddleCenter,true);if(Ink.HitButton(more,GUIContent.none,GUIStyle.none)){page=Page.Collection;scroll=Vector2.zero;}}
            var settings=OriginalArt.NodeRect(layout,"/TopLeft/SettingBtn",new Rect(0,0,Width,Height),new Vector2(Width,Height));if(settings.width>0&&Ink.HitButton(settings,GUIContent.none,GUIStyle.none))page=Page.Settings;
        }
        void DrawCarousel(Rect viewport)
        {
            var e=Event.current;if(e.type==EventType.MouseDown&&viewport.Contains(e.mousePosition)){carouselDragging=false;carouselStart=e.mousePosition.x;}
            if(e.type==EventType.MouseDrag&&viewport.Contains(e.mousePosition)){if(Mathf.Abs(e.mousePosition.x-carouselStart)>10)carouselDragging=true;carousel=Mathf.Clamp(carousel-e.delta.x,0,homeModes.Length*484-viewport.width);e.Use();}
            if(e.type==EventType.ScrollWheel&&viewport.Contains(e.mousePosition)){carousel=Mathf.Clamp(carousel+e.delta.y*45,0,homeModes.Length*484-viewport.width);e.Use();}
            GUI.BeginGroup(viewport);for(int i=0;i<homeModes.Length;i++){var rect=new Rect(12+i*484-carousel,5,462,Mathf.Min(viewport.height-10,600));DrawModeCard(homeModes[i],rect);bool pressed=Ink.HitButton(rect,GUIContent.none,GUIStyle.none);if(pressed&&!carouselDragging)StartGame(homeModes[i]);}GUI.EndGroup();
        }
        void DrawModeCard(GameMode mode,Rect r)
        {
            string layout=mode==GameMode.Arrows3D?"HomeArrows3DCard":mode==GameMode.ArrowClash?"HomeArrowClashCard":mode==GameMode.OctaArrows?"HomeOctaCard":mode==GameMode.SharkRush?"HomeSharkRushCard":"HomeDCCard";
            var binding=new ArtBinding
            {
                Visible=(path,active)=>path=="/"+layout?true:path.Contains("CardBg_lock")||path.Contains("/arrow")||path.Contains("NotNetWork")||path.Contains("NotNet")||path.EndsWith("/IconOff")?false:path.EndsWith("/CardBg_unlock")||path.EndsWith("/BottomBtn")?true:active,
                Text=(path,text)=>path.EndsWith("/Title")?mode==GameMode.Daily?"Daily Challenge":"New Mode":path.EndsWith("/SubheadTitle")?mode==GameMode.Daily?DateTime.Now.ToString("MMM dd"):Modes.Name(mode):path.EndsWith("/BtnText")||path.EndsWith("/State")?"Play":path.EndsWith("/TimeText")?"Challenge":text,
            };OriginalArt.Layout(layout,r,new Vector2(492,640),binding);
        }
        void DrawJourney()
        {
            int at=Progress.levels[0],start=Mathf.Max(0,at-3);for(int i=0;i<8;i++){int n=start+i;float y=330+i*185,x=540+Mathf.Sin(i*1.5f)*155;OriginalArt.Draw("goal_icon_bg",new Rect(x-72,y-72,144,144),n<=at?OriginalArt.Accent:Ink.Hex("D8CCB9"));Ink.TextAt(new Rect(x-65,y-65,130,130),n<at?"✓":(n+1).ToString(),49,Color.white,TextAnchor.MiddleCenter,true);if(n<=at&&Ink.HitButton(new Rect(x-75,y-75,150,150),GUIContent.none,GUIStyle.none))StartGame(GameMode.Classic,n);}
        }
        void DrawCollection()
        {
            var viewport=new Rect(30,210,1020,1650);var content=new Rect(0,0,990,10*660);HandleScroll(viewport,content.height);scroll=GUI.BeginScrollView(viewport,scroll,content,false,false,GUIStyle.none,GUIStyle.none);
            for(int i=0;i<20;i++)
            {
                var mode=(GameMode)i;var r=new Rect(12+i%2*494,i/2*660,474,628);bool play=false;
                if(mode==GameMode.Arrows3D||mode==GameMode.ArrowClash||mode==GameMode.OctaArrows||mode==GameMode.SharkRush||mode==GameMode.Daily)DrawModeCard(mode,r);
                else {OriginalArt.Draw("HomeDaiyCardBeigeUnLock",r);Ink.TextAt(new Rect(r.x+25,r.y+30,r.width-50,100),Modes.Name(mode),42,Ink.Hex("6E553C"),TextAnchor.MiddleCenter,true);OriginalArt.Draw(ModeIllustration(mode),new Rect(r.x+50,r.y+156,r.width-100,285),null,true);play=OriginalArt.Button(new Rect(r.x+34,r.y+486,r.width-68,100),"Level "+(Progress.levels[i]+1),OriginalArt.Accent,35);}
                bool pressed=Ink.HitButton(r,GUIContent.none,GUIStyle.none);if((play||pressed)&&!listDragging)StartGame(mode);
            }GUI.EndScrollView();
        }
        string ModeIllustration(GameMode mode)
        {
            switch(mode){case GameMode.OneLife:return "death_home_btn_icon";case GameMode.TimeAttack:return "img_play_mainpage_1";case GameMode.Countdown:return "CommonIconHeart1";case GameMode.CatEscape:return "img_cat_banner";case GameMode.Tarot:return "icon_taluo_logo_2_s";case GameMode.Zodiac:return "constellation_pop_icon_1";case GameMode.Zoo:return "icon_chick";case GameMode.Gallery:return "Livingroom_30_Cat";case GameMode.Battle:return "BattleEntryBg";case GameMode.LinkDots:return "fllow1";case GameMode.OneLine:return "line_play_hand";case GameMode.Schulte:return "SchulteEntryBg_Green";default:return "Settlement_img_arrows_light";}
        }
        void HandleScroll(Rect viewport,float total){var e=Event.current;if(e.type==EventType.MouseDown&&viewport.Contains(e.mousePosition)){listDragging=false;listDragStart=e.mousePosition;}if(e.type==EventType.MouseDrag&&viewport.Contains(e.mousePosition)){if(Vector2.Distance(listDragStart,e.mousePosition)>12)listDragging=true;scroll.y=Mathf.Clamp(scroll.y-e.delta.y,0,Mathf.Max(0,total-viewport.height));}}
        void DrawSettings()
        {
            string layout=OriginalArt.Theme==2?"GameSettingStyle3UI":OriginalArt.Theme==1?"GameSettingV2UI":"GameSettingUI";
            var binding=new ArtBinding
            {
                Visible=(path,active)=>{if(path.Contains("/CloseBg")||path.Contains("/Music")||path.Contains("/GuideLine")||path.Contains("/Voice")||path.Contains("/FoucsMode")||path.Contains("/FeedBack")||path.Contains("/EffectNode")||path.Contains("/DlgBtn")||path.Contains("/BtnOk"))return false;if(path.EndsWith("/TogglePar/Handle"))return false;if(path.Contains("/Sound/TogglePar/On"))return Progress.sound;if(path.Contains("/Sound/TogglePar/Off"))return !Progress.sound;if(path.Contains("/Vibration/TogglePar/On"))return Progress.vibration;if(path.Contains("/Vibration/TogglePar/Off"))return !Progress.vibration;return active;},
                Text=(path,text)=>path.EndsWith("/Title")?"Settings":text
            };
            OriginalArt.Layout(layout,new Rect(0,0,Width,Height),new Vector2(Width,Height),binding);SettingsSwitches(layout,binding);
            float settingsOffset=OriginalArt.Theme==2?-1120:0;
            Ink.TextAt(new Rect(160,1430+settingsOffset,760,70),"Art theme",40,FG,TextAnchor.MiddleCenter,true);string[] skins={"Original","V2","Style3"};for(int i=0;i<3;i++)if(OriginalArt.Button(new Rect(100+i*300,1520+settingsOffset,280,100),skins[i],i==OriginalArt.Theme?OriginalArt.Accent:Ink.Hex("9F9A8E"),33))OriginalArt.Theme=i;
            if(OriginalArt.Button(new Rect(140,1710+settingsOffset,800,120),"How to play",OriginalArt.Accent,42))modalHelp=true;
            var close=OriginalArt.NodeRect(layout,"/Dialog_mid/CloseBtn",new Rect(0,0,Width,Height),new Vector2(Width,Height),binding);if(Ink.HitButton(close,GUIContent.none,GUIStyle.none))page=Page.Home;
        }
        void SettingsSwitches(string layout,ArtBinding binding)
        {
            foreach(string item in new[]{"Sound","Vibration"})
            {
                bool enabled=item=="Sound"?Progress.sound:Progress.vibration;
                var r=OriginalArt.NodeRect(layout,"/AreaBg/"+item,new Rect(0,0,Width,Height),new Vector2(Width,Height),binding);
                var toggle=OriginalArt.NodeRect(layout,"/AreaBg/"+item+"/TogglePar",new Rect(0,0,Width,Height),new Vector2(Width,Height),binding);
                if(toggle.width>0){float knob=Mathf.Min(60,toggle.height-10);OriginalArt.Draw(OriginalArt.Theme==1?"SettingToggleHandleV2":"SettingTogglebtn",new Rect(enabled?toggle.xMax-knob-5:toggle.x+5,toggle.center.y-knob*.5f,knob,knob),Color.white,true);}
                if(r.width>0&&Ink.HitButton(r,GUIContent.none,GUIStyle.none)){if(item=="Sound")Progress.sound=!Progress.sound;else Progress.vibration=!Progress.vibration;SaveStore.Save(Progress);}
            }
        }
        void DrawGame()
        {
            DrawGameBackground();GUI.BeginGroup(new Rect(0,gameViewport.yMin,Width,340));if(OriginalArt.IconButton(new Rect(48,58,94,94),OriginalArt.Theme==2?"back_icon_style3_2":"CommonIconBack"))paused=true;Ink.TextAt(new Rect(180,60,720,80),Mode==GameMode.Daily?DateTime.Now.ToString("MMM dd"):"Level "+(LevelIndex+1),58,FG,TextAnchor.MiddleCenter,true);if(OriginalArt.IconButton(new Rect(940,58,94,94),OriginalArt.Theme==2?"Style3SettingBtnIcon":"CommonIconSet"))paused=true;
            for(int i=0;i<(Mode==GameMode.OneLife?1:3);i++)OriginalArt.Draw("CommonIconHeart1",new Rect(435+i*74,175,64,64),i<hearts?OriginalArt.Accent:Ink.WithAlpha(OriginalArt.Accent,.20f),true);
            if(Modes.IsTimed(Mode)||Schulte!=null||Octa!=null)Ink.TextAt(new Rect(160,249,760,70),Modes.IsTimed(Mode)?Mathf.CeilToInt(timeLeft)+"s":Schulte!=null?"Find "+Schulte.Next:Octa.MovesLeft+" moves",39,FG,TextAnchor.MiddleCenter,true);
            GUI.EndGroup();
            float top=gameViewport.yMin+(Modes.IsTimed(Mode)||Schulte!=null||Octa!=null?330:265);
            float bottom=gameViewport.yMax-(Mode==GameMode.ArrowClash?590:455);
            var boardRect=new Rect(gameViewport.xMin+32,top,gameViewport.width-64,Mathf.Max(100,bottom-top));
            GUI.BeginGroup(boardRect);DrawBoard(new Rect(0,0,boardRect.width,boardRect.height));GUI.EndGroup();
            if(Mode==GameMode.ArrowClash){GUI.BeginGroup(new Rect(0,gameViewport.yMax-Height,Width,Height));DrawTray();GUI.EndGroup();}
            GUI.BeginGroup(new Rect(0,gameViewport.yMax-Height,Width,Height));
            if(Mode==GameMode.Battle){Ink.TextAt(new Rect(78,1470,930,40),"Local opponent",27,FG);OriginalArt.Draw("BarBg",new Rect(78,1520,924,18),Ink.Hex("CCBDA3"),false,true);Ink.Rect(new Rect(78,1520,924*botProgress,18),Ink.Red);}else Ink.TextAt(new Rect(78,1480,924,70),BoardInstruction(),27,FG,TextAnchor.MiddleCenter);
            GUI.enabled=CanInteract;if(OriginalArt.IconButton(new Rect(170,1630,154,154),"CommonIconBack","Undo"))Undo();if(OriginalArt.IconButton(new Rect(463,1630,154,154),OriginalArt.Theme==2?"Style3GameHintBtnIcon":"CommonIconHint","Hint · 15"))Hint();if(OriginalArt.IconButton(new Rect(756,1630,154,154),"restart","Restart"))StartGame(Mode,LevelIndex);
            if(Arrows!=null){if(Ink.HitButton(new Rect(70,1840,190,65),GUIContent.none,GUIStyle.none))zoom=Mathf.Max(.6f,zoom-.25f);Ink.TextAt(new Rect(70,1840,190,65),"−",44,FG,TextAnchor.MiddleCenter);if(Ink.HitButton(new Rect(360,1840,360,65),GUIContent.none,GUIStyle.none)){zoom=1;pan=Vector2.zero;}Ink.TextAt(new Rect(360,1840,360,65),Arrows.Cube?"Rotate / Zoom":"Fit board",28,FG,TextAnchor.MiddleCenter);if(Ink.HitButton(new Rect(820,1840,190,65),GUIContent.none,GUIStyle.none))zoom=Mathf.Min(4,zoom+.25f);Ink.TextAt(new Rect(820,1840,190,65),"+",44,FG,TextAnchor.MiddleCenter);}
            GUI.EndGroup();
            GUI.enabled=true;if(paused||won||failed)DrawModal();
        }
        void DrawGameBackground(){string background=Mode==GameMode.SharkRush?"Sea_BG":Mode==GameMode.ArrowClash?"clash_level_bg_1":Mode==GameMode.Schulte?"green_wallpaper_1":null;if(background!=null)OriginalArt.Draw(background,gameViewport);}
        string BoardStatus()=>Arrows!=null?Arrows.Remaining+" / "+Arrows.TargetCount+" arrows":Octa!=null?Octa.Remaining+" arrows":OneLine!=null?OneLine.Trail.Count+" steps":Links!=null?ConnectedCount()+" / "+Links.Connected.Length+" pairs":Mathf.FloorToInt(elapsed)+"s";
        int ConnectedCount(){int n=0;foreach(bool c in Links.Connected)if(c)n++;return n;}
        string BoardInstruction()=>Arrows!=null?(Arrows.Cube?"Drag to rotate. Tap an arrow to free it.":Mode==GameMode.Countdown?"Clear the marked arrow before its countdown expires.":Mode==GameMode.Reasoning?"Remember the paths. Tap to reveal an arrow.":"Tap arrows to clear the board."):Octa!=null?"Find a clear route through the arrows.":OneLine!=null?"Connect every line in one stroke.":Links!=null?"Connect matching dots. Do not cross lines.":"Find the numbers in order.";
        void DrawTray(){OriginalArt.Draw("CommonAreaBg",new Rect(55,1370,970,120),Ink.Hex("D1C5AF"),false,true);for(int i=0;i<7;i++){var p=new Rect(105+i*132,1387,100,86);OriginalArt.Draw("CommonBtnProps",p,Color.white,false,true);if(i<tray.Colors.Count)OriginalArt.Draw("Arrow_head_black",new Rect(p.x+19,p.y+10,62,62),Ink.Palette[Mathf.Abs(tray.Colors[i])%7],true);}}
        void DrawModal()
        {
            int state=won?1:failed?2:3;if(state!=modalArtState){modalArtState=state;modalArtEntered=Time.unscaledTime;}
            if(paused&&!won&&!failed){DrawPauseArtwork();return;}
            string layout=won?(Mode==GameMode.Arrows3D?"Arrow3DSuccessUI":OriginalArt.Theme==2?"SuccessScoreResultStyle3UI":OriginalArt.Theme==1?"SuccessScoreResultV2UI":"SuccessResultUI"):(OriginalArt.Theme==2?"FailResultStyle3UI":OriginalArt.Theme==1?"FailResultV2UI":"FailResultUI");
            var binding=new ArtBinding
            {
                Clip=won?"SuccessResultUI":null,ClipTime=Time.unscaledTime-modalArtEntered,
                Visible=(path,active)=>
                {
                    if(path.Contains("Ritualistic")||path.Contains("/NextBtnTarot")||path.Contains("/Tarot")||path.Contains("/DailyRankPart")||path.Contains("/RecordPart/OnePart")||path.Contains("/TopAreaBG")||path.Contains("/FailProgressPart")||path.Contains("/FailArrowCountPart")||path.Contains("/SharkRunProgressPart")||path.Contains("/ReviveBtn")||path.Contains("/SkipBtn"))return false;
                    if(path.Contains("/RestartBtnContainer")||path.EndsWith("/Heart")||path.EndsWith("/RecordPart/TwoPart"))return true;return active;
                },
                Text=(path,text)=>
                {
                    if(path.EndsWith("/CompleteText"))return "Level "+(LevelIndex+1)+" Completed!";
                    if(path.EndsWith("/CoastTimeTittleText"))return "Time";if(path.EndsWith("/ComboTittleText"))return "Moves";
                    if(path.EndsWith("/CoastTimeText")||path.EndsWith("/TimeText"))return Mathf.FloorToInt(elapsed/60).ToString("00")+":"+Mathf.FloorToInt(elapsed%60).ToString("00");
                    if(path.EndsWith("/ComboText"))return moves.ToString();if(path.EndsWith("/Slogan"))return toast;return text;
                }
            };
            OriginalArt.Layout(layout,new Rect(0,0,Width,Height),new Vector2(Width,Height),binding);
            var primary=OriginalArt.NodeRect(layout,won?"/Button/NextBtn":"/RestartBtnContainer/RestartBtn",new Rect(0,0,Width,Height),new Vector2(Width,Height),binding);
            if(primary.width<100)primary=new Rect(140,1400,800,150);
            if(Ink.HitButton(primary,GUIContent.none,GUIStyle.none)){if(won)StartGame(Mode,Mode==GameMode.Daily?LevelIndex:LevelIndex+1);else StartGame(Mode,LevelIndex);}
            var home=OriginalArt.NodeRect(layout,"/Button/HomeBtn",new Rect(0,0,Width,Height),new Vector2(Width,Height),binding);
            if(home.width>100){if(Ink.HitButton(home,GUIContent.none,GUIStyle.none)){page=Page.Home;paused=false;scroll=Vector2.zero;}}
            else if(OriginalArt.Button(new Rect(250,1650,580,110),"Home",OriginalArt.Accent,40)){page=Page.Home;paused=false;scroll=Vector2.zero;}
            if(won)DrawCelebration();
        }
        void DrawPauseArtwork()
        {
            string layout=OriginalArt.Theme==2?"GameSettingStyle3UI":OriginalArt.Theme==1?"GameSettingV2UI":"GameSettingUI";
            var binding=new ArtBinding
            {
                Visible=(path,active)=>
                {
                    if(path.Contains("/Music")||path.Contains("/GuideLine")||path.Contains("/Voice")||path.Contains("/FoucsMode")||path.Contains("/FeedBack")||path.Contains("/EffectNode")||path.EndsWith("/TogglePar/Handle"))return false;
                    if(path.Contains("/Sound/TogglePar/On"))return Progress.sound;if(path.Contains("/Sound/TogglePar/Off"))return !Progress.sound;if(path.Contains("/Vibration/TogglePar/On"))return Progress.vibration;if(path.Contains("/Vibration/TogglePar/Off"))return !Progress.vibration;return active;
                }
            };
            OriginalArt.Layout(layout,new Rect(0,0,Width,Height),new Vector2(Width,Height),binding);
            SettingsSwitches(layout,binding);
            var close=OriginalArt.NodeRect(layout,"/Dialog_mid/CloseBtn",new Rect(0,0,Width,Height),new Vector2(Width,Height),binding);if(Ink.HitButton(close,GUIContent.none,GUIStyle.none))paused=false;
            var restart=OriginalArt.NodeRect(layout,OriginalArt.Theme==2?"/BtnOk/ContinueBtn":"/DlgBtn/RestartBtn",new Rect(0,0,Width,Height),new Vector2(Width,Height),binding);if(restart.width>0&&Ink.HitButton(restart,GUIContent.none,GUIStyle.none))StartGame(Mode,LevelIndex);
            if(OriginalArt.Button(new Rect(260,OriginalArt.Theme==2?300:1540,560,110),"Home",OriginalArt.Accent,40)){page=Page.Home;paused=false;}
        }
        void DrawCelebration(){for(int i=0;i<22;i++){float t=(Time.unscaledTime*.28f+i*.073f)%1;float x=95+(i*137%890)+Mathf.Sin(t*9+i)*30;OriginalArt.Draw("RiBtnStar",new Rect(x,-100+t*2000,24+i%4*9,24+i%4*9),Ink.Palette[i%7],true);}}
        void DrawHelp()
        {
            Ink.Rect(new Rect(0,0,Width,Height),new Color(0,0,0,.65f));OriginalArt.Draw("CommonDialogBg",new Rect(85,300,910,1270),Color.white,false,true);Ink.TextAt(new Rect(145,365,790,100),"How to play",64,Ink.Hex("30353C"),TextAnchor.MiddleCenter,true);
            Ink.TextAt(new Rect(165,530,750,760),"ARROWS\nTap an arrow with a clear route to the edge.\n\nARROW CLASH\nCollect three arrows of the same color. Keep the tray from filling up.\n\nLINK DOTS\nConnect matching colors without crossing paths.\n\nONE LINE\nTrace every edge exactly once.\n\n3D\nDrag to rotate the shape. Only the visible surface can be tapped.",34,Ink.Hex("60656E"));if(OriginalArt.Button(new Rect(175,1380,730,120),"Got it",OriginalArt.Accent,45))modalHelp=false;
        }
    }
}
