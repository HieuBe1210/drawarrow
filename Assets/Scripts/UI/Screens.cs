using System;
using UnityEngine;

namespace DrawArrow
{
    public sealed partial class GameApp
    {
        const float HomeContentBottom = Height * .875f;
        const float GameBannerReserve = .125f;
        const float HomeLevelRowTop = 1192;
        const float HomePlayPanelHeight = 198;
        const float HomePlayPanelTop = HomeLevelRowTop + 132;
        const float GameHeaderButtonTop = 58;
        const float GameHeaderButtonSize = 94;
        const float ScreenTransitionDuration = .26f;
        Rect gameViewport;float scale;Vector2 screenOffset;
        Page displayedPage;
        int gameScreenVersion, displayedGameVersion;
        bool hasDisplayedPage;
        float screenTransitionStart = float.NegativeInfinity;
        Vector2 screenTransitionOffset;
        enum HomeFeature { Daily, MoreGames }
        static readonly HomeFeature[] HomeFeatures = { HomeFeature.Daily, HomeFeature.MoreGames };
        Vector2 homeFeaturePress;
        float homeFeatureOffset, homeFeatureTarget, homeFeatureDragStart, homeFeatureVelocity;
        bool homeFeatureTracking, homeFeatureDragging;
        int homeFeatureRepaintFrame = -1;

        Color FG=>CircuitStyle.Foreground;

        void OnGUI()
        {
            Ink.Init();GUI.depth=0;
            float transition = GetScreenTransitionProgress();
            GUI.matrix=Matrix4x4.identity;Ink.Rect(new Rect(0,0,Screen.width,Screen.height),CircuitStyle.Background);
            if (ads) ads.DrawNativeFrame();
            var mainCamera=Camera.main;if(mainCamera)mainCamera.backgroundColor=CircuitStyle.Background;
            if(ads && ads.Blocking)
            {
                Ink.TextAt(new Rect(0, 0, Screen.width, Screen.height), ads.LoadingMessage, 30, FG, TextAnchor.MiddleCenter);
                ads.DrawEditorAds();
                return;
            }
            var usable = AdsSafeArea(page == Page.Game);
            scale=Mathf.Min(usable.width/Width,usable.height/Height);
            screenOffset=new Vector2(usable.x+(usable.width-Width*scale)*.5f,Screen.height-usable.yMax+(usable.height-Height*scale)*.5f);
            if(page==Page.Game) gameViewport=BoardLayout.Viewport(usable,Screen.height,screenOffset,scale);

            float remaining = 1 - transition;
            float slide = remaining * remaining * remaining;
            var drawOffset = screenOffset + screenTransitionOffset * (slide * scale);
            GUI.matrix=Matrix4x4.TRS(drawOffset,Quaternion.identity,new Vector3(scale,scale,1));Ink.Rect(page==Page.Game?gameViewport:new Rect(0,0,Width,Height),CircuitStyle.Background);
            if (transition < 1)
            {
                var pointer = Event.current;
                if (pointer.isMouse || pointer.isKey || pointer.type == EventType.ScrollWheel)
                    pointer.Use();
            }
            GUI.enabled=!LevelToolsOpen&&!profileOpen;
            if(page==Page.Game)DrawGame();else if(page==Page.Home)DrawHome();else
            if(page==Page.Settings)DrawSettings();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (page == Page.Game && Event.current.type == EventType.Repaint) DrawGameplayFps();
#endif
            if (Time.unscaledTime < toastUntil && !failed && !won)
            {
                float toastY = page == Page.Home ? (Height - 130) * .5f : 1520;
                CircuitStyle.Draw("Toast_bg", new Rect(105, toastY, 870, 130),
                    Ink.WithAlpha(Color.black, .85f), false, true);
                Ink.TextAt(new Rect(135, toastY + 10, 810, 110),
                    toast, 31, Color.white, TextAnchor.MiddleCenter);
            }
            if(modalHelp&&!won)DrawHelp();
            GUI.enabled=true;
            if (page == Page.Home && profileOpen) DrawHomeProfile();
            else if (page == Page.Home) DrawLevelTestTools();
            if (transition < 1 && Event.current.type == EventType.Repaint)
            {
                GUI.matrix = Matrix4x4.identity;
                Ink.Rect(new Rect(0, 0, Screen.width, Screen.height),
                    Ink.WithAlpha(CircuitStyle.Background, remaining * remaining * .82f));
            }
            GUI.matrix = Matrix4x4.identity;
            if (ads) ads.DrawEditorAds();
        }

