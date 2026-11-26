using System;
using System.Collections.Generic;
using UnityEngine;

namespace DrawArrow
{
    public enum GameMode { Classic, ArrowClash, Arrows3D, OctaArrows, LinkDots, OneLine, Schulte, OneLife, TimeAttack, Countdown, Reasoning, CatEscape, Battle, SharkRush, Daily, Pipeline, Tarot, Zodiac, Zoo, Gallery, ArrowSwitch, ArrowWorkshop, ArrowChameleon }

    public static class Modes
    {
        public static readonly string[] Names = { "Arrow Tidy", "Arrow Clash", "Arrows 3D", "Octa Arrows", "Link Dots", "One Line", "Schulte", "One Life", "Time Attack", "Countdown", "Reasoning", "Cat Escape", "Battle", "Shark Rush", "Daily Challenge", "Pipeline", "Tarot", "Zodiac", "Zoo", "Gallery", "Arrow Switch", "Arrow Workshop", "Arrow Chameleon" };
        public static readonly string[] Descriptions = { "Clear every arrow. Find the way out.", "Free arrows. Match three colors.", "Turn the shape. Discover a clear path.", "Eight directions. Portals and turn tiles.", "Connect matching dots without crossing.", "Draw every edge in one continuous line.", "Find the numbers in ascending order.", "A single mistake ends the challenge.", "Clear the board before time runs out.", "Remove marked arrows before they expire.", "Remember the paths. Reveal hidden arrows.", "Guide the little cats to freedom.", "Race a local computer opponent.", "Keep moving to escape the shark.", "A new puzzle for each calendar day.", "Untangle winding paths and clear the board.", "Clear puzzles to reveal your tarot collection.", "Discover the twelve constellations.", "Solve puzzles and collect animal badges.", "Clear shaped boards to reveal the artwork.", "Send an arrow through the switch to swap the gates.", "Turn arrows in place to open a path with limited rotations.", "Clear arrows to change nearby colors. Match each exit." };
        public static string Name(GameMode m) => Names[(int)m];
        public static bool IsArrow(GameMode m) => m != GameMode.LinkDots && m != GameMode.OneLine && m != GameMode.Schulte && m != GameMode.OctaArrows;
        public static bool IsTimed(GameMode m) => m == GameMode.TimeAttack || m == GameMode.SharkRush || m == GameMode.Battle;
    }

    [Serializable] public class ArrowData { public int[] points; public int color, special; public string param, kind; public int dx, dy, dx2, dy2; public bool open; }
    [Serializable] public class TurnData { public int x, y, dx, dy; }
    [Serializable] public class PortalData { public int a, b; }
    [Serializable] public class PointData { public float x, y; public Vector2 V => new Vector2(x,y); }
    [Serializable] public class PairData { public int color; public PointData[] points; }
    [Serializable] public class EdgeData { public int a, b; public bool curved; public float cx, cy; }
    [Serializable] public struct SwitchGateSpan { public int start, end; }
    [Serializable] public class SwitchGateData
    {
        public int pad;
        public SwitchGateSpan initiallyOpen, initiallyClosed;
    }
    [Serializable] public class LevelData
    {
        public string id, category, source; public int width, height, depth, moveLimit;
        public float[] initialView;
        public float arrowVisualScale;
        public float[] packedFitBounds;
        public bool followDiagonalPaths, keepBoardOrientation;
        public ArrowData[] arrows; public TurnData[] turns; public PortalData[] portals;
        public PairData[] pairs; public PointData[] nodes; public EdgeData[] edges; public int[] solution;
    }
    [Serializable] public sealed class ArrowSwitchLevelData : LevelData
    {
        public SwitchGateData switchGates;
        public int sourceMainLevel;
        public int tutorialClearArrow, tutorialSwitchArrow;
    }
    [Serializable] public sealed class ArrowWorkshopLevelData : LevelData
    {
        public int rotateBudget, tutorialArrow;
    }
    [Serializable] public sealed class ArrowChameleonLevelData : LevelData
    {
        public int[] gateCells, gateColors;
    }
    [Serializable] public class ProgressData
    {
        public int[] levels = new int[Modes.Names.Length]; public int coins = 250, wins, streak, bestStreak, hintsUsed,mainLevelsCompleted;
        public int SelectedClassicIndex
        {
            get => levels[(int)GameMode.Classic];
            set => levels[(int)GameMode.Classic] = value;
        }
        public int freeHints=3,hintEconomyVersion=2;
        public bool noAdsBundleHintsGranted;
        public bool sound = true, vibration = true, dark,hasStartedGame; public string lastDaily = "";
        public List<string> dailyWins = new List<string>();
        public List<DailyTime> dailyTimes = new List<DailyTime>();
        public byte[] classicBestStars = new byte[CircuitLevels.MainSetCount];
        public List<StarRaceWeek> starRaceWeeks = new List<StarRaceWeek>();
        public int[] rankedLevels = new int[Modes.Names.Length];
        public string rankedDailyDate;
        public int rankedDailyMilliseconds;
        public string playerName;
        public int avatarIndex;
    }

