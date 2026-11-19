using System;
using System.Collections.Generic;
using UnityEngine;

namespace DrawArrow
{
    internal readonly struct Silhouette
    {
        readonly int bottom, middle, top;

        internal Silhouette(int bottom, int middle, int top)
        {
            this.bottom = bottom;
            this.middle = middle;
            this.top = top;
        }

        internal bool Contains(Vector2Int point, int width, int height)
        {
            float y = 2f * point.y / Mathf.Max(height - 1, 1);
            float percent = y <= 1 ? Mathf.Lerp(bottom, middle, y) : Mathf.Lerp(middle, top, y - 1);
            float span = width * percent / 100f;
            return Mathf.Abs(point.x - (width - 1) * .5f) < span * .5f;
        }
    }

    internal static class ClassicMilestoneLayout
    {
        const int SamplesPerArrow = 64;
        static readonly Vector2Int[] Directions =
            { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };

        internal static LevelData GenerateBoard(string id, int seed, int target, int width, int height,
            int maxLength, int[] requiredLengths = null, Silhouette? silhouette = null, bool longRuns = false)
        {
            int originalWidth = width;
            int originalHeight = height;
            int attempt = 0;
            while (true)
            {
                int boardSeed = unchecked(seed + attempt * 104729);
                var arrows = new BoardBuilder(width, height, boardSeed, silhouette, longRuns).Build(target, maxLength, requiredLengths);
                if (arrows != null)
                {
                    return new LevelData
                    {
                        id = id,
                        width = width,
                        height = height,
                        arrows = arrows,
                        packedFitBounds = new[] { (float)(width - 1), (float)(height - 1) }
                    };
                }
                if (longRuns && ++attempt < 3) continue;
                attempt = 0;
                if (silhouette.HasValue)
                {
                    float scale = Mathf.Max(width / (float)originalWidth, height / (float)originalHeight) * 1.12f;
                    width = Mathf.CeilToInt(originalWidth * scale);
                    height = Mathf.CeilToInt(originalHeight * scale);
                }
                else
                {
                    width += Mathf.Max(4, width / 8);
                    height += Mathf.Max(4, height / 8);
                }
            }
        }

        sealed class BoardBuilder
        {
            readonly int width, height;
            readonly System.Random random;
            readonly bool[] occupied;
            readonly int[] rowMin, rowMax, columnMin, columnMax;
            readonly bool longRuns;

            internal BoardBuilder(int width, int height, int seed, Silhouette? silhouette, bool longRuns)
            {
                this.width = width;
                this.height = height;
                this.longRuns = longRuns;
                random = new System.Random(seed);
                occupied = new bool[width * height];
                if (silhouette.HasValue)
                    for (int y = 0; y < height; y++)
                        for (int x = 0; x < width; x++)
                            occupied[y * width + x] = !silhouette.Value.Contains(new Vector2Int(x, y), width, height);
                rowMin = new int[height];
                rowMax = new int[height];
                columnMin = new int[width];
                columnMax = new int[width];
                Array.Fill(rowMin, width);
                Array.Fill(rowMax, -1);
                Array.Fill(columnMin, height);
                Array.Fill(columnMax, -1);
            }

