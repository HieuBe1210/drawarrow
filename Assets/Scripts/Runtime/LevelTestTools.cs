using UnityEngine;

namespace DrawArrow
{
    public sealed partial class GameApp
    {
        bool LevelToolsOpen
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return LevelTestToolsSettings.Enabled&&levelToolsOpen;
#else
                return false;
#endif
            }
        }
        void CloseLevelTestTools()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            levelToolsOpen=false;
#endif
        }
        void DrawLevelTestTools()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(!LevelTestToolsSettings.Enabled){levelToolsOpen=false;return;}
            if(!levelToolsOpen)
            {
                if(modalHelp||paused||won||failed||(page!=Page.Home&&page!=Page.Game))return;
                var button=page==Page.Home?new Rect(32,1815,245,68):new Rect(48,175,245,64);
                if(Ink.Button(button,"Test tools",Ink.Hex("346D91"),Color.white,30))
                {CancelArrowPress();levelToolsOpen=true;levelTestInput=(Progress.levels[0]+1).ToString();levelTestError="";}
                return;
            }
            Ink.Rect(page==Page.Game?gameViewport:new Rect(0,0,Width,Height),new Color(0,0,0,.65f));
            Ink.Card(new Rect(80,550,920,760),OriginalArt.Background,28);
            Ink.TextAt(new Rect(130,590,820,70),"Test level chính",48,FG,TextAnchor.MiddleCenter,true);
            Ink.TextAt(new Rect(140,690,800,55),"Level (1 – "+Repository.Count(GameMode.Classic)+")",32,FG);
            if(levelTestInputStyle==null)levelTestInputStyle=new GUIStyle(GUI.skin.textField){fontSize=42,alignment=TextAnchor.MiddleLeft};
            levelTestInput=GUI.TextField(new Rect(140,755,510,90),levelTestInput,9,levelTestInputStyle);
            if(Ink.Button(new Rect(680,755,260,90),"Play",OriginalArt.Accent,Color.white,38))TryJumpToMainLevel(levelTestInput);
            if(!string.IsNullOrEmpty(levelTestError))Ink.TextAt(new Rect(140,855,800,70),levelTestError,28,Ink.Red);
            if(Ink.Button(new Rect(140,955,800,90),"Xóa tiến trình level chính",Ink.Hex("A84E39"),Color.white,34))ResetMainLevelProgress();
            Ink.TextAt(new Rect(140,1065,800,75),"Xóa tiến trình sẽ đưa về level 1. Giữ coin, âm thanh, rung và các chế độ khác.",28,FG,TextAnchor.MiddleCenter);
            if(Ink.Button(new Rect(340,1180,400,80),"Đóng",Ink.Hex("6F767A"),Color.white,34))CloseLevelTestTools();
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool levelToolsOpen;
        string levelTestInput="1",levelTestError="";
        GUIStyle levelTestInputStyle;
        bool TryJumpToMainLevel(string input)
        {
            if(!LevelTestToolsSettings.Enabled)return false;
            int count=Repository.Count(GameMode.Classic);
            if(!int.TryParse(input,out int number)||number<1||number>count)
            {levelTestError="Nhập số level từ 1 đến "+count+".";return false;}
            Progress.levels[(int)GameMode.Classic]=number-1;SaveStore.Save(Progress);
            CloseLevelTestTools();StartGame(GameMode.Classic,number-1);return true;
        }
        void ResetMainLevelProgress()
        {
            if(!LevelTestToolsSettings.Enabled)return;
            Progress.levels[(int)GameMode.Classic]=0;Progress.mainLevelsCompleted=0;ReturnHome();
        }
#endif
    }
}