    [Serializable]
    public sealed class StarRaceWeek
    {
        public string monday;
        public int stars;
    }

    public static class StarRace
    {
        public static DateTime WeekStart(DateTime utc) => utc.Date.AddDays(-((int)utc.DayOfWeek + 6) % 7);
        public static string WeekKey(DateTime utc) => WeekStart(utc).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        public static int StarsFromHearts(int hearts) => Mathf.Clamp(hearts, 0, 3);
        public static readonly int[] Milestones = { 10, 25, 50 };

        public static int RecordClassicWin(ProgressData progress, int index, int stars, DateTime utc)
        {
            int improvement = stars - progress.classicBestStars[index];
            if (improvement <= 0) return 0;
            progress.classicBestStars[index] = (byte)stars;
            if (!FeatureUnlocks.StarRaceUnlocked(progress)) return 0;
            string key = WeekKey(utc);
            var week = progress.starRaceWeeks.Find(item => item.monday == key);
            if (week == null)
            {
                week = new StarRaceWeek { monday = key };
                progress.starRaceWeeks.Add(week);
            }
            int previousStars = week.stars;
            week.stars += improvement;
            int reward = 0;
            foreach (int milestone in Milestones)
                if (previousStars < milestone && week.stars >= milestone) reward++;
            progress.freeHints += reward;
            return reward;
        }
    }

    [Serializable]
    public sealed class DailyTime
    {
        public string date;
        public int milliseconds;
    }

    public static class DailyChallengeResults
    {
        public static bool RecordWin(ProgressData progress, string date, int milliseconds)
        {
            bool first = progress.lastDaily != date && !progress.dailyWins.Contains(date);
            if (first)
            {
                progress.dailyWins.Add(date);
                progress.freeHints++;
            }

            progress.lastDaily = date;
            var best = progress.dailyTimes.Find(record => record.date == date);
            if (best == null)
                progress.dailyTimes.Add(new DailyTime { date = date, milliseconds = milliseconds });
            else if (milliseconds < best.milliseconds)
                best.milliseconds = milliseconds;
            return first;
        }

        public static int BestTime(ProgressData progress, string date)
            => progress.dailyTimes.Find(record => record.date == date)?.milliseconds ?? 0;
    }
    public static class SaveStore
    {
        const string Key = "drawarrow.progress.v1";
        static readonly string[] DefaultNameAdjectives = { "Swift", "Bright", "Lucky", "Quiet", "Clever", "Sunny" };
        static readonly string[] DefaultNameAnimals = { "Fox", "Panda", "Otter", "Owl", "Gecko", "Rabbit" };

        static string CreateDefaultPlayerName()
        {
            var random = new System.Random();
            string adjective = DefaultNameAdjectives[random.Next(DefaultNameAdjectives.Length)];
            string animal = DefaultNameAnimals[random.Next(DefaultNameAnimals.Length)];
            return $"{adjective}{animal}{random.Next(1000, 10000)}";
        }

        [Serializable]
        sealed class LegacyLevelSets
        {
            public bool useLevelSet0, useLevelSet2;
            public int levelSet0Progress, levelSet0Completed, levelSet2Progress, levelSet2Completed;
        }

        static void RestoreClassicProgress(string json, ProgressData progress)
        {
            var legacy = JsonUtility.FromJson<LegacyLevelSets>(json);
            if (legacy.useLevelSet0 || legacy.levelSet0Progress > 0 || legacy.levelSet0Completed > 0)
                progress.SelectedClassicIndex = legacy.levelSet0Progress;
            else if (legacy.useLevelSet2)
                progress.SelectedClassicIndex = legacy.levelSet2Progress;

            progress.SelectedClassicIndex = Mathf.Clamp(progress.SelectedClassicIndex, 0, CircuitLevels.MainSetCount);
            progress.mainLevelsCompleted = Mathf.Clamp(
                Mathf.Max(progress.mainLevelsCompleted, legacy.levelSet0Completed, legacy.levelSet2Completed),
                0, CircuitLevels.MainSetCount);
        }

