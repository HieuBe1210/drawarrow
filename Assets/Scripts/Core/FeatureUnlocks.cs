using UnityEngine;

namespace DrawArrow
{
    public static class FeatureUnlocks
    {
        public const int DailyLevel=11,MoreGamesLevel=16;
        public static bool DailyUnlocked(ProgressData progress)=>progress.mainLevelsCompleted>=DailyLevel-1;
        public static bool MoreGamesUnlocked(ProgressData progress)=>progress.mainLevelsCompleted>=MoreGamesLevel-1;
        public static void RecordMainWin(ProgressData progress,int completedLevel)
        {progress.mainLevelsCompleted=Mathf.Max(progress.mainLevelsCompleted,completedLevel);}
    }
}