        Rect AdsSafeArea(bool deviceSafeArea)
        {
            bool adsVisible = ads && (ads.TopInset > 0 || ads.BottomInset > 0);
            var area = deviceSafeArea || adsVisible ? Screen.safeArea : new Rect(0, 0, Screen.width, Screen.height);
            if (!adsVisible) return area;
            area.yMax -= Mathf.Clamp(ads.TopInset, 0, area.height * .5f);
            area.yMin += Mathf.Clamp(ads.BottomInset, 0, area.height * .5f);
            return area;
        }

        float GetScreenTransitionProgress()
        {
            float now = Time.unscaledTime;
            bool newGame = page == Page.Game && gameScreenVersion != displayedGameVersion;
            if (hasDisplayedPage && (displayedPage != page || newGame))
            {
                screenTransitionStart = now;
                screenTransitionOffset = page == Page.Game ? new Vector2(0, 44)
                    : page == Page.Home ? new Vector2(-54, 0) : new Vector2(54, 0);
                Ink.CancelButtonAnimation();
                homeFeatureTracking = homeFeatureDragging = false;
            }
            displayedPage = page;
            displayedGameVersion = gameScreenVersion;
            hasDisplayedPage = true;
            return Mathf.Clamp01((now - screenTransitionStart) / ScreenTransitionDuration);
        }
        void DrawHome()
        {
            const float featureGap = 24;
            const float featureTop = 468;
            const float featureHeight = 570;
            const float featureLeft = 56;
            const float panelWidth = Width - featureLeft * 2;
            const float featureWidth = (panelWidth - featureGap) * .5f;
            var accent = CircuitStyle.Accent;
            var safe = AdsSafeArea(true);
            float gameScale = Mathf.Min(safe.width / Width, safe.height / Height);
            float settingsTop = (Screen.height - safe.yMax + GameHeaderButtonTop * gameScale - screenOffset.y) / scale;
            float settingsSize = GameHeaderButtonSize * gameScale / scale;
            if (DrawSettingsIconButton(new Rect(940, settingsTop, settingsSize, settingsSize), CircuitStyle.SoftControl))
                page = Page.Settings;
            var avatar = new Rect(Width - 940 - settingsSize, settingsTop, settingsSize, settingsSize);
            if (DrawHomeAvatar(avatar))
                OpenHomeProfile();
            string playerName = string.IsNullOrWhiteSpace(Progress.playerName) ? "Player" : Progress.playerName;
            Ink.TextAt(new Rect(avatar.xMax + 22, avatar.y, 680, avatar.height),
                playerName, 34, FG, TextAnchor.MiddleLeft, true);

            const int titleSize = 93;
            Ink.TextAt(new Rect(featureLeft + 26, 252, panelWidth - 26, 112),
                $"Arrow <color=#{ColorUtility.ToHtmlStringRGB(accent)}>Circuit</color>", titleSize,
                FG, TextAnchor.MiddleLeft, true);
            var line = new Vector2(featureLeft + 28, 398);
            Ink.Line(line, line + Vector2.right * 310, 9, accent);
            CircuitStyle.ArrowHead(line + Vector2.right * 310, Vector2.right, 25, 9, accent);

            DrawHomeFeatureStrip(new Rect(featureLeft, featureTop, panelWidth, featureHeight), featureWidth, featureGap);

            const float levelRowY = HomeLevelRowTop;
            DrawHomeRouteIcon(new Rect(featureLeft, levelRowY, 94, 88), accent);
            DrawHomeLevelStrip(new Rect(featureLeft + 116, levelRowY, panelWidth - 116, 88), accent);

            float panelTop = Mathf.Min(HomePlayPanelTop, HomeContentBottom - HomePlayPanelHeight);
            var card = new Rect(featureLeft, panelTop, panelWidth, HomePlayPanelHeight);
            var play = card;
            var playMatrix = Ink.BeginButtonVisual(play);
            CircuitStyle.RaisedCard(play, accent, play.height * .5f);
            DrawContinuePattern(play);
            Ink.TextAt(new Rect(play.x, play.y + 23, play.width, 78),
                "Continue", 65, Color.white, TextAnchor.MiddleCenter, true);
            Ink.TextAt(new Rect(play.x, play.y + 107, play.width, 54),
                "Level " + (ActiveMainLevel + 1), 43, Ink.WithAlpha(Color.white, .84f), TextAnchor.MiddleCenter);
            GUI.matrix = playMatrix;
            if (Ink.HitButton(play, GUIContent.none, GUIStyle.none)) StartGame(GameMode.Classic);
        }

