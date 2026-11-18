using System;
using UnityEngine;

namespace DrawArrow
{
    public sealed partial class GameApp
    {
        Rect gameViewport;float scale;Vector2 screenOffset;float carousel;bool carouselDragging,listDragging;float carouselStart;Vector2 listDragStart;float artEntered;Page lastArtPage;int modalArtState;float modalArtEntered;
        readonly GameMode[] homeModes={GameMode.Daily,GameMode.Arrows3D,GameMode.ArrowClash,GameMode.OctaArrows,GameMode.SharkRush};
        Color FG=>OriginalArt.Foreground;
        string HomeLayout=>"HomeUI";
        void OnGUI()
        {
            Ink.Init();OriginalArt.Load();GUI.depth=0;
            GUI.matrix=Matrix4x4.identity;Ink.Rect(new Rect(0,0,Screen.width,Screen.height),OriginalArt.Background);
            var mainCamera=Camera.main;if(mainCamera)mainCamera.backgroundColor=OriginalArt.Background;
            scale=Mathf.Min(Screen.width/Width,Screen.height/Height);screenOffset=new Vector2((Screen.width-Width*scale)*.5f,(Screen.height-Height*scale)*.5f);
            if(page==Page.Game)
            {
                var safe=Screen.safeArea;scale=Mathf.Min(safe.width/Width,safe.height/Height);
                screenOffset=new Vector2(safe.x+(safe.width-Width*scale)*.5f,Screen.height-safe.yMax+(safe.height-Height*scale)*.5f);
                gameViewport=BoardLayout.Viewport(safe,Screen.height,screenOffset,scale);
            }
            if(page!=lastArtPage){lastArtPage=page;artEntered=Time.unscaledTime;}
            GUI.matrix=Matrix4x4.TRS(screenOffset,Quaternion.identity,new Vector3(scale,scale,1));Ink.Rect(page==Page.Game?gameViewport:new Rect(0,0,Width,Height),OriginalArt.Background);
            GUI.enabled=!LevelToolsOpen;
            if(page==Page.Game)DrawGame();else if(page==Page.Home)DrawHome();else
            {if(page==Page.Settings)DrawSettings();else{DrawHeader();if(page==Page.Journey)DrawJourney();if(page==Page.Collection)DrawCollection();}}
            if(Time.unscaledTime<toastUntil&&!failed&&!won){OriginalArt.Draw("Toast_bg",new Rect(105,1520,870,130),Ink.WithAlpha(Color.black,.85f),false,true);Ink.TextAt(new Rect(135,1530,810,110),toast,31,Color.white,TextAnchor.MiddleCenter);}
            if(modalHelp&&!won)DrawHelp();
            GUI.enabled=true;if(page!=Page.Game||!won)DrawLevelTestTools();
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
                Clip="ea_HomeUI",ClipTime=Time.unscaledTime-artEntered,
                Visible=(path,active)=>
                {
                    if(path=="/HomeUI/Bg")return false;
                    if(!FeatureUnlocks.MoreGamesUnlocked(Progress)&&path.Contains("/GalleryBtn/"))return false;
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
                Color=(path,color)=>path.EndsWith("/LogoText")||path.Contains("/TopLeft/SettingBtn")?FG:color,
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
            if(gallery.width<=100)gallery=new Rect(140,1740,800,150);
            if(!FeatureUnlocks.MoreGamesUnlocked(Progress))
            {
                Ink.Card(gallery,Ink.Hex("815D3C"),24);
                Ink.TextAt(new Rect(gallery.x+20,gallery.y+15,gallery.width-40,gallery.height*.36f),"More Games",38,Color.white,TextAnchor.MiddleCenter,true);
                Ink.TextAt(new Rect(gallery.x+20,gallery.y+gallery.height*.50f,gallery.width-40,gallery.height*.38f),"Unlock at level "+FeatureUnlocks.MoreGamesLevel,30,Color.white,TextAnchor.MiddleCenter,true);
            }
            if(Ink.HitButton(gallery,GUIContent.none,GUIStyle.none))TryOpenMoreGames();
            var settings=OriginalArt.NodeRect(layout,"/TopLeft/SettingBtn",new Rect(0,0,Width,Height),new Vector2(Width,Height));if(settings.width>0&&Ink.HitButton(settings,GUIContent.none,GUIStyle.none))page=Page.Settings;
        }
        bool TryOpenMoreGames()
        {
            if(!FeatureUnlocks.MoreGamesUnlocked(Progress)){Notify("Unlock at level "+FeatureUnlocks.MoreGamesLevel);return false;}
            page=Page.Collection;scroll=Vector2.zero;return true;
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
            bool locked=mode==GameMode.Daily&&!FeatureUnlocks.DailyUnlocked(Progress);
            string layout=mode==GameMode.Arrows3D?"HomeArrows3DCard":mode==GameMode.ArrowClash?"HomeArrowClashCard":mode==GameMode.OctaArrows?"HomeOctaCard":mode==GameMode.SharkRush?"HomeSharkRushCard":"HomeDCCard";
            var binding=new ArtBinding
            {
                Visible=(path,active)=>locked&&(path.EndsWith("/BtnText")||path.EndsWith("/State"))?false:path=="/"+layout?true:path.Contains("CardBg_lock")||path.Contains("/arrow")||path.Contains("NotNetWork")||path.Contains("NotNet")||path.EndsWith("/IconOff")?false:path.EndsWith("/CardBg_unlock")||path.EndsWith("/BottomBtn")?true:active,
                Text=(path,text)=>path.EndsWith("/Title")?mode==GameMode.Daily?"Daily Challenge":"New Mode":path.EndsWith("/SubheadTitle")?mode==GameMode.Daily?DateTime.Now.ToString("MMM dd"):Modes.Name(mode):path.EndsWith("/BtnText")||path.EndsWith("/State")?"Play":path.EndsWith("/TimeText")?"Challenge":text,
            };OriginalArt.Layout(layout,r,new Vector2(492,640),binding);
            if(locked)
            {
                Ink.Card(r,new Color(.28f,.18f,.10f,.38f),24);
                var button=OriginalArt.NodeRect(layout,"/BottomBtn",r,new Vector2(492,640),binding);
                Ink.Card(button,Ink.Hex("815D3C"),20);
                Ink.TextAt(new Rect(button.x+8,button.y+8,button.width-16,button.height-16),"Unlock at level "+FeatureUnlocks.DailyLevel,31,Color.white,TextAnchor.MiddleCenter,true);
            }
        }
        void DrawJourney()
        {
            int at=Progress.levels[0],start=Mathf.Max(0,at-3);for(int i=0;i<8;i++){int n=start+i;float y=330+i*185,x=540+Mathf.Sin(i*1.5f)*155;OriginalArt.Draw("goal_icon_bg",new Rect(x-72,y-72,144,144),n<=at?OriginalArt.Accent:Ink.Hex("D8CCB9"));Ink.TextAt(new Rect(x-65,y-65,130,130),n<at?"✓":(n+1).ToString(),49,Color.white,TextAnchor.MiddleCenter,true);if(n<=at&&Ink.HitButton(new Rect(x-75,y-75,150,150),GUIContent.none,GUIStyle.none))StartGame(GameMode.Classic,n);}
        }
        void DrawCollection()
        {
            if(!FeatureUnlocks.MoreGamesUnlocked(Progress)){page=Page.Home;return;}
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
            var text=Ink.Hex("555B63");
            Ink.Card(new Rect(64,240,952,1050),Color.white,32);
            OriginalArt.Draw("CommonIconBack",new Rect(121,307,58,58),text,true);
            if(Ink.HitButton(new Rect(105,290,90,90),GUIContent.none,GUIStyle.none)){ReturnHome();return;}
            Ink.TextAt(new Rect(230,295,620,80),"Settings",54,text,TextAnchor.MiddleCenter,true);
            Ink.Rect(new Rect(120,408,840,2),Ink.Hex("E7EBEF"));
            DrawCompactSettingSwitch("Sound","Sound",new Rect(120,444,840,112));
            DrawCompactSettingSwitch("Vibration","Vibration",new Rect(120,574,840,112));
            Ink.TextAt(new Rect(120,740,840,60),"Art theme",38,text,TextAnchor.MiddleLeft,true);
            string[] names={"Original","V2","V3"};
            for(int i=0;i<3;i++)
            {
                var tile=new Rect(120+i*290,824,260,210);bool selected=OriginalArt.Theme==i;
                Ink.Card(tile,selected?OriginalArt.Accent:Ink.Hex("E7EBEF"),20);
                Ink.Card(new Rect(tile.x+3,tile.y+3,tile.width-6,tile.height-6),Color.white,18);
                Ink.Circle(new Vector2(tile.center.x,tile.y+72),38,OriginalArt.ThemeBackground(i));
                Ink.TextAt(new Rect(tile.x+10,tile.y+132,tile.width-20,55),names[i],34,text,TextAnchor.MiddleCenter,selected);
                if(Ink.HitButton(tile,GUIContent.none,GUIStyle.none))OriginalArt.Theme=i;
            }
            if(Ink.Button(new Rect(120,1130,840,100),"How to play",OriginalArt.Accent,Color.white,38))modalHelp=true;
        }
        void DrawCompactSettingSwitch(string item,string label,Rect row)
        {
            bool enabled=item=="Sound"?Progress.sound:Progress.vibration;
            var text=Ink.Hex("555B63");
            Ink.Card(row,Ink.Hex("F3F5F7"),16);
            OriginalArt.Draw(item=="Sound"?"Sound":"vibrations",new Rect(row.x+25,row.y+23,66,66),text,true);
            Ink.TextAt(new Rect(row.x+118,row.y+20,460,72),label,37,text,TextAnchor.MiddleLeft);
            var track=new Rect(row.xMax-160,row.center.y-35,130,70);
            Ink.Card(track,enabled?Ink.Hex("205BE0"):Ink.Hex("A5A7AA"),35);
            Ink.Circle(new Vector2(enabled?track.xMax-35:track.x+35,track.center.y),28,Color.white);
            if(Ink.HitButton(row,GUIContent.none,GUIStyle.none))ToggleSetting(item);
        }
        void SettingsSwitches(string layout,ArtBinding binding)
        {
            foreach(string item in new[]{"Sound","Vibration"})
            {
                bool enabled=item=="Sound"?Progress.sound:Progress.vibration;
                var r=SettingsToggleBounds(layout,item,binding);
                var toggle=OriginalArt.NodeRect(layout,"/AreaBg/"+item+"/TogglePar",new Rect(0,0,Width,Height),new Vector2(Width,Height),binding);
                if(toggle.width>0){float knob=Mathf.Min(60,toggle.height-10);OriginalArt.Draw("SettingTogglebtn",new Rect(enabled?toggle.xMax-knob-5:toggle.x+5,toggle.center.y-knob*.5f,knob,knob),Color.white,true);}
                if(r.width>0&&Ink.HitButton(r,GUIContent.none,GUIStyle.none))ToggleSetting(item);
            }
        }
        static Rect SettingsToggleBounds(string layout,string item,ArtBinding binding)
        {
            Rect result=Rect.zero;
            // The prefab's 110-wide parent bounds cover the icon only. Its
            // switch, label and original button are positioned outside it.
            foreach(string suffix in new[]{"","/TogglePar","/"+item+"Btn","/Icon","/Text"})
            {
                var rect=OriginalArt.NodeRect(layout,"/AreaBg/"+item+suffix,new Rect(0,0,Width,Height),new Vector2(Width,Height),binding);
                if(rect.width<=0||rect.height<=0)continue;
                result=result.width<=0?rect:Rect.MinMaxRect(Mathf.Min(result.xMin,rect.xMin),Mathf.Min(result.yMin,rect.yMin),Mathf.Max(result.xMax,rect.xMax),Mathf.Max(result.yMax,rect.yMax));
            }
            return result;
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
            DrawGameBackground();GUI.BeginGroup(new Rect(0,gameViewport.yMin,Width,340));
            if(OriginalArt.IconButton(new Rect(48,58,94,94),"CommonIconBack"))
            {ReturnHome();GUI.EndGroup();return;}
            Ink.TextAt(new Rect(180,60,720,80),Mode==GameMode.Daily?DateTime.Now.ToString("MMM dd"):"Level "+(LevelIndex+1),58,FG,TextAnchor.MiddleCenter,true);if(OriginalArt.IconButton(new Rect(940,58,94,94),"CommonIconSet"))paused=true;
            for(int i=0;i<(Mode==GameMode.OneLife?1:3);i++)OriginalArt.Draw("CommonIconHeart1",new Rect(435+i*74,175,64,64),i<hearts?Color.white:Ink.WithAlpha(Color.white,.20f),true);
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
            if(Mode==GameMode.Battle){Ink.TextAt(new Rect(78,1470,930,40),"Local opponent",27,FG);OriginalArt.Draw("BarBg",new Rect(78,1520,924,18),Ink.Hex("CCBDA3"),false,true);Ink.Rect(new Rect(78,1520,924*botProgress,18),Ink.Red);}else if(showInstructions)Ink.TextAt(new Rect(78,Mode==GameMode.Classic?footerBottom-footerY+8:1480,924,70),BoardInstruction(),27,FG,TextAnchor.MiddleCenter);
            GUI.enabled=CanInteract;
            if(Mode!=GameMode.Classic)
            {
                if(OriginalArt.IconButton(new Rect(170,1630,154,154),"CommonIconBack","Undo"))Undo();
                DrawHintButton(new Rect(463,1630,154,154));
                if(OriginalArt.IconButton(new Rect(756,1630,154,154),"restart","Restart"))StartGame(Mode,LevelIndex);
            }
            float zoomY=Mode==GameMode.Classic?Mathf.Min(footerBottom-footerY+(showInstructions?100:20),gameViewport.yMax-footerY-73):1840;
            if(Arrows!=null){if(Ink.HitButton(new Rect(70,zoomY,190,65),GUIContent.none,GUIStyle.none))zoom=Mathf.Max(.6f,zoom-.25f);Ink.TextAt(new Rect(70,zoomY,190,65),"−",44,FG,TextAnchor.MiddleCenter);if(Ink.HitButton(new Rect(360,zoomY,360,65),GUIContent.none,GUIStyle.none)){zoom=1;pan=Vector2.zero;}Ink.TextAt(new Rect(360,zoomY,360,65),Arrows.Cube?"Rotate / Zoom":"Fit board",28,FG,TextAnchor.MiddleCenter);if(Ink.HitButton(new Rect(820,zoomY,190,65),GUIContent.none,GUIStyle.none))zoom=Mathf.Min(4,zoom+.25f);Ink.TextAt(new Rect(820,zoomY,190,65),"+",44,FG,TextAnchor.MiddleCenter);}
            GUI.EndGroup();
            GUI.enabled=true;
            if(CanInteract&&Arrows!=null&&!Arrows.Cube)DrawHeldArrowRoute(boardRect.position);
            if(hintAdPending)Ink.TextAt(new Rect(180,gameViewport.center.y-45,720,90),"Loading ad…",38,FG,TextAnchor.MiddleCenter,true);
            if(!hintAdPending&&(paused||won||failed))DrawModal();
        }
        void DrawGameBackground(){string background=Mode==GameMode.SharkRush?"Sea_BG":Mode==GameMode.ArrowClash?"clash_level_bg_1":Mode==GameMode.Schulte?"green_wallpaper_1":null;if(background!=null)OriginalArt.Draw(background,gameViewport);}
        string BoardStatus()=>Arrows!=null?Arrows.Remaining+" / "+Arrows.TargetCount+" arrows":Octa!=null?Octa.Remaining+" arrows":OneLine!=null?OneLine.Trail.Count+" steps":Links!=null?ConnectedCount()+" / "+Links.Connected.Length+" pairs":Mathf.FloorToInt(elapsed)+"s";
        int ConnectedCount(){int n=0;foreach(bool c in Links.Connected)if(c)n++;return n;}
        string BoardInstruction()=>Arrows!=null?(Arrows.Cube?"Drag to rotate. Tap an arrow to free it.":Mode==GameMode.Countdown?"Clear the marked arrow before its countdown expires.":Mode==GameMode.Reasoning?"Remember the paths. Tap to reveal an arrow.":"Tap to clear. Hold an arrow to preview its path."):Octa!=null?"Find a clear route through the arrows.":OneLine!=null?"Connect every line in one stroke.":Links!=null?"Connect matching dots. Do not cross lines.":"Find the numbers in order.";
        void DrawTray(){OriginalArt.Draw("CommonAreaBg",new Rect(55,1370,970,120),Ink.Hex("D1C5AF"),false,true);for(int i=0;i<7;i++){var p=new Rect(105+i*132,1387,100,86);OriginalArt.Draw("CommonBtnProps",p,Color.white,false,true);if(i<tray.Colors.Count)OriginalArt.Draw("Arrow_head_black",new Rect(p.x+19,p.y+10,62,62),Ink.Palette[Mathf.Abs(tray.Colors[i])%7],true);}}
        void DrawModal()
        {
            int state=won?1:failed?2:3;if(state!=modalArtState){modalArtState=state;modalArtEntered=Time.unscaledTime;}
            if(won){DrawWinPopup();return;}
            if(paused&&!won&&!failed){DrawPauseArtwork();return;}
            string layout=won?(Mode==GameMode.Arrows3D?"Arrow3DSuccessUI":"SuccessResultUI"):("FailResultUI");
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
            if(Ink.HitButton(primary,GUIContent.none,GUIStyle.none))
            {
                RequestResultContinue();
                return;
            }
            var home=OriginalArt.NodeRect(layout,"/Button/HomeBtn",new Rect(0,0,Width,Height),new Vector2(Width,Height),binding);
            if(home.width>100){if(Ink.HitButton(home,GUIContent.none,GUIStyle.none)){ReturnHome();return;}}
            else if(OriginalArt.Button(new Rect(250,1650,580,110),"Home",OriginalArt.Accent,40)){ReturnHome();return;}
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
            Ink.Rect(gameViewport,OriginalArt.Background);
            DrawCelebration();
            float height=winHintReward>0?1380:1220;
            var panel=new Rect(gameViewport.center.x-450,gameViewport.center.y-height*.5f,900,height);
            Ink.Card(new Rect(panel.x,panel.y+12,panel.width,panel.height),Ink.WithAlpha(Color.black,.07f),40);
            Ink.Card(panel,Color.Lerp(OriginalArt.Background,Color.white,OriginalArt.Theme==2?.07f:.5f),40);
            int lostHearts=Mathf.Max(0,(Mode==GameMode.OneLife?1:3)-hearts);
            int earnedStars=Mathf.Clamp(3-lostHearts,0,3);
            for(int i=0;i<3;i++)
            {
                float size=i==1?154:106;
                var center=new Vector2(panel.center.x+(i-1)*150,panel.y+(i==1?146:166));
                var starColor=i<earnedStars?Ink.Gold:Ink.WithAlpha(FG,.16f);
                OriginalArt.Draw("RiBtnStar",new Rect(center-Vector2.one*size*.5f,Vector2.one*size),starColor,true);
            }
            Ink.TextAt(new Rect(panel.x+60,panel.y+268,780,108),"Well Done!",76,FG,TextAnchor.MiddleCenter,true);
            Ink.TextAt(new Rect(panel.x+60,panel.y+380,780,64),"Level "+(LevelIndex+1)+" Completed!",34,FG,TextAnchor.MiddleCenter);
            var stats=new Rect(panel.x+70,panel.y+490,760,170);
            Ink.Card(stats,Ink.WithAlpha(OriginalArt.Accent,.09f),24);
            Ink.Rect(new Rect(stats.center.x,stats.y+32,2,106),Ink.WithAlpha(OriginalArt.Accent,.2f));
            Ink.TextAt(new Rect(stats.x,stats.y+25,380,40),"Time",27,FG,TextAnchor.MiddleCenter);
            Ink.TextAt(new Rect(stats.center.x,stats.y+25,380,40),"Moves",27,FG,TextAnchor.MiddleCenter);
            string time=Mathf.FloorToInt(elapsed/60).ToString("00")+":"+Mathf.FloorToInt(elapsed%60).ToString("00");
            Ink.TextAt(new Rect(stats.x,stats.y+70,380,70),time,48,FG,TextAnchor.MiddleCenter,true);
            Ink.TextAt(new Rect(stats.center.x,stats.y+70,380,70),moves.ToString(),48,FG,TextAnchor.MiddleCenter,true);
            if(winHintReward>0)
            {
                var reward=new Rect(panel.x+190,panel.y+710,520,108);
                Ink.Card(reward,Ink.WithAlpha(OriginalArt.Accent,.12f),24);
                OriginalArt.Draw("CommonIconHint",new Rect(reward.x+100,reward.y+21,66,66),OriginalArt.Accent,true);
                Ink.TextAt(new Rect(reward.x+182,reward.y,250,108),"+"+winHintReward+" Hint",42,FG,TextAnchor.MiddleLeft,true);
            }
            float nextY=panel.y+(winHintReward>0?870:710);
            if(OriginalArt.Button(new Rect(panel.x+70,nextY,760,280),"Next Level",OriginalArt.Accent,64))
            {RequestResultContinue();GUI.enabled=true;return;}
            var home=new Rect(panel.x+220,nextY+315,460,90);
            Ink.Card(home,Ink.WithAlpha(OriginalArt.Accent,.08f),22);
            Ink.TextAt(home,"Home",34,FG,TextAnchor.MiddleCenter,true);
            if(Ink.HitButton(home,GUIContent.none,GUIStyle.none))ReturnHome();
            GUI.enabled=true;
        }
        void DrawHintButton(Rect rect)
        {
            if(OriginalArt.IconButton(rect,"CommonIconHint"))Hint();
            bool unlimited=HintEconomy.Unlimited(Mode,LevelIndex);
            string text=unlimited?"∞":Progress.freeHints>0?Progress.freeHints.ToString():"AD";
            float size=rect.width*.4f;
            Ink.TextAt(new Rect(rect.xMax-size,rect.yMax-size,size+8,size+8),text,
                Mathf.RoundToInt(rect.width*.33f),Ink.Red,TextAnchor.MiddleCenter,true);
        }
        void DrawPauseArtwork()
        {
            string layout="GameSettingUI";
            var binding=new ArtBinding
            {
                Visible=(path,active)=>
                {
                    if(path.Contains("/HomeBtn"))return false;
                    if(path.Contains("/Music")||path.Contains("/GuideLine")||path.Contains("/Voice")||path.Contains("/FoucsMode")||path.Contains("/FeedBack")||path.Contains("/EffectNode")||path.EndsWith("/TogglePar/Handle"))return false;
                    if(path.Contains("/Sound/TogglePar/On"))return Progress.sound;if(path.Contains("/Sound/TogglePar/Off"))return !Progress.sound;if(path.Contains("/Vibration/TogglePar/On"))return Progress.vibration;if(path.Contains("/Vibration/TogglePar/Off"))return !Progress.vibration;return active;
                }
            };
            OriginalArt.Layout(layout,new Rect(0,0,Width,Height),new Vector2(Width,Height),binding);
            SettingsSwitches(layout,binding);
            var close=OriginalArt.NodeRect(layout,"/Dialog_mid/CloseBtn",new Rect(0,0,Width,Height),new Vector2(Width,Height),binding);if(Ink.HitButton(close,GUIContent.none,GUIStyle.none))paused=false;
            var restart=OriginalArt.NodeRect(layout,"/DlgBtn/RestartBtn",new Rect(0,0,Width,Height),new Vector2(Width,Height),binding);
            if(restart.width>0&&Ink.HitButton(restart,GUIContent.none,GUIStyle.none))StartGame(Mode,LevelIndex);
        }
        void DrawCelebration(){for(int i=0;i<22;i++){float t=(Time.unscaledTime*.28f+i*.073f)%1;float x=95+(i*137%890)+Mathf.Sin(t*9+i)*30;OriginalArt.Draw("RiBtnStar",new Rect(x,-100+t*2000,24+i%4*9,24+i%4*9),Ink.Palette[i%7],true);}}
        void DrawHelp()
        {
            Ink.Rect(new Rect(0,0,Width,Height),new Color(0,0,0,.65f));OriginalArt.Draw("CommonDialogBg",new Rect(85,300,910,1270),Color.white,false,true);Ink.TextAt(new Rect(145,365,790,100),"How to play",64,Ink.Hex("30353C"),TextAnchor.MiddleCenter,true);
            Ink.TextAt(new Rect(165,530,750,760),"ARROWS\nTap an arrow with a clear route to the edge.\n\nARROW CLASH\nCollect three arrows of the same color. Keep the tray from filling up.\n\nLINK DOTS\nConnect matching colors without crossing paths.\n\nONE LINE\nTrace every edge exactly once.\n\n3D\nDrag to rotate the shape. Only the visible surface can be tapped.",34,Ink.Hex("60656E"));if(OriginalArt.Button(new Rect(175,1380,730,120),"Got it",OriginalArt.Accent,45))modalHelp=false;
        }
    }
}
