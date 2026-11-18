using System.Collections.Generic;
using UnityEngine;

namespace DrawArrow
{
    internal sealed partial class FlatArrowLayout
    {
        bool TryRouteIntoHoles(Vector2Int size, List<int>[] blockers, int[] visited)
        {
            GetPathBounds(out var low, out var high);
            var edge = new List<int>();
            foreach (var inward in CardinalDirections)
            {
                var step = ArrowBoard.Key(inward);
                var min = low + Vector3Int.Max(step, Vector3Int.zero);
                var max = high + Vector3Int.Min(step, Vector3Int.zero);
                var box = new RectInt(min.x, min.y, max.x - min.x + 1, max.y - min.y + 1);
                if (box.width <= 0 || box.height <= 0 || box.width * box.height < occupied.Count) continue;

                CollectOutsidePaths(min, max, edge);
                if (edge.Count == 0 || edge.Count > (requireOneStep ? 3 : 5)) continue;
                edge.Sort((a, b) => paths[b].Count.CompareTo(paths[a].Count));
                foreach (int index in edge) SetOccupied(index, false);
                int budget = 9000;
                if (RouteNextIntoHole(edge, 0, box, size, blockers, visited, ref budget)) return true;
                foreach (int index in edge) SetOccupied(index, true);
            }
            return false;
        }

        bool RouteNextIntoHole(List<int> edge, int depth, RectInt box, Vector2Int size,
            List<int>[] blockers, int[] visited, ref int budget)
        {
            if (depth == edge.Count) return ReadBlockers(size, blockers, visited) && CanClear(blockers);
            if (budget <= 0) return false;
            int index = edge[depth];
            var original = paths[index];
            var originalDirection = directions[index];
            var heads = FindVacantHeads(box, original.Count);
            var route = new List<Vector3>(original.Count);
            var used = new HashSet<Vector3Int>();
            foreach (var option in heads)
            {
                if (budget <= 0) break;
                var previous = option.head - option.forward;
                route.Clear();
                used.Clear();
                route.Add(option.head);
                route.Add(previous);
                used.Add(option.head);
                used.Add(previous);
                int headBudget = 180;
                if (TraceHoleTail(edge, depth, box, size, blockers, visited, original.Count, option.head,
                    option.forward, previous, -option.forward, route, used, ref budget, ref headBudget)) return true;
            }
            paths[index] = original;
            directions[index] = originalDirection;
            return false;
        }

        bool TraceHoleTail(List<int> edge, int depth, RectInt box, Vector2Int size,
            List<int>[] blockers, int[] visited, int points, Vector3Int head, Vector3Int forward,
            Vector3Int tail, Vector3Int backward, List<Vector3> route, HashSet<Vector3Int> used,
            ref int budget, ref int headBudget)
        {
            if (budget <= 0 || headBudget <= 0) return false;
            budget--;
            headBudget--;
            if (route.Count == points)
            {
                int index = edge[depth];
                var candidate = new List<Vector3>(route);
                candidate.Reverse();
                paths[index] = candidate;
                directions[index] = forward;
                SetOccupied(index, true);
                if (RouteNextIntoHole(edge, depth + 1, box, size, blockers, visited, ref budget)) return true;
                SetOccupied(index, false);
                return false;
            }

            var side = new Vector3Int(-backward.y, backward.x, 0);
            for (int i = 0; i < 3 && budget > 0 && headBudget > 0; i++)
            {
                var step = i == 0 ? backward : i == 1 ? side : -side;
                var cell = tail + step;
                var delta = cell - head;
                if (delta.x * forward.y == delta.y * forward.x &&
                    delta.x * forward.x + delta.y * forward.y > 0) continue;
                if (!Inside(box, cell) || occupied.ContainsKey(cell) || !used.Add(cell)) continue;
                route.Add(cell);
                if (TraceHoleTail(edge, depth, box, size, blockers, visited, points, head, forward,
                    cell, step, route, used, ref budget, ref headBudget)) return true;
                route.RemoveAt(route.Count - 1);
                used.Remove(cell);
            }
            return false;
        }

        List<(Vector3Int head, Vector3Int forward, int space)> FindVacantHeads(RectInt box, int points)
        {
            var seen = new HashSet<Vector3Int>();
            var component = new List<Vector3Int>();
            var heads = new List<(Vector3Int head, Vector3Int forward, int space)>();
            for (int y = box.yMin; y < box.yMax; y++)
            for (int x = box.xMin; x < box.xMax; x++)
            {
                var start = new Vector3Int(x, y, 0);
                if (occupied.ContainsKey(start) || !seen.Add(start)) continue;
                component.Clear();
                component.Add(start);
                for (int i = 0; i < component.Count; i++)
                foreach (var direction in CardinalDirections)
                {
                    var cell = component[i] + ArrowBoard.Key(direction);
                    if (Inside(box, cell) && !occupied.ContainsKey(cell) && seen.Add(cell)) component.Add(cell);
                }
                if (component.Count < points) continue;
                foreach (var head in component)
                foreach (var direction in CardinalDirections)
                {
                    var forward = ArrowBoard.Key(direction);
                    var previous = head - forward;
                    if (!Inside(box, previous) || occupied.ContainsKey(previous)) continue;
                    if (!requireOneStep || CanExitHole(box, head, forward)) heads.Add((head, forward, component.Count));
                }
            }
            heads.Sort((a, b) =>
            {
                int space = b.space.CompareTo(a.space);
                if (space != 0) return space;
                int row = a.head.y.CompareTo(b.head.y);
                if (row != 0) return row;
                int column = a.head.x.CompareTo(b.head.x);
                if (column != 0) return column;
                return (a.forward.x + 2 * a.forward.y).CompareTo(b.forward.x + 2 * b.forward.y);
            });
            return heads;
        }

        bool CanExitHole(RectInt box, Vector3Int head, Vector3Int direction)
        {
            var next = head + direction;
            for (var cell = next; Inside(box, cell); cell += direction)
                if (occupied.ContainsKey(cell)) return cell == next;
            return true;
        }

        static bool Inside(RectInt box, Vector3Int cell) =>
            cell.x >= box.xMin && cell.x < box.xMax && cell.y >= box.yMin && cell.y < box.yMax;
    }
}