        void DrawHomeFeatureStrip(Rect viewport, float cardWidth, float gap)
        {
            float stride = cardWidth + gap;
            float maxOffset = Mathf.Max(0, HomeFeatures.Length * stride - gap - viewport.width);
            var pointer = Event.current;
            if (GUI.enabled && pointer.type == EventType.MouseDown && pointer.button == 0 &&
                viewport.Contains(pointer.mousePosition))
            {
                homeFeatureTracking = true;
                homeFeatureDragging = false;
                homeFeaturePress = pointer.mousePosition;
                homeFeatureDragStart = homeFeatureOffset;
            }
            else if (homeFeatureTracking && pointer.type == EventType.MouseDrag && pointer.button == 0)
            {
                var delta = pointer.mousePosition - homeFeaturePress;
                if (homeFeatureDragging || (Mathf.Abs(delta.x) > 14 && Mathf.Abs(delta.x) > Mathf.Abs(delta.y)))
                {
                    if (!homeFeatureDragging)
                        Ink.CancelButtonAnimation();
                    homeFeatureDragging = true;
                    float raw = homeFeatureDragStart - delta.x;
                    float bounded = Mathf.Clamp(raw, 0, maxOffset);
                    homeFeatureOffset = bounded + Mathf.Clamp((raw - bounded) * .28f, -70, 70);
                    pointer.Use();
                }
            }
            else if (homeFeatureTracking && (pointer.type == EventType.MouseUp || pointer.type == EventType.Ignore))
            {
                homeFeatureTracking = false;
                if (homeFeatureDragging)
                {
                    homeFeatureDragging = false;
                    homeFeatureVelocity = 0;
                    homeFeatureTarget = Mathf.Clamp(homeFeatureOffset, 0, maxOffset);
                    if (pointer.type == EventType.MouseUp) pointer.Use();
                }
            }
            else if (GUI.enabled && maxOffset > 0 && pointer.type == EventType.ScrollWheel &&
                viewport.Contains(pointer.mousePosition))
            {
                float delta = Mathf.Abs(pointer.delta.x) > Mathf.Abs(pointer.delta.y) ? pointer.delta.x : pointer.delta.y;
                homeFeatureTarget = Mathf.Clamp(homeFeatureTarget + delta * 33, 0, maxOffset);
                pointer.Use();
            }

            if (!homeFeatureDragging && pointer.type == EventType.Repaint && homeFeatureRepaintFrame != Time.frameCount)
            {
                homeFeatureRepaintFrame = Time.frameCount;
                homeFeatureOffset = Mathf.SmoothDamp(homeFeatureOffset, homeFeatureTarget, ref homeFeatureVelocity,
                    .16f, Mathf.Infinity, Time.unscaledDeltaTime);
            }

            GUI.BeginGroup(viewport);
            for (int i = 0; i < HomeFeatures.Length; i++)
            {
                float x = i * stride - homeFeatureOffset;
                bool daily = HomeFeatures[i] == HomeFeature.Daily;
                bool unlocked = daily && FeatureUnlocks.DailyUnlocked(Progress);
                int unlockLevel = FeatureUnlocks.DailyLevel;
                var rect = new Rect(x, 0, cardWidth, viewport.height);
                if (!DrawHomeFeatureCard(rect,
                    !daily ? "Coming soon" : unlocked ? "Play" : "Unlock at level " + unlockLevel,
                    daily ? CircuitStyle.Accent : CircuitStyle.Teal, unlocked, daily))
                    continue;

                if (!daily) Notify("Coming soon");
                else if (unlocked) StartGame(GameMode.Daily);
                else Notify("Unlock at level " + unlockLevel);
            }
            GUI.EndGroup();
        }

        void DrawHomeLevelStrip(Rect strip, Color accent)
        {
            const int nodeCount = 5;
            int current = ActiveMainLevel;
            int first = Mathf.Min(current, ActiveMainLevelCount - nodeCount);
            const float inset = 45;
            float spacing = (strip.width - inset * 2) / (nodeCount - 1);
            float y = strip.center.y;
            var upcomingInk = CircuitStyle.SoftControl;

            for (int i = 0; i < nodeCount - 1; i++)
            {
                int level = first + i;
                float radius = level == current ? 44 : 35;
                float nextRadius = level + 1 == current ? 44 : 35;
                float x = strip.x + inset + i * spacing + radius + 5;
                float end = strip.x + inset + (i + 1) * spacing - nextRadius - 5;
                bool solid = level <= current;
                var color = solid ? accent : Ink.WithAlpha(CircuitStyle.ControlInk, .62f);
                if (solid)
                    Ink.Line(new Vector2(x, y), new Vector2(end, y), 4, color);
                else
                    for (float dash = x; dash < end - 8; dash += 11)
                        Ink.Line(new Vector2(dash, y), new Vector2(Mathf.Min(dash + 5, end - 8), y), 4, color);
                CircuitStyle.ArrowHead(new Vector2(end, y), Vector2.right, 9, 4, color);
            }

            for (int i = 0; i < nodeCount; i++)
            {
                int level = first + i;
                bool active = level == current;
                float radius = active ? 44 : 35;
                var center = new Vector2(strip.x + inset + i * spacing, y);
                bool upcoming = level > current;
                bool hard = CircuitLevels.IsHardMainLevel(level);
                Ink.Circle(center, radius, hard ? HomeHardLevelColor : upcoming ? upcomingInk : accent);
                Ink.TextAt(new Rect(center.x - radius, y - radius, radius * 2, radius * 2),
                    (level + 1).ToString(), active ? 32 : 26, upcoming && !hard ? FG : Color.white,
                    TextAnchor.MiddleCenter, true);
            }
        }