        public static ProgressData Load()
        {
            try
            {
                string json=PlayerPrefs.GetString(Key,"");var p=JsonUtility.FromJson<ProgressData>(json);
                if(p!=null&&p.levels!=null&&p.levels.Length>=20&&p.levels.Length<=Modes.Names.Length)
                {
                    if(p.levels.Length!=Modes.Names.Length)Array.Resize(ref p.levels,Modes.Names.Length);
                    if(p.dailyWins==null)p.dailyWins=new List<string>();
                    if(p.dailyTimes==null)p.dailyTimes=new List<DailyTime>();
                    if(p.classicBestStars?.Length!=CircuitLevels.MainSetCount)
                        Array.Resize(ref p.classicBestStars,CircuitLevels.MainSetCount);
                    if(p.starRaceWeeks==null)p.starRaceWeeks=new List<StarRaceWeek>();
                    if(p.rankedLevels?.Length!=Modes.Names.Length)
                        Array.Resize(ref p.rankedLevels,Modes.Names.Length);
                    // Older saves used the next main-level index as their completion marker.
                    if(!json.Contains("\"mainLevelsCompleted\""))p.mainLevelsCompleted=Mathf.Max(0,p.levels[0]);
                    RestoreClassicProgress(json,p);
                    if(!json.Contains("\"hasStartedGame\""))p.hasStartedGame=true;
                    if(!json.Contains("\"freeHints\"")){p.freeHints=3;p.hintEconomyVersion=2;}
                    else if(!json.Contains("\"hintEconomyVersion\""))p.hintEconomyVersion=1;
                    HintEconomy.UpgradeSave(p);
                    p.freeHints=Mathf.Max(0,p.freeHints);
                    if (string.IsNullOrWhiteSpace(p.playerName))
                    {
                        p.playerName = CreateDefaultPlayerName();
                        Save(p);
                    }
                    return p;
                }
            }
            catch(Exception) { }
            return new ProgressData { playerName = CreateDefaultPlayerName() };
        }
        public static void Save(ProgressData p) { PlayerPrefs.SetString(Key,JsonUtility.ToJson(p)); PlayerPrefs.Save(); }
    }
    // A result belongs to the level that produced it. Consume it only once,
    // outside OnGUI, even if multiple input events request continuation.
    public sealed class LevelResultTransition
    {
        GameMode mode;int index;bool won,available,pending;
        public bool Pending=>pending;
        public void Clear(){available=pending=false;}
        public void Begin(GameMode resultMode,int resultIndex,bool victory)
        {mode=resultMode;index=resultIndex;won=victory;available=true;pending=false;}
        public bool Request()
        {if(!available||pending)return false;pending=true;return true;}
        public bool Take(out GameMode nextMode,out int nextIndex)
        {
            nextMode=mode;nextIndex=index;
            if(!pending)return false;
            if(won&&mode!=GameMode.Daily)nextIndex=index+1;
            Clear();return true;
        }
    }
    public static class HintEconomy
    {
        public static bool Unlimited(GameMode mode,int levelIndex)=>mode==GameMode.Classic&&levelIndex<4;
        public static void UpgradeSave(ProgressData progress)
        {
            if(progress.hintEconomyVersion>=2)return;
            // Change the original reserve from five to three once, accounting
            // for hints already spent or earned instead of resetting the balance.
            progress.freeHints=Mathf.Max(0,progress.freeHints-2);
            progress.hintEconomyVersion=2;
        }
        public static bool SpendFree(ProgressData progress,GameMode mode,int levelIndex)
        {
            if(Unlimited(mode,levelIndex))return true;
            if(progress.freeHints<=0)return false;
            progress.freeHints--;return true;
        }
        public static int RewardWin(ProgressData progress,GameMode mode,int completedLevel)
        {
            if(mode!=GameMode.Classic||completedLevel<10||completedLevel%10!=0)return 0;
            progress.freeHints++;return 1;
        }
    }
}
