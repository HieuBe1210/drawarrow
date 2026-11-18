using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace DrawArrow
{
    public enum GameMode { Classic, ArrowClash, Arrows3D, OctaArrows, LinkDots, OneLine, Schulte, OneLife, TimeAttack, Countdown, Reasoning, CatEscape, Battle, SharkRush, Daily, Pipeline, Tarot, Zodiac, Zoo, Gallery }

    public static class Modes
    {
        public static readonly string[] Names = { "Arrow Out", "Arrow Clash", "Arrows 3D", "Octa Arrows", "Link Dots", "One Line", "Schulte", "One Life", "Time Attack", "Countdown", "Reasoning", "Cat Escape", "Battle", "Shark Rush", "Daily Challenge", "Pipeline", "Tarot", "Zodiac", "Zoo", "Gallery" };
        public static readonly string[] Descriptions = { "Clear every arrow. Find the way out.", "Free arrows. Match three colors.", "Turn the shape. Discover a clear path.", "Eight directions. Portals and turn tiles.", "Connect matching dots without crossing.", "Draw every edge in one continuous line.", "Find the numbers in ascending order.", "A single mistake ends the challenge.", "Clear the board before time runs out.", "Remove marked arrows before they expire.", "Remember the paths. Reveal hidden arrows.", "Guide the little cats to freedom.", "Race a local computer opponent.", "Keep moving to escape the shark.", "A new puzzle for each calendar day.", "Untangle winding paths and clear the board.", "Clear puzzles to reveal your tarot collection.", "Discover the twelve constellations.", "Solve puzzles and collect animal badges.", "Clear shaped boards to reveal the artwork." };
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
    [Serializable] public class LevelData
    {
        public string id, category, source; public int width, height, depth, moveLimit;
        public float[] initialView;
        public float arrowVisualScale;
        public float[] packedFitBounds;
        public ArrowData[] arrows; public TurnData[] turns; public PortalData[] portals;
        public PairData[] pairs; public PointData[] nodes; public EdgeData[] edges; public int[] solution;
    }
    [Serializable] public class LevelPack { public LevelData[] levels; }
    [Serializable] public class CatalogGroup { public string name; public int count, packSize; }
    [Serializable] public class ModeSequence { public string mode,group; public int[] indices; }
    [Serializable] public class LevelOverride { public string mode,group; public int index,levelIndex; }
    [Serializable] public class Catalog { public CatalogGroup[] groups; public ModeSequence[] sequences; public LevelOverride[] overrides; }

    public sealed class LevelRepository
    {
        readonly Catalog catalog; readonly Dictionary<string,LevelPack> cache = new Dictionary<string,LevelPack>();
        public LevelRepository()
        {
            var text = Resources.Load<TextAsset>("Levels/catalog");
            if (!text) throw new InvalidDataException("Missing recovered level catalog. See Tools/package_levels.py.");
            catalog = JsonUtility.FromJson<Catalog>(text.text);
        }
        public string Group(GameMode mode) => mode == GameMode.ArrowClash ? "clash" : mode == GameMode.Arrows3D ? "cube" : mode == GameMode.OctaArrows ? "octa" : mode == GameMode.LinkDots ? "links" : mode == GameMode.OneLine ? "line" : "classic";
        ModeSequence Sequence(GameMode mode){if(catalog.sequences!=null)foreach(var sequence in catalog.sequences)if(sequence.mode==mode.ToString())return sequence;return null;}
        public int Count(GameMode mode) {var sequence=Sequence(mode);return sequence!=null?sequence.indices.Length:Count(Group(mode));}
        public int Count(string name) { foreach(var g in catalog.groups) if(g.name == name) return g.count; return 0; }
        public LevelData Load(GameMode mode, int index)
        {
            int count=Count(mode);if(count<=0)throw new InvalidDataException("No levels for "+mode);
            index=((index%count)+count)%count;
            if(catalog.overrides!=null)foreach(var replacement in catalog.overrides)
                if(replacement.mode==mode.ToString()&&replacement.index==index)return Load(replacement.group,replacement.levelIndex);
            var sequence=Sequence(mode);return sequence!=null?Load(sequence.group,sequence.indices[index]):Load(Group(mode),index);
        }
        public LevelData Load(string group, int index)
        {
            CatalogGroup config = null; foreach(var g in catalog.groups) if(g.name == group) config = g;
            if(config == null || config.count == 0) throw new InvalidDataException("No levels in " + group);
            index = ((index % config.count) + config.count) % config.count;
            var key = group + "_" + (index/config.packSize).ToString("000");
            if(!cache.TryGetValue(key,out var pack))
            {
                var asset = Resources.Load<TextAsset>("Levels/" + key);
                if(!asset) throw new InvalidDataException("Missing level pack " + key);
                using(var input = new MemoryStream(asset.bytes))
                using(var unzip = new GZipStream(input,CompressionMode.Decompress))
                using(var reader = new StreamReader(unzip,Encoding.UTF8)) pack = JsonUtility.FromJson<LevelPack>(reader.ReadToEnd());
                if(cache.Count >= 4) cache.Clear(); cache[key] = pack;
                Resources.UnloadAsset(asset);
            }
            // Copy mutable fields; gameplay must never modify cached recovered data.
            return JsonUtility.FromJson<LevelData>(JsonUtility.ToJson(pack.levels[index%config.packSize]));
        }
    }

    [Serializable] public class ProgressData
    {
        public int[] levels = new int[20]; public int coins = 250, wins, streak, bestStreak, hintsUsed,mainLevelsCompleted;
        public int freeHints=3,hintEconomyVersion=2;
        public bool sound = true, vibration = true, dark,hasStartedGame; public string lastDaily = "";
        public List<string> dailyWins = new List<string>();
    }
    public static class SaveStore
    {
        const string Key = "drawarrow.progress.v1";
        public static ProgressData Load()
        {
            try
            {
                string json=PlayerPrefs.GetString(Key,"");var p=JsonUtility.FromJson<ProgressData>(json);
                if(p!=null&&p.levels!=null&&p.levels.Length==20)
                {
                    if(p.dailyWins==null)p.dailyWins=new List<string>();
                    // Older saves used the next main-level index as their completion marker.
                    if(!json.Contains("\"mainLevelsCompleted\""))p.mainLevelsCompleted=Mathf.Max(0,p.levels[0]);
                    if(!json.Contains("\"hasStartedGame\""))p.hasStartedGame=true;
                    if(!json.Contains("\"freeHints\"")){p.freeHints=3;p.hintEconomyVersion=2;}
                    else if(!json.Contains("\"hintEconomyVersion\""))p.hintEconomyVersion=1;
                    HintEconomy.UpgradeSave(p);
                    p.freeHints=Mathf.Max(0,p.freeHints);
                    return p;
                }
            }
            catch(Exception) { }
            return new ProgressData();
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
            if(mode!=GameMode.Classic||completedLevel<5||completedLevel%5!=0)return 0;
            progress.freeHints++;return 1;
        }
    }
}
