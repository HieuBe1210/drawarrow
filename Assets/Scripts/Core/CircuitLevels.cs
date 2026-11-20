using System;
using System.Collections.Generic;
using UnityEngine;

namespace DrawArrow
{
    public static class CircuitLevels
    {
        public const int MainSetCount = 6540;
        const int SetZeroShardSize = 200;
        const int DifficultyWindow = 10;
        const int MinimumHardArrowIncrease = 35;
        static readonly int[] classicArrowCounts = new int[MainSetCount];
        static readonly byte[] classicDifficulty = new byte[MainSetCount];
        static ImportedLevelPack importedPack;
        static string loadedResource;
        static int[] dailyLevelOrder;

        [Serializable]
        sealed class ImportedLevelPack
        {
            public LevelData[] levels;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetImportedLevels()
        {
            importedPack = null;
            loadedResource = null;
            dailyLevelOrder = null;
            Array.Clear(classicArrowCounts, 0, classicArrowCounts.Length);
            Array.Clear(classicDifficulty, 0, classicDifficulty.Length);
        }

        static LevelData LoadSetZero(int index)
        {
            int first = index / SetZeroShardSize * SetZeroShardSize;
            int count = Mathf.Min(SetZeroShardSize, MainSetCount - first);
            string resource = $"Circuit/LevelSet0/{first + 1:D4}-{first + count:D4}";
            if (loadedResource != resource)
            {
                var asset = Resources.Load<TextAsset>(resource);
                var pack = asset != null ? JsonUtility.FromJson<ImportedLevelPack>(asset.text) : null;
                if (asset != null) Resources.UnloadAsset(asset);
                if (pack?.levels == null || pack.levels.Length != count)
                    throw new InvalidOperationException("Missing or invalid campaign pack: " + resource);
                importedPack = pack;
                loadedResource = resource;
                for (int i = 0; i < count; i++)
                    classicArrowCounts[first + i] = pack.levels[i].arrows.Length;
            }
            return importedPack.levels[index - first];
        }

        public static bool IsHardMainLevel(int index)
        {
            if ((uint)index >= MainSetCount) return false;
            if (classicDifficulty[index] != 0) return classicDifficulty[index] == 2;

            int count = ClassicArrowCount(index);
            int first = Mathf.Max(0, index - DifficultyWindow);
            int last = Mathf.Min(MainSetCount - 1, index + DifficultyWindow);
            var neighbors = new int[last - first];
            int n = 0;
            for (int i = first; i <= last; i++)
                if (i != index) neighbors[n++] = ClassicArrowCount(i);
            Array.Sort(neighbors);
            float median = (neighbors[(n - 1) / 2] + neighbors[n / 2]) * .5f;
            // Require both a 1.8x jump and a meaningful absolute increase.
            bool hard = count * 5 >= median * 9 && count >= median + MinimumHardArrowIncrease;
            classicDifficulty[index] = hard ? (byte)2 : (byte)1;
            return hard;
        }

        static int ClassicArrowCount(int index)
        {
            if (classicArrowCounts[index] == 0) LoadSetZero(index);
            return classicArrowCounts[index];
        }

        public static int DailyLevelIndex(DateTime date)
        {
            if (dailyLevelOrder == null)
            {
                var eligible = new List<int>();
                for (int i = 0; i < MainSetCount; i++)
                    if (ClassicArrowCount(i) > 170) eligible.Add(i);
                if (eligible.Count == 0)
                    throw new InvalidOperationException("Daily Challenge requires a Classic level with more than 170 arrows.");

                // A fixed shuffle gives every device the same board for the same local date.
                uint state = 0x27934AB1;
                for (int i = eligible.Count - 1; i > 0; i--)
                {
                    state = unchecked(state * 1664525 + 1013904223);
                    int other = (int)(state % (uint)(i + 1));
                    (eligible[i], eligible[other]) = (eligible[other], eligible[i]);
                }
                dailyLevelOrder = eligible.ToArray();
            }
            long day = date.Date.Ticks / TimeSpan.TicksPerDay;
            return dailyLevelOrder[(int)(day % dailyLevelOrder.Length)];
        }

        public static bool SupportsMode(GameMode mode) => mode == GameMode.Classic || mode == GameMode.Daily;

        public static LevelData Load(GameMode mode, int index)
        {
            if (!SupportsMode(mode))
                throw new ArgumentOutOfRangeException(nameof(mode));

            if ((uint)index >= MainSetCount)
                throw new ArgumentOutOfRangeException(nameof(index));

            return LoadSetZero(index);
        }
    }
}
