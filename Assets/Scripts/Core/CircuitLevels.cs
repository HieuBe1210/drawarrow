using System;
using UnityEngine;

namespace DrawArrow
{
    public static class CircuitLevels
    {
        public const int CampaignCount = 22543;
        public const int MainSetCount = 6540;
        const int FirstPackCount = 40;
        const int AdditionalFirstIndex = 540;
        const int AdditionalShardSize = 200;
        const string FullSetResource = "Circuit/LevelSet1-All";
        static ImportedLevelPack fullSet;
        static bool fullSetChecked;
        static ImportedLevelPack importedPack;
        static string loadedResource;

        public static int Count(ClassicLevelSet set) =>
            set == ClassicLevelSet.Current ? MainSetCount : CampaignCount;

        [Serializable]
        sealed class ImportedLevelPack
        {
            public LevelData[] levels;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetImportedLevels()
        {
            fullSet = null;
            fullSetChecked = false;
            importedPack = null;
            loadedResource = null;
        }

        static void LoadFullSet()
        {
            fullSetChecked = true;
            var asset = Resources.Load<TextAsset>(FullSetResource);
            if (asset == null) return;

            try
            {
                var pack = JsonUtility.FromJson<ImportedLevelPack>(asset.text);
                if (pack?.levels == null || pack.levels.Length != MainSetCount)
                    throw new ArgumentException($"Expected {MainSetCount} entries in levels.");

                for (int i = 0; i < pack.levels.Length; i++)
                {
                    var level = pack.levels[i];
                    if (level == null || level.width <= 0 || level.height <= 0 ||
                        level.arrows == null || level.arrows.Length == 0)
                        throw new ArgumentException($"Invalid board at level {i + 1}.");

                    long cellCount = (long)level.width * level.height;
                    foreach (var arrow in level.arrows)
                    {
                        if (arrow?.points == null || arrow.points.Length < 2)
                            throw new ArgumentException($"Invalid arrow at level {i + 1}.");
                        foreach (int point in arrow.points)
                            if (point < 0 || point >= cellCount)
                                throw new ArgumentException($"Point outside the board at level {i + 1}.");
                    }
                }
                fullSet = pack;
            }
            catch (ArgumentException error)
            {
                Debug.LogError($"Invalid {FullSetResource}.json. Loading the original packs. {error.Message}");
            }
            finally
            {
                Resources.UnloadAsset(asset);
            }
        }

        static LevelData LoadImported(int number)
        {
            if (!fullSetChecked) LoadFullSet();
            if (fullSet != null) return fullSet.levels[number];

            int first = 0;
            int count = FirstPackCount;
            string resource = "Circuit/LevelSet1/0001-0040";
            if (number >= AdditionalFirstIndex)
            {
                first = AdditionalFirstIndex + (number - AdditionalFirstIndex) / AdditionalShardSize * AdditionalShardSize;
                count = Mathf.Min(AdditionalShardSize, MainSetCount - first);
                resource = $"Circuit/LevelSet1/{first + 1:D4}-{first + count:D4}";
            }
            else if (number >= FirstPackCount)
            {
                first = FirstPackCount;
                count = AdditionalFirstIndex - first;
                resource = "Circuit/LevelSet1-041-540";
            }
            if (loadedResource != resource)
            {
                var asset = Resources.Load<TextAsset>(resource);
                var pack = asset != null ? JsonUtility.FromJson<ImportedLevelPack>(asset.text) : null;
                if (pack?.levels == null || pack.levels.Length != count)
                    throw new InvalidOperationException("Missing or invalid campaign pack: " + resource);
                importedPack = pack;
                loadedResource = resource;
            }
            return importedPack.levels[number - first];
        }

