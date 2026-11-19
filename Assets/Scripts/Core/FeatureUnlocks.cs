using UnityEngine;

namespace DrawArrow
{
    public static class FeatureUnlocks
    {
        public const int DailyLevel=11,MoreGamesLevel=16;
        public static bool DailyUnlocked(ProgressData progress)=>Completed(progress)>=DailyLevel-1;
        public static bool MoreGamesUnlocked(ProgressData progress)=>Completed(progress)>=MoreGamesLevel-1;
        static int Completed(ProgressData progress)=>
            Mathf.Max(progress.mainLevelsCompleted,Mathf.Max(progress.levelSet0Completed,progress.levelSet2Completed));
        public static void RecordMainWin(ProgressData progress,int completedLevel)
        {
            if(progress.SelectedLevelSet==ClassicLevelSet.Original)
                progress.levelSet0Completed=Mathf.Max(progress.levelSet0Completed,completedLevel);
            else if(progress.SelectedLevelSet==ClassicLevelSet.Generated)
                progress.levelSet2Completed=Mathf.Max(progress.levelSet2Completed,completedLevel);
            else progress.mainLevelsCompleted=Mathf.Max(progress.mainLevelsCompleted,completedLevel);
        }
    }
}
