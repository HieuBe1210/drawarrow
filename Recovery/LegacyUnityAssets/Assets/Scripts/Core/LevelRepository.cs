using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace DrawArrow
{
    [Serializable] public class LevelPack { public LevelData[] levels; }
    [Serializable] public class CatalogGroup { public string name; public int count, packSize; }
    [Serializable] public class ModeSequence { public string mode,group; public int[] indices; }
    [Serializable] public class LevelOverride { public string mode,group; public int index,levelIndex; }
    [Serializable] public class Catalog { public CatalogGroup[] groups; public ModeSequence[] sequences; public LevelOverride[] overrides; public int[] classicShardOrder; }

    public sealed class LevelRepository
    {
        const int OriginalLevel304Index = 303;
        const int OriginalLevel304PackIndex = 16962;
        const int ClassicIntroCount = 40;
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
            return LoadCatalogLevel(mode,((index%count)+count)%count);
        }
        public LevelData LoadPlayable(GameMode mode, int index)
        {
            if (mode != GameMode.Classic) return Load(mode, index);
            int sourceIndex = NormalizePlayableClassicIndex(index);
            return sourceIndex < ClassicIntroCount
                ? LoadCatalogLevel(mode, sourceIndex)
                : Load("classic", catalog.classicShardOrder[sourceIndex - ClassicIntroCount]);
        }
        public int PlayableClassicCount => ClassicIntroCount + catalog.classicShardOrder.Length;

        public bool UsesOriginalClassicLayout(GameMode mode, int index)
            => mode == GameMode.Classic && NormalizePlayableClassicIndex(index) >= ClassicIntroCount;

        int NormalizePlayableClassicIndex(int index)
        {
            int count = PlayableClassicCount;
            return ((index % count) + count) % count;
        }

        int NormalizeClassicIndex(int index)
        {
            int count = Count("classic");
            return ((index % count) + count) % count;
        }

        public LevelData LoadOriginalClassic(int index)
        {
            index = NormalizeClassicIndex(index);
            // The original project sorted the asset pool by complexity.
            // Swap these entries to restore the confirmed level 304 position.
            if (index == OriginalLevel304Index) index = OriginalLevel304PackIndex;
            else if (index == OriginalLevel304PackIndex) index = OriginalLevel304Index;
            return Load("classic", index);
        }
        LevelData LoadCatalogLevel(GameMode mode, int index)
        {
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

}