        public static LevelData Load(GameMode mode, int index, ClassicLevelSet set = ClassicLevelSet.Current)
        {
            if (mode != GameMode.Classic) set = ClassicLevelSet.Current;
            int number = ((index % CampaignCount) + CampaignCount) % CampaignCount;
            if (mode == GameMode.Classic && set == ClassicLevelSet.Current)
            {
                if (index < 0 || index >= MainSetCount)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return LoadImported(index);
            }

            int seed = number * 71807 + ((int)mode + 1) * 7919 + (int)set * 1009;
            var random = new System.Random(seed);
            string id = "circuit-" + mode + "-" + (mode == GameMode.Classic ? (int)set + "-" : "") + (number + 1);

            if (mode == GameMode.LinkDots) return Links(id, number, random);
            if (mode == GameMode.OneLine) return Line(id, number, random);
            if (mode == GameMode.Arrows3D) return Cube(id, number, random);
            int target = Mathf.Min(120, 5 + Mathf.RoundToInt(Mathf.Sqrt(number) * .76f));
            if (mode == GameMode.Classic)
            {
                if (set == ClassicLevelSet.Original) target = Mathf.Min(92, target);
                if (set == ClassicLevelSet.Generated) target = Mathf.Min(138, target + Mathf.Min(number / 4, 18));
                if (number is 4 or 9 or 14 or 19) target += 3;
            }
            if (Modes.IsTimed(mode)) target = Mathf.Min(target, mode == GameMode.SharkRush ? 10 : 22);
            if (mode == GameMode.ArrowClash) target = Mathf.Max(6, target / 3 * 3);

            int width = Mathf.Max(6, Mathf.CeilToInt(Mathf.Sqrt(target * 8f)));
            int height = Mathf.CeilToInt(width * (set == ClassicLevelSet.Original ? 1.08f : 1.25f));
            int maxLength = Mathf.Clamp(6 + target / 9, 6, 16);
            Silhouette? shape = set == ClassicLevelSet.Generated ? new Silhouette(75, 100, 75) : null;
            var data = ClassicMilestoneLayout.GenerateBoard(id, seed, target, width, height, maxLength, silhouette: shape);

            if (mode == GameMode.ArrowClash)
            {
                int[] colors = { 0, 1, 2, 3, 4, 5, 6 };
                for (int i = colors.Length - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    (colors[i], colors[j]) = (colors[j], colors[i]);
                }
                for (int i = 0; i < target; i++) data.arrows[i].color = colors[(i / 3) % colors.Length];
            }
            if (mode == GameMode.OctaArrows)
            {
                int diagonalCount = data.width / 3;
                Array.Resize(ref data.arrows, target + diagonalCount);
                foreach (var arrow in data.arrows)
                    if (arrow != null) arrow.kind = "normal";
                for (int i = 0; i < diagonalCount; i++)
                {
                    int x = i * 3 + 1;
                    int dx = random.Next(2) == 0 ? -1 : 1;
                    data.arrows[target + i] = new ArrowData
                    {
                        points = new[] { (data.height + 1) * data.width + x, data.height * data.width + x - dx },
                        dx = dx, dy = 1, kind = "normal"
                    };
                }
                data.height += 2;
                data.moveLimit = data.arrows.Length + Mathf.Max(4, target / 3);
            }
            return data;
        }

        static LevelData Links(string id, int number, System.Random random)
        {
            int count = Mathf.Min(7, 3 + number / 3);
            bool transpose = random.Next(2) == 0;
            var pairs = new PairData[count];
            for (int i = 0; i < count; i++)
            {
                int row = i * 2 + 1;
                int left = random.Next(1, 4), right = random.Next(6, 10);
                pairs[i] = new PairData
                {
                    color = (i + number) % 7,
                    points = transpose
                        ? new[] { new PointData { x = row, y = left }, new PointData { x = row, y = right } }
                        : new[] { new PointData { x = left, y = row }, new PointData { x = right, y = row } }
                };
            }
            return new LevelData { id = id, width = transpose ? count * 2 : 10,
                height = transpose ? 10 : count * 2, pairs = pairs };
        }

        static LevelData Line(string id, int number, System.Random random)
        {
            int rows = Mathf.Min(7, 2 + number / 3);
            int columns = Mathf.Min(6, 3 + number / 5);
            var nodes = new PointData[rows * columns];
            var solution = new int[nodes.Length];
            var edges = new EdgeData[nodes.Length - 1];
            for (int i = 0; i < nodes.Length; i++)
            {
                int row = i / columns, offset = i % columns;
                int x = row % 2 == 0 ? offset : columns - 1 - offset;
                nodes[i] = new PointData { x = x + random.Next(-18, 19) * .01f,
                    y = row + random.Next(-18, 19) * .01f };
                solution[i] = i;
                if (i > 0) edges[i - 1] = new EdgeData { a = i - 1, b = i };
            }
            return new LevelData { id = id, nodes = nodes, edges = edges, solution = solution };
        }

        static LevelData Cube(string id, int number, System.Random random)
        {
            int size = Mathf.Min(9, 4 + number / 4);
            var arrows = new ArrowData[6 * (size - 2)];
            int cursor = 0;
            for (int face = 0; face < 6; face++)
            {
                bool reverse = random.Next(2) == 0;
                for (int row = 1; row < size - 1; row++)
                {
                    int span = random.Next(2, size);
                    int first = random.Next(size - span + 1);
                    var points = new int[span];
                    for (int i = 0; i < span; i++)
                    {
                        int lane = first + (reverse ? i : span - 1 - i);
                        int edge = face % 2 == 0 ? size - 1 : 0;
                        int x = face < 2 ? edge : lane;
                        int y = face < 2 ? row : face < 4 ? edge : row;
                        int z = face < 2 ? lane : face < 4 ? row : edge;
                        points[i] = (x + size * (y + size * z)) * 6 + face;
                    }
                    arrows[cursor++] = new ArrowData { points = points };
                }
            }
            return new LevelData { id = id, width = size, height = size, depth = size, arrows = arrows };
        }
    }
}
