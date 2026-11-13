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
        public ArrowData[] arrows; public TurnData[] turns; public PortalData[] portals;
        public PairData[] pairs; public PointData[] nodes; public EdgeData[] edges; public int[] solution;
    }
    [Serializable] public class LevelPack { public LevelData[] levels; }
    [Serializable] public class CatalogGroup { public string name; public int count, packSize; }
    [Serializable] public class ModeSequence { public string mode,group; public int[] indices; }
    [Serializable] public class Catalog { public CatalogGroup[] groups; public ModeSequence[] sequences; }

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
        public LevelData Load(GameMode mode, int index) {var sequence=Sequence(mode);if(sequence!=null)return Load(sequence.group,sequence.indices[((index%sequence.indices.Length)+sequence.indices.Length)%sequence.indices.Length]);return Load(Group(mode),index);}
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
        public int[] levels = new int[20]; public int coins = 250, wins, streak, bestStreak, hintsUsed;
        public bool sound = true, vibration = true, dark; public string lastDaily = "";
        public List<string> dailyWins = new List<string>();
    }
    public static class SaveStore
    {
        const string Key = "drawarrow.progress.v1";
        public static ProgressData Load()
        {
            try { var p = JsonUtility.FromJson<ProgressData>(PlayerPrefs.GetString(Key,"")); if(p != null && p.levels != null && p.levels.Length == 20) { if(p.dailyWins == null)p.dailyWins = new List<string>(); return p; } } catch(Exception) { }
            return new ProgressData();
        }
        public static void Save(ProgressData p) { PlayerPrefs.SetString(Key,JsonUtility.ToJson(p)); PlayerPrefs.Save(); }
    }
}
