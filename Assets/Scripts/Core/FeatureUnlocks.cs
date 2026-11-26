using UnityEngine;

namespace DrawArrow
{
    public static class FeatureUnlocks
    {
        public const int StarRaceLevel=11;
        public const int DailyLevel=21;
        public const int MoreGamesLevel=26;
        public static bool StarRaceUnlocked(ProgressData progress)=>Completed(progress)>=StarRaceLevel-1;
        public static bool DailyUnlocked(ProgressData progress)=>Completed(progress)>=DailyLevel-1;
        public static bool MoreGamesUnlocked(ProgressData progress)=>Completed(progress)>=MoreGamesLevel-1;
        static int Completed(ProgressData progress)=>progress.mainLevelsCompleted;
        public static int NewlyUnlockedLevel(ProgressData progress, int wonLevel)
        {
            int nextLevel = wonLevel + 1;
            return Completed(progress) < wonLevel && nextLevel is StarRaceLevel or DailyLevel or MoreGamesLevel
                ? nextLevel : 0;
        }
        public static void RecordMainWin(ProgressData progress,int completedLevel)
        {
            progress.mainLevelsCompleted=Mathf.Max(progress.mainLevelsCompleted,completedLevel);
        }
    }
}