            // Each head starts beyond the occupied cells in its exit lane.
            // Placing arrows in this order guarantees a reverse removal order.
            internal ArrowData[] Build(int count, int maxLength, int[] requiredLengths)
            {
                var arrows = new ArrowData[count];
                var path = new List<Vector2Int>(14);
                var best = new List<Vector2Int>(14);
                for (int i = 0; i < count; i++)
                {
                    best.Clear();
                    float bestScore = float.NegativeInfinity;
                    int length = requiredLengths == null
                        ? 3 + Mathf.FloorToInt(Mathf.Pow((float)random.NextDouble(), 1.6f) * (maxLength - 2))
                        : requiredLengths[i];
                    var bestDirection = Vector2Int.zero;
                    int samples = longRuns ? SamplesPerArrow * 3 : SamplesPerArrow;
                    for (int sample = 0; sample < samples; sample++)
                    {
                        var direction = Directions[random.Next(Directions.Length)];
                        var head = PickHead(direction);
                        if (!IsFree(head) || !IsFree(head - direction)) continue;

                        path.Clear();
                        path.Add(head);
                        path.Add(head - direction);
                        int turns = Extend(path, direction, length);
                        if (requiredLengths != null && path.Count != length) continue;
                        float distance = Mathf.Abs(head.x - width / 2) + Mathf.Abs(head.y - height / 2);
                        float score = longRuns
                            ? path.Count * 3 - distance * .12f + CountNeighbors(path) * .32f
                                - Mathf.Abs(turns - Mathf.Max(1, path.Count / 5)) * .55f + (float)random.NextDouble()
                            : path.Count * 3 + Mathf.Min(turns, 4) - distance * .2f + (float)random.NextDouble();
                        if (score <= bestScore) continue;
                        bestScore = score;
                        bestDirection = direction;
                        best.Clear();
                        best.AddRange(path);
                    }

                    if (best.Count == 0) return null;
                    var points = new int[best.Count];
                    for (int j = 0; j < points.Length; j++)
                    {
                        var cell = best[j];
                        points[j] = Encode(cell);
                        occupied[points[j]] = true;
                        rowMin[cell.y] = Mathf.Min(rowMin[cell.y], cell.x);
                        rowMax[cell.y] = Mathf.Max(rowMax[cell.y], cell.x);
                        columnMin[cell.x] = Mathf.Min(columnMin[cell.x], cell.y);
                        columnMax[cell.x] = Mathf.Max(columnMax[cell.x], cell.y);
                    }
                    arrows[i] = new ArrowData { points = points, dx = bestDirection.x, dy = bestDirection.y };
                }
                return arrows;
            }

            Vector2Int PickHead(Vector2Int direction)
            {
                int offset = random.Next(1, longRuns ? 5 : 7);
                if (direction.x != 0)
                {
                    int y = random.Next(height);
                    int edge = direction.x > 0 ? rowMax[y] : rowMin[y];
                    if (edge < 0 || edge >= width) edge = random.Next(width);
                    return new Vector2Int(edge + direction.x * offset, y);
                }
                int x = random.Next(width);
                int columnEdge = direction.y > 0 ? columnMax[x] : columnMin[x];
                if (columnEdge < 0 || columnEdge >= height) columnEdge = random.Next(height);
                return new Vector2Int(x, columnEdge + direction.y * offset);
            }

            int Extend(List<Vector2Int> path, Vector2Int direction, int length)
            {
                int turns = 0;
                var heading = -direction;
                int run = 1;
                var head = path[0];
                while (path.Count < length)
                {
                    var tail = path[path.Count - 1];
                    float bestScore = float.NegativeInfinity;
                    var nextHeading = Vector2Int.zero;
                    foreach (var step in Directions)
                    {
                        var next = tail + step;
                        if (!IsFree(next) || path.Contains(next)) continue;
                        var offset = next - head;
                        if (offset.x * direction.y == offset.y * direction.x &&
                            offset.x * direction.x + offset.y * direction.y > 0) continue;
                        float continuation = longRuns ? (run < 3 ? .82f : run < 7 ? .50f : .18f) : .3f;
                        float score = (float)random.NextDouble() + (step == heading ? continuation : 0f);
                        if (score <= bestScore) continue;
                        bestScore = score;
                        nextHeading = step;
                    }
                    if (nextHeading == Vector2Int.zero) break;
                    if (nextHeading != heading) { turns++; run = 0; }
                    run++;
                    heading = nextHeading;
                    path.Add(tail + heading);
                }
                return turns;
            }

            int CountNeighbors(List<Vector2Int> path)
            {
                int neighbors = 0;
                foreach (var cell in path)
                    foreach (var step in Directions)
                        if (!IsFree(cell + step)) neighbors++;
                return neighbors;
            }

            int Encode(Vector2Int point) => point.y * width + point.x;

            bool IsFree(Vector2Int point) =>
                point.x >= 0 && point.x < width && point.y >= 0 && point.y < height && !occupied[Encode(point)];
        }
    }
}