        static Color HomeHardLevelColor => Ink.Hex("AD4F2D");
        bool DrawSettingsIconButton(Rect rect, Color fill)
        {
            var matrix = Ink.BeginButtonVisual(rect);
            Ink.Card(rect, fill, 24);
            var icon = fill == CircuitStyle.SoftControl ? CircuitStyle.ControlInk : Color.white;
            var center = rect.center;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Ink.Line(center + direction * 20, center + direction * 31, 10, icon);
            }
            Ink.Circle(center, 24, icon);
            Ink.Circle(center, 9, fill);
            GUI.matrix = matrix;
            return Ink.HitButton(rect, new GUIContent(string.Empty, "Settings"), GUIStyle.none);
        }

        static void DrawHomeRouteIcon(Rect rect, Color fill)
        {
            Ink.Card(rect, fill, 24);
            var center = rect.center;
            var left = center + new Vector2(-26, 14);
            var middle = center + new Vector2(0, -12);
            var right = center + new Vector2(26, 4);
            Ink.Line(left, middle, 5, Color.white);
            Ink.Line(middle, right, 5, Color.white);
            Ink.Circle(left, 8, Color.white);
            Ink.Circle(middle, 8, Color.white);
            Ink.Circle(right, 8, Color.white);
        }

        bool DrawHomeFeatureCard(Rect card, string action, Color fill, bool unlocked, bool daily)
        {
            var matrix = Ink.BeginButtonVisual(card);
            CircuitStyle.RaisedCard(card, fill, 42);
            DrawHomeFeaturePattern(card, daily);
            Ink.TextAt(new Rect(card.x + 18, card.y + 38, card.width - 36, 137),
                daily ? "Daily\nChallenge" : "More\nGames", 53, Color.white, TextAnchor.MiddleCenter, true);

            var center = new Vector2(card.center.x, card.y + 301);
            var icon = Ink.WithAlpha(Color.white, .95f);
            if (daily) DrawHomeTrophy(center, fill, icon);
            else
            {
                for (int y = 0; y < 2; y++)
                    for (int x = 0; x < 2; x++)
                        Ink.Card(new Rect(center.x - 82 + x * 89, center.y - 82 + y * 89, 76, 76), icon, 20);
            }

            var footer = new Rect(card.x + 25, card.yMax - 104, card.width - 50, 78);
            Ink.Card(footer, Color.Lerp(fill, Color.black, unlocked ? .28f : .4f), 18);
            var caption = footer;
            if (daily && !unlocked)
            {
                var origin = new Vector2(footer.x + 28, footer.center.y);
                Ink.Circle(origin + new Vector2(0, -9), 11, icon);
                Ink.Circle(origin + new Vector2(0, -9), 6, Color.Lerp(fill, Color.black, .4f));
                Ink.Card(new Rect(origin.x - 14, origin.y - 5, 28, 22), icon, 5);
                caption.xMin += 44;
            }
            Ink.TextAt(caption, action, 30, Color.white, TextAnchor.MiddleCenter, true);
            GUI.matrix = matrix;
            return Ink.HitButton(card, GUIContent.none, GUIStyle.none);
        }

