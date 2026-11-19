using UnityEngine;

namespace DrawArrow
{
    public static class ClassicLevelSet2
    {
        public const int Count = 22543;

        // Quantiles of arrow counts and median board sizes from the shipped
        // asset pool, ordered by arrow count. Each board uses a fresh seed.
        static readonly (int index, int arrows, int width, int height)[] Profiles =
        {
            (40, 30, 15, 20),
            (1127, 31, 15, 20),
            (2254, 36, 16, 23),
            (5636, 50, 19, 27),
            (11271, 74, 24, 33),
            (16907, 128, 34, 41),
            (20288, 170, 37, 48),
            (21415, 188, 38, 50),
            (22317, 224, 40, 49),
            (Count - 1, 224, 42, 53)
        };

        public static LevelData Load(int index)
        {
            index = Mathf.Clamp(index, 0, Count - 1);
            string id = "drawarrow-set2-" + (index + 1);
            int seed = unchecked(92821 * index + 21713);
            if (index < ClassicLevelSet2Guide.Profiles.Length)
            {
                var guide = ClassicLevelSet2Guide.Profiles[index];
                return ClassicMilestoneLayout.GenerateBoard(id, seed, guide.lengths.Length,
                    guide.width, guide.height, 0, guide.lengths, guide.silhouette);
            }
            int upper = 1;
            while (index > Profiles[upper].index) upper++;
            var a = Profiles[upper - 1];
            var b = Profiles[upper];
            float t = Mathf.InverseLerp(a.index, b.index, index);
            int target = Mathf.RoundToInt(Mathf.Lerp(a.arrows, b.arrows, t));
            int width = Mathf.RoundToInt(Mathf.Lerp(a.width, b.width, t));
            int height = Mathf.RoundToInt(Mathf.Lerp(a.height, b.height, t));
            int maxLength = Mathf.Clamp(8 + target / 8, 8, 14);
            return ClassicMilestoneLayout.GenerateBoard(id, seed, target, width, height, maxLength);
        }
    }
}
