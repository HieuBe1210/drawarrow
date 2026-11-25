using UnityEngine;

namespace DrawArrow
{
    public static class FeatureUnlocks
    {
        public const int DailyLevel=11;
        public const int MoreGamesLevel=21;
        public static bool DailyUnlocked(ProgressData progress)=>Completed(progress)>=DailyLevel-1;
        public static bool MoreGamesUnlocked(ProgressData progress)=>Completed(progress)>=MoreGamesLevel-1;
        static int Completed(ProgressData progress)=>progress.mainLevelsCompleted;
        public static void RecordMainWin(ProgressData progress,int completedLevel)
        {
            progress.mainLevelsCompleted=Mathf.Max(progress.mainLevelsCompleted,completedLevel);
        }
    }
}