        static void DrawHomeTrophy(Vector2 center, Color fill, Color icon)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                Ink.Curve(center + new Vector2(side * 56, -70), center + new Vector2(side * 133, -84),
                    center + new Vector2(side * 62, 14), 11, icon);
                var laurel = Ink.WithAlpha(icon, .55f);
                Ink.Curve(center + new Vector2(side * 89, 88), center + new Vector2(side * 153, 59),
                    center + new Vector2(side * 143, -2), 4, laurel);
                for (int i = 0; i < 4; i++)
                {
                    var leaf = center + new Vector2(side * (109 + i * 10), 71 - i * 20);
                    Ink.Line(leaf, leaf + new Vector2(side * 14, -13), 9, laurel);
                }
            }
            Ink.Card(new Rect(center.x - 68, center.y - 87, 136, 104), icon, 35);
            Ink.Circle(center + new Vector2(0, 5), 49, icon);
            Ink.Card(new Rect(center.x - 12, center.y + 32, 24, 47), icon, 8);
            Ink.Card(new Rect(center.x - 57, center.y + 73, 114, 21), icon, 6);
            Ink.TextAt(new Rect(center.x - 45, center.y - 60, 90, 76),
                "★", 61, fill, TextAnchor.MiddleCenter, true);
        }

        static void DrawContinuePattern(Rect rect)
        {
            var ink = Ink.WithAlpha(Color.white, .94f);
            DrawChevron(rect.xMax - 137, rect.center.y, ink);
            DrawChevron(rect.xMax - 102, rect.center.y, ink);
        }

        static void DrawChevron(float x, float y, Color color)
        {
            var tip = new Vector2(x + 23, y);
            Ink.Line(new Vector2(x, y - 23), tip, 5, color);
            Ink.Line(tip, new Vector2(x, y + 23), 5, color);
        }

        static void DrawHomeFeaturePattern(Rect rect, bool daily)
        {
            var ink = Ink.WithAlpha(Color.white, .13f);
            if (daily)
            {
                DrawCardSparkle(new Vector2(rect.x + 57, rect.y + 133), 15, ink);
                DrawCardSparkle(new Vector2(rect.xMax - 56, rect.y + 157), 22, ink);
                DrawCardSparkle(new Vector2(rect.x + 73, rect.y + 254), 23, ink);
                DrawCardSparkle(new Vector2(rect.xMax - 66, rect.y + 281), 13, ink);
                Ink.Circle(new Vector2(rect.x + 39, rect.y + 200), 4, ink);
                Ink.Circle(new Vector2(rect.xMax - 42, rect.y + 221), 5, ink);
                return;
            }

            for (int row = 0; row < 4; row++)
                for (int column = 0; column < 2; column++)
                {
                    float inset = 34 + column * 31 + (row % 2) * 9;
                    float y = rect.y + 115 + row * 48 + column * 12;
                    float size = (row + column) % 2 == 0 ? 17 : 10;
                    Ink.Card(new Rect(rect.x + inset, y, size, size), ink, 4);
                    Ink.Card(new Rect(rect.xMax - inset - size, y + 17, size, size), ink, 4);
                }
        }

        static void DrawCardSparkle(Vector2 center, float radius, Color color)
        {
            var top = center - Vector2.up * radius;
            var bottom = center + Vector2.up * radius;
            var left = center - Vector2.right * radius * .65f;
            var right = center + Vector2.right * radius * .65f;
            Ink.Line(top, right, 3, color);
            Ink.Line(right, bottom, 3, color);
            Ink.Line(bottom, left, 3, color);
            Ink.Line(left, top, 3, color);
        }
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
            if(WinPopupVisible){DrawModal();return;}
            var mainHintRect=new Rect(940,160,94,94);
            int levelNumber = LevelIndex + 1;
            bool showInstructions = Mode == GameMode.Classic && levelNumber >= 1 && levelNumber <= 10;
            bool showZoomControls = Mode == GameMode.Classic && Arrows != null;
            bool teachZoom = showZoomControls && levelNumber == 5;
            bool showModeStatus = Modes.IsTimed(Mode) || Schulte != null || Octa != null;
            GUI.BeginGroup(new Rect(0,gameViewport.yMin,Width,340));
            GUI.enabled = GUI.enabled && !won && !RewardAdPending && !(failed && HeartBreakProgress(0) < 1);
            if(CircuitStyle.IconButton(new Rect(48,GameHeaderButtonTop,GameHeaderButtonSize,GameHeaderButtonSize),"CommonIconBack"))
            {ReturnHome();GUI.EndGroup();return;}
            Ink.TextAt(new Rect(180,60,720,80),Mode==GameMode.Daily?dailyDate.ToString("MMM dd"):"Level "+(LevelIndex+1),58,FG,TextAnchor.MiddleCenter,true);
            if (DrawSettingsIconButton(new Rect(940, GameHeaderButtonTop, GameHeaderButtonSize, GameHeaderButtonSize),
                CircuitStyle.Accent))
                paused = true;
            for (int i = 0; i < (Mode == GameMode.OneLife ? 1 : 3); i++)
                DrawGameHeart(new Rect(435 + i * 74, 175, 64, 64), i);
            GUI.enabled = GUI.enabled && CanInteract;
            DrawHintButton(mainHintRect);
            GUI.enabled = true;
            if(showModeStatus)Ink.TextAt(new Rect(160,249,760,70),Modes.IsTimed(Mode)?Mathf.CeilToInt(timeLeft)+"s":Schulte!=null?"Find "+Schulte.Next:Octa.MovesLeft+" moves",39,FG,TextAnchor.MiddleCenter,true);
            GUI.EndGroup();
            // Keep the board and its controls above the banner reserve, even before an ad loads.
            float bannerBoundary = (Screen.height * (1 - GameBannerReserve) -
                Mathf.Max(screenOffset.y, GUI.matrix.m13)) / scale;
            float footerBottom = Mathf.Min(bannerBoundary, gameViewport.yMax) - 8;
            float footerY=Mathf.Min(gameViewport.yMax-Height,footerBottom-1905);
            float footerLift=gameViewport.yMax-Height-footerY;
            const float instructionHeight = 70;
            const float zoomHeight = 65;
            float zoomTop = footerBottom - zoomHeight;
            float instructionTop = (showZoomControls ? zoomTop - 12 : footerBottom) - instructionHeight;
            float top = gameViewport.yMin + Mathf.Max(mainHintRect.yMax + 30 / scale, showModeStatus ? 330 : 0);
            float bottom = showInstructions ? instructionTop - 8 : showZoomControls ? zoomTop - 8 : footerBottom;
            if (Mode == GameMode.ArrowClash || Mode == GameMode.Battle)
                bottom = Mathf.Min(bottom, gameViewport.yMax - footerLift - (Mode == GameMode.ArrowClash ? 590 : 455));
            var boardRect=new Rect(gameViewport.xMin+32,top,gameViewport.width-64,Mathf.Max(100,bottom-top));
            GUI.BeginGroup(boardRect);DrawBoard(new Rect(0,0,boardRect.width,boardRect.height), boardRect.position);GUI.EndGroup();
            if (won)
            {
                if (victoryPhase == VictoryPhase.Flight && Event.current.type == EventType.Repaint)
                {
                    var boardCenter = projectedCenter;
                    projectedCenter += boardRect.position;
                    foreach (var arrow in flying)
                        DrawArrowPath(arrow.id, arrow.elapsed, arrow.id == victoryFinalArrow);
                    projectedCenter = boardCenter;
                }
                else if (victoryPhase == VictoryPhase.Confetti && Event.current.type == EventType.Repaint)
                    DrawVictoryConfetti();
                GUI.enabled = true;
                return;
            }
            if(Mode==GameMode.ArrowClash){GUI.BeginGroup(new Rect(0,footerY,Width,Height));DrawTray();GUI.EndGroup();}
            if(Mode==GameMode.Battle){Ink.TextAt(new Rect(78,footerY+1470,930,40),"Local opponent",27,FG);CircuitStyle.Draw("BarBg",new Rect(78,footerY+1520,924,18),Ink.Hex("CCBDA3"),false,true);Ink.Rect(new Rect(78,footerY+1520,924*botProgress,18),Ink.Red);}
            else if (showInstructions)
            {
                string instruction = teachZoom
                    ? (Application.isMobilePlatform ? "Spread two fingers or tap + to zoom in." : "Scroll up or click + to zoom in.")
                    : BoardInstruction();
                Ink.TextAt(new Rect(78, instructionTop, 924, instructionHeight), instruction, 27, FG, TextAnchor.MiddleCenter);
            }
            GUI.enabled=CanInteract;
            if (showZoomControls) DrawBoardZoomControls(zoomTop, zoomHeight, teachZoom && zoom <= 1);
            GUI.enabled=true;
            if(CanInteract&&Arrows!=null&&!Arrows.Cube)DrawHeldArrowRoute(boardRect.position);
            if(RewardAdPending)Ink.TextAt(new Rect(180,gameViewport.center.y-45,720,90),"Loading ad…",38,FG,TextAnchor.MiddleCenter,true);
            bool lastHeartBreaking = failed && HeartBreakProgress(0) < 1;
            if(!RewardAdPending&&!lastHeartBreaking&&(paused||won||failed))DrawModal();
        }

        void DrawBoardZoomControls(float top, float height, bool highlightPlus)
        {
            var minus = new Rect(70, top, 190, height);
            var reset = new Rect(360, top, 360, height);
            var plus = new Rect(820, top, 190, height);
            if (highlightPlus) Ink.Card(plus, CircuitStyle.Accent, 18);
            if (Ink.HitButton(minus, GUIContent.none, GUIStyle.none)) zoom = Mathf.Max(.6f, zoom - .25f);
            if (Ink.HitButton(reset, GUIContent.none, GUIStyle.none)) { zoom = 1; pan = Vector2.zero; }
            if (Ink.HitButton(plus, GUIContent.none, GUIStyle.none)) zoom = Mathf.Min(4, zoom + .25f);
            Ink.TextAt(minus, "−", 44, FG, TextAnchor.MiddleCenter);
            Ink.TextAt(reset, Arrows.Cube ? "Rotate / Zoom" : "Fit board", 28, FG, TextAnchor.MiddleCenter);
            Ink.TextAt(plus, "+", 44, highlightPlus ? Color.white : FG, TextAnchor.MiddleCenter);
        }

        void DrawGameHeart(Rect rect, int index)
        {
            float progress = HeartBreakProgress(index);
            if (index < hearts || progress >= 1)
            {
                CircuitStyle.Draw("CommonIconHeart1", rect,
                    index < hearts ? Color.white : Ink.WithAlpha(Color.white, .2f), true);
                return;
            }

            var red = Ink.Hex("EA536A");
            Ink.Heart(rect, Ink.WithAlpha(red, .2f));
            if (progress < .28f)
            {
                float pulse = 1 + Mathf.Sin(progress / .28f * Mathf.PI) * .13f;
                var size = rect.size * pulse;
                Ink.Heart(new Rect(rect.center - size * .5f, size), red);
                var center = rect.center;
                var bend1 = center + new Vector2(-5, -5);
                var bend2 = center + new Vector2(5, 2);
                Ink.Line(center + new Vector2(2, -20), bend1, 4, CircuitStyle.Background);
                Ink.Line(bend1, bend2, 4, CircuitStyle.Background);
                Ink.Line(bend2, center + new Vector2(-2, 18), 4, CircuitStyle.Background);
                return;
            }

            float split = Mathf.InverseLerp(.28f, 1, progress);
            float spread = Mathf.SmoothStep(0, 22, split);
            float drop = 28 * split * split;
            var color = Ink.WithAlpha(red, 1 - split);
            var left = new Rect(rect.x - spread, rect.y + drop, rect.width * .5f, rect.height);
            var right = new Rect(rect.center.x + spread, rect.y + drop, rect.width * .5f, rect.height);
            Ink.HeartHalf(left, false, color);
            Ink.HeartHalf(right, true, color);
            for (int i = 0; i < 5; i++)
            {
                float angle = i * 1.26f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Ink.Circle(rect.center + direction * (10 + 31 * split), 3, color);
            }
        }

        void DrawVictoryConfetti()
        {
            float progress = Mathf.Clamp01(victoryConfettiElapsed / VictoryConfettiDuration);
            float time = Mathf.Sqrt(progress);
            float opacity = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.62f, 1, progress));
            var bounds = gameViewport;
            var matrix = GUI.matrix;
            for (int i = 0; i < 72; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                float variation = (i * 47 % 73) / 72f;
                float height = (i * 29 % 71) / 70f;
                var origin = new Vector2(bounds.center.x + side * bounds.width * .31f, bounds.center.y + 135);
                var point = origin + new Vector2(-side * (70 + variation * 540) * time,
                    -(180 + height * 510) * time + 475 * time * time);
                if (!bounds.Contains(point)) continue;
                float angle = i * .79f + time * ((i % 3 - 1) * 8 + 3);
                float halfLength = 6 + i % 9;
                float thickness = 4 + i % 4;
                var color = Ink.WithAlpha(Ink.Palette[i % Ink.Palette.Length], opacity);
                // Reuse the white GUI texture instead of rasterizing every spinning angle.
                GUI.matrix = matrix * Matrix4x4.TRS(point,
                    Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg), Vector3.one);
                Ink.Card(new Rect(-halfLength - thickness * .5f, -thickness * .5f,
                    halfLength * 2 + thickness, thickness), color, thickness * .5f);
            }
            GUI.matrix = matrix;
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
            if (CircuitStyle.Button(new Rect(panel.x + 90, panel.y + 462, 680, 96), "Continue · Watch ad", CircuitStyle.Accent, 36))
                RequestRewardedContinue();
            if (!string.IsNullOrEmpty(continueAdError))
                Ink.TextAt(new Rect(panel.x + 45, panel.y + 574, 770, 50), continueAdError,
                    25, FG, TextAnchor.MiddleCenter);
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
            Ink.TextAt(new Rect(panel.x+60,panel.y+380,780,64),
                Mode == GameMode.Daily ? "Daily Challenge Completed!" : "Level " + (LevelIndex + 1) + " Completed!",
                34, FG, TextAnchor.MiddleCenter);
            var stats=new Rect(panel.x+70,panel.y+490,760,170);
            Ink.Card(stats,Ink.WithAlpha(CircuitStyle.Accent,.09f),24);
            bool daily = Mode == GameMode.Daily;
            float column = stats.width / (daily ? 3 : 2);
            int finishTime = Mathf.RoundToInt(elapsed * 1000);
            DrawWinStat(stats, 0, column, "Time", FormatCompletionTime(finishTime, daily));
            if (daily)
                DrawWinStat(stats, 1, column, "Best time",
                    FormatCompletionTime(DailyChallengeResults.BestTime(Progress, DailyDateKey), true));
            DrawWinStat(stats, daily ? 2 : 1, column, "Moves", moves.ToString());
            if(winHintReward>0)
            {
                var reward=new Rect(panel.x+190,panel.y+710,520,108);
                Ink.Card(reward,Ink.WithAlpha(CircuitStyle.Accent,.12f),24);
                DrawHintBulb(new Rect(reward.x + 100, reward.y + 21, 66, 66), CircuitStyle.Accent);
                Ink.TextAt(new Rect(reward.x+182,reward.y,250,108),"+"+winHintReward+" Hint",42,FG,TextAnchor.MiddleLeft,true);
            }
            float nextY=panel.y+(winHintReward>0?870:710);
            var nextButton = new Rect(panel.x + 70, nextY, 760, 280);
            bool nextPressed;
            if (!daily && !FinishedLevelSet)
            {
                int nextIndex = LevelIndex + 1;
                bool hard = CircuitLevels.IsHardMainLevel(nextIndex);
                var matrix = Ink.BeginButtonVisual(nextButton);
                CircuitStyle.RaisedCard(nextButton, hard ? HomeHardLevelColor : CircuitStyle.Accent, 32);
                Ink.TextAt(new Rect(nextButton.x, nextButton.y + 59, nextButton.width, 84),
                    "Next Level", 64, Color.white, TextAnchor.MiddleCenter, true);
                string subtitle = hard ? $"Level {nextIndex + 1} · Hard Level" : $"Level {nextIndex + 1}";
                Ink.TextAt(new Rect(nextButton.x + 20, nextButton.y + 147, nextButton.width - 40, 58),
                    subtitle, 39, Ink.WithAlpha(Color.white, .9f), TextAnchor.MiddleCenter);
                GUI.matrix = matrix;
                nextPressed = Ink.HitButton(nextButton, GUIContent.none, GUIStyle.none);
            }
            else
                nextPressed = CircuitStyle.Button(nextButton, daily ? "Play Again" : "Finish", CircuitStyle.Accent, 58);
            if(nextPressed)
            {RequestResultContinue();GUI.enabled=true;return;}
            var home=new Rect(panel.x+220,nextY+315,460,90);
            Ink.Card(home,Ink.WithAlpha(CircuitStyle.Accent,.08f),22);
            Ink.TextAt(home,"Home",34,FG,TextAnchor.MiddleCenter,true);
            if(Ink.HitButton(home,GUIContent.none,GUIStyle.none))ReturnHome();
            GUI.enabled=true;
        }

        void DrawWinStat(Rect stats, int index, float width, string label, string value)
        {
            float x = stats.x + index * width;
            if (index > 0)
                Ink.Rect(new Rect(x, stats.y + 32, 2, 106), Ink.WithAlpha(CircuitStyle.Accent, .2f));
            Ink.TextAt(new Rect(x, stats.y + 25, width, 40), label, 27, FG, TextAnchor.MiddleCenter);
            Ink.TextAt(new Rect(x, stats.y + 70, width, 70), value, 48, FG, TextAnchor.MiddleCenter, true);
        }

        static string FormatCompletionTime(int milliseconds, bool showTenths)
        {
            int seconds = milliseconds / 1000;
            string time = $"{seconds / 60:00}:{seconds % 60:00}";
            return showTenths ? time + "." + (milliseconds / 100 % 10) : time;
        }
        static void DrawHintBulb(Rect rect, Color color)
        {
            float size = Mathf.Min(rect.width, rect.height);
            var center = rect.center;
            Ink.Circle(center + new Vector2(0, -.13f * size), .17f * size, color);
            Ink.Card(new Rect(center.x - .085f * size, center.y - .055f * size,
                .17f * size, .19f * size), color, .035f * size);
            Ink.Line(center + new Vector2(-.065f, .22f) * size,
                center + new Vector2(.065f, .22f) * size, .05f * size, color);
            Ink.Line(center + new Vector2(-.04f, .30f) * size,
                center + new Vector2(.04f, .30f) * size, .035f * size, color);
        }

        void DrawHintButton(Rect rect)
        {
            var matrix = Ink.BeginButtonVisual(rect);
            Ink.Card(rect, CircuitStyle.Accent, 24);
            DrawHintBulb(rect, Color.white);
            bool unlimited=HintEconomy.Unlimited(Mode,LevelIndex);
            string text=unlimited?"∞":Progress.freeHints>0?Progress.freeHints.ToString():"AD";
            float radius = Mathf.Min(rect.width, rect.height) * .19f;
            var center = new Vector2(rect.xMax, rect.yMax);
            Ink.Circle(center, radius, Ink.Hex("C6384C"));
            var badge = new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2);
            int fontSize = Mathf.RoundToInt(radius * Mathf.Min(1.2f, 3.1f / text.Length));
            Ink.TextAt(badge, text, fontSize, Color.white, TextAnchor.MiddleCenter, true);
            GUI.matrix = matrix;
            if (Ink.HitButton(rect, new GUIContent(string.Empty, "Hint"), GUIStyle.none)) Hint();
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
