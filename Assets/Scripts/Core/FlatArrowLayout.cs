using System.Collections.Generic;
using UnityEngine;

namespace DrawArrow
{
    // Builds the initial flat board on the same integer grid used by movement.
    internal sealed partial class FlatArrowLayout
    {
        public const int GridStep = 1;
        static readonly Vector3[] CardinalDirections =
            { Vector3.right, Vector3.up, Vector3.left, Vector3.down };

        readonly List<Vector3>[] paths;
        readonly Vector3[] directions;
        readonly Vector3[] originalHeads;
        readonly List<Vector3>[] sourcePaths;
        readonly Dictionary<Vector3Int, int> occupied = new Dictionary<Vector3Int, int>();
        readonly List<int> relocated = new List<int>();
        readonly int width;
        readonly int height;
        bool requireOneStep = true;

        public FlatArrowLayout(List<Vector3>[] paths, Vector3[] directions, int width, int height,
            bool preserveLengths = false)
        {
            this.paths = paths;
            this.directions = directions;
            this.width = width;
            this.height = height;
            originalHeads = new Vector3[paths.Length];
            if (preserveLengths)
            {
                sourcePaths = new List<Vector3>[paths.Length];
                for (int i = 0; i < paths.Length; i++) sourcePaths[i] = new List<Vector3>(paths[i]);
            }
        }

        public Vector2Int FitInitialGaps(float sourceScale)
        {
            GetPathBounds(out var min, out var max);
            if (sourcePaths == null && sourceScale > 1f && min.x <= max.x)
            {
                // Legacy expanded packs store several coordinates per visual step.
                float factor = 1f / (Mathf.CeilToInt(sourceScale) + 1);
                Resample(min, factor, factor);
                GetPathBounds(out _, out max);
                var sampled = new FlatArrowLayout(paths, directions, max.x + 1, max.y + 1);
                sampled.NormalizeGaps();
            }
            else
            {
                NormalizeGaps();
            }

            if (TryPlaceOnGrid(out var size)) return size;
            if (sourcePaths != null)
            {
                PackLengthRows(0, 0, ArrowBoard.ExtendedGridSteps, false);
                return new Vector2Int(ArrowBoard.ExtendedGridSteps + 1, ArrowBoard.FlatGridSteps + 1);
            }
            CompactStraightRuns();
            if (TryPlaceOnGrid(out size)) return size;

            GetPathBounds(out var low, out var high);
            Resample(low, Mathf.Min(1f, 900f / Mathf.Max(1, high.x - low.x)),
                Mathf.Min(1f, 85f / Mathf.Max(1, high.y - low.y)));
            var compact = new FlatArrowLayout(paths, directions, 901, 86);
            compact.NormalizeGaps();
            if (TryPlaceOnGrid(out size)) return size;

            PackRows();
            return new Vector2Int(ArrowBoard.ExtendedGridSteps + 1, ArrowBoard.FlatGridSteps + 1);
        }

        public void CompactPlacedPaths(Vector2Int size, bool searchHoles)
        {
            if (paths.Length < 2) return;
            RemoveEmptyLines();
            if (!RebuildPlacedOccupancy()) return;
            var blockers = new List<int>[paths.Length];
            var candidateBlockers = new List<int>[paths.Length];
            var visited = new int[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                blockers[i] = new List<int>();
                candidateBlockers[i] = new List<int>();
            }
            if (!ReadBlockers(size, blockers, visited)) return;

            // A complete route keeps its shape and length when it moves.
            while (true)
            {
                bool inset = TryInsetEdge(size, blockers, visited);
                if (!inset &&
                    !TryPackInterior(size, candidateBlockers, visited) &&
                    !TryPackEdgeGroup(size, candidateBlockers, visited) &&
                    !TryPushEdge(size, candidateBlockers, visited))
                {
                    if (!searchHoles) break;
                    if (!TryRouteIntoHoles(size, candidateBlockers, visited))
                    {
                        if (!requireOneStep) break;
                        requireOneStep = false;
                        if (!TryRouteIntoHoles(size, candidateBlockers, visited)) break;
                    }
                }
                RemoveEmptyLines();
                RebuildPlacedOccupancy();
                if (!inset && !ReadBlockers(size, blockers, visited)) return;
            }
        }

        bool TryPackInterior(Vector2Int size, List<int>[] blockers, int[] visited)
        {
            GetPathBounds(out var low, out var high);
            var checkedArrows = new HashSet<int>();
            foreach (var inward in CardinalDirections)
            {
                int edge = EdgeOwner(low, high, ArrowBoard.Key(inward));
                if (edge < 0 || !checkedArrows.Add(edge)) continue;
                if (TryPackArrow(edge, size, blockers, visited)) return true;
            }
            return false;
        }

        int EdgeOwner(Vector3Int low, Vector3Int high, Vector3Int inward)
        {
            int owner = -1;
            foreach (var cell in occupied)
            {
                if (inward.x > 0 && cell.Key.x != low.x || inward.x < 0 && cell.Key.x != high.x ||
                    inward.y > 0 && cell.Key.y != low.y || inward.y < 0 && cell.Key.y != high.y) continue;
                if (owner >= 0 && owner != cell.Value) return -1;
                owner = cell.Value;
            }
            return owner;
        }

        bool TryPackArrow(int index, Vector2Int size, List<int>[] blockers, int[] visited)
        {
            SetOccupied(index, false);
            var innerMin = new Vector3Int(size.x, size.y, 0);
            var innerMax = Vector3Int.zero;
            foreach (var cell in occupied.Keys)
            {
                innerMin = Vector3Int.Min(innerMin, cell);
                innerMax = Vector3Int.Max(innerMax, cell);
            }

            foreach (var candidate in PackingOffsets(index, innerMin, innerMax))
            {
                ShiftPath(index, candidate.offset);
                SetOccupied(index, true);
                if (ReadBlockers(size, blockers, visited) && CanClear(blockers)) return true;
                SetOccupied(index, false);
                ShiftPath(index, -(Vector3)candidate.offset);
            }
            SetOccupied(index, true);
            return false;
        }

        bool TryPackEdgeGroup(Vector2Int size, List<int>[] blockers, int[] visited)
        {
            const int maxGroupSize = 6;
            GetPathBounds(out var low, out var high);
            var edge = new List<int>();
            foreach (var inward in CardinalDirections)
            {
                var step = ArrowBoard.Key(inward);
                var min = low + Vector3Int.Max(step, Vector3Int.zero);
                var max = high + Vector3Int.Min(step, Vector3Int.zero);
                int columns = max.x - min.x + 1;
                int rows = max.y - min.y + 1;
                if (columns <= 0 || rows <= 0 || columns * rows < occupied.Count) continue;

                CollectOutsidePaths(min, max, edge);
                if (edge.Count < 2 || edge.Count > maxGroupSize) continue;
                if (TryPackGroup(edge, min, max, size, blockers, visited)) return true;
            }
            return false;
        }

        bool TryPushEdge(Vector2Int size, List<int>[] blockers, int[] visited)
        {
            GetPathBounds(out var low, out var high);
            var edge = new List<int>();
            var moving = new HashSet<int>();
            foreach (var inward in CardinalDirections)
            {
                var step = ArrowBoard.Key(inward);
                var min = low + Vector3Int.Max(step, Vector3Int.zero);
                var max = high + Vector3Int.Min(step, Vector3Int.zero);
                if (min.x > max.x || min.y > max.y ||
                    (max.x - min.x + 1) * (max.y - min.y + 1) < occupied.Count) continue;

                var side = new Vector3Int(-step.y, step.x, 0);
                foreach (var delta in new[] { step, step + side, step - side })
                {
                    CollectOutsidePaths(min, max, edge);
                    moving.Clear();
                    foreach (int index in edge) moving.Add(index);
                    if (!CollectPushedPaths(edge, moving, delta, min, max)) continue;
                    ShiftPaths(edge, delta);
                    if (RebuildPlacedOccupancy() && ReadBlockers(size, blockers, visited) && CanClear(blockers))
                        return true;
                    ShiftPaths(edge, -(Vector3)delta);
                    RebuildPlacedOccupancy();
                }
            }
            return false;
        }

        bool CollectPushedPaths(List<int> edge, HashSet<int> moving, Vector3Int delta,
            Vector3Int min, Vector3Int max)
        {
            // Translate all routes occupying the destination, stopping if any
            // part of the connected group cannot fit inside the new envelope.
            for (int i = 0; i < edge.Count; i++)
            foreach (var point in paths[edge[i]])
            {
                var target = ArrowBoard.Key(point) + delta;
                if (target.x < min.x || target.x > max.x || target.y < min.y || target.y > max.y) return false;
                if (occupied.TryGetValue(target, out int owner) && moving.Add(owner)) edge.Add(owner);
            }
            return true;
        }

        bool TryPackGroup(List<int> edge, Vector3Int min, Vector3Int max, Vector2Int size,
            List<int>[] blockers, int[] visited)
        {
            foreach (int index in edge) SetOccupied(index, false);
            var candidates = new List<(int index, List<(Vector3Int offset, int contacts)> placements)>();
            foreach (int index in edge)
                candidates.Add((index, PackingOffsets(index, min, max)));
            // Place the route with the fewest options first.
            candidates.Sort((a, b) =>
            {
                int count = a.placements.Count.CompareTo(b.placements.Count);
                return count != 0 ? count : a.index.CompareTo(b.index);
            });
            int budget = 192;
            if (FindGroupPlacement(candidates, 0, size, blockers, visited, ref budget)) return true;
            foreach (int index in edge) SetOccupied(index, true);
            return false;
        }

        bool FindGroupPlacement(List<(int index, List<(Vector3Int offset, int contacts)> placements)> group,
            int depth, Vector2Int size, List<int>[] blockers, int[] visited, ref int budget)
        {
            if (depth == group.Count) return ReadBlockers(size, blockers, visited) && CanClear(blockers);

            var route = group[depth];
            if (route.placements.Count == 0) return false;
            int visitedOffsets = 0;
            foreach (var placement in route.placements)
            {
                if (budget == 0 || visitedOffsets >= 20) break;
                if (!CanShiftPath(route.index, placement.offset)) continue;
                visitedOffsets++;
                budget--;
                ShiftPath(route.index, placement.offset);
                SetOccupied(route.index, true);
                if (FindGroupPlacement(group, depth + 1, size, blockers, visited, ref budget)) return true;
                SetOccupied(route.index, false);
                ShiftPath(route.index, -(Vector3)placement.offset);
            }
            return false;
        }

        bool CanShiftPath(int index, Vector3Int offset)
        {
            foreach (var point in paths[index])
                if (occupied.ContainsKey(ArrowBoard.Key(point) + offset)) return false;
            return true;
        }

        List<(Vector3Int offset, int contacts)> PackingOffsets(int index, Vector3Int innerMin, Vector3Int innerMax)
        {
            var path = paths[index];
            var min = ArrowBoard.Key(path[0]);
            var max = min;
            foreach (var point in path)
            {
                var cell = ArrowBoard.Key(point);
                min = Vector3Int.Min(min, cell);
                max = Vector3Int.Max(max, cell);
            }
            var candidates = new List<(Vector3Int offset, int contacts)>();
            for (int y = innerMin.y - min.y; y <= innerMax.y - max.y; y++)
            for (int x = innerMin.x - min.x; x <= innerMax.x - max.x; x++)
            {
                var offset = new Vector3Int(x, y, 0);
                int contacts = CountContacts(path, offset);
                if (contacts >= 0) candidates.Add((offset, contacts));
            }
            candidates.Sort((a, b) =>
            {
                int contacts = b.contacts.CompareTo(a.contacts);
                if (contacts != 0) return contacts;
                int row = a.offset.y.CompareTo(b.offset.y);
                return row != 0 ? row : a.offset.x.CompareTo(b.offset.x);
            });
            return candidates;
        }

        int CountContacts(List<Vector3> path, Vector3Int offset)
        {
            int contacts = 0;
            foreach (var point in path)
            {
                var cell = ArrowBoard.Key(point) + offset;
                if (occupied.ContainsKey(cell)) return -1;
                foreach (var direction in CardinalDirections)
                    if (occupied.ContainsKey(cell + ArrowBoard.Key(direction))) contacts++;
            }
            return contacts;
        }

        bool ReadBlockers(Vector2Int size, List<int>[] blockers, int[] visited)
        {
            System.Array.Clear(visited, 0, visited.Length);
            for (int i = 0; i < paths.Length; i++)
            {
                blockers[i].Clear();
                if (!ScanBlockers(i, size, visited, blockers[i], null)) return false;
            }
            return true;
        }

        static bool CanClear(List<int>[] blockers)
        {
            var state = new byte[blockers.Length];
            for (int i = 0; i < blockers.Length; i++)
                if (!CanClearArrow(i, blockers, state)) return false;
            return true;
        }

        static bool CanClearArrow(int index, List<int>[] blockers, byte[] state)
        {
            if (state[index] != 0) return state[index] == 2;
            state[index] = 1;
            foreach (int blocker in blockers[index])
                if (!CanClearArrow(blocker, blockers, state)) return false;
            state[index] = 2;
            return true;
        }

        void RemoveEmptyLines()
        {
            var usedColumns = new SortedSet<int>();
            var usedRows = new SortedSet<int>();
            foreach (var path in paths)
            foreach (var point in path)
            {
                usedColumns.Add((int)point.x);
                usedRows.Add((int)point.y);
            }
            var columns = RankCoordinates(usedColumns);
            var rows = RankCoordinates(usedRows);
            int offsetX = usedColumns.Min + (usedColumns.Max - usedColumns.Min - columns.Count + 1) / 2;
            int offsetY = usedRows.Min + (usedRows.Max - usedRows.Min - rows.Count + 1) / 2;
            foreach (var path in paths)
            for (int i = 0; i < path.Count; i++)
            {
                var point = path[i];
                path[i] = new Vector3(columns[(int)point.x] + offsetX, rows[(int)point.y] + offsetY);
            }
        }

        bool TryInsetEdge(Vector2Int size, List<int>[] blockers, int[] visited)
        {
            GetPathBounds(out var low, out var high);
            var edge = new List<int>();
            foreach (var inward in CardinalDirections)
            {
                var step = ArrowBoard.Key(inward);
                var min = low + Vector3Int.Max(step, Vector3Int.zero);
                var max = high + Vector3Int.Min(step, Vector3Int.zero);
                if (min.x > max.x || min.y > max.y) continue;

                CollectOutsidePaths(min, max, edge);

                var side = new Vector3(-inward.y, inward.x);
                for (int n = 0; n <= 4; n++)
                {
                    int lateral = (n + 1) / 2 * (n % 2 == 0 ? -1 : 1);
                    var delta = inward + side * lateral;
                    if (!FitsInset(edge, delta, min, max)) continue;
                    ShiftPaths(edge, delta);
                    if (RebuildPlacedOccupancy() && SameBlockers(size, blockers, visited)) return true;
                    ShiftPaths(edge, -delta);
                }
                RebuildPlacedOccupancy();
            }
            return false;
        }

        void CollectOutsidePaths(Vector3Int min, Vector3Int max, List<int> result)
        {
            result.Clear();
            for (int i = 0; i < paths.Length; i++)
            foreach (var point in paths[i])
            {
                if (point.x >= min.x && point.x <= max.x && point.y >= min.y && point.y <= max.y) continue;
                result.Add(i);
                break;
            }
        }

        bool FitsInset(List<int> edge, Vector3 delta, Vector3Int min, Vector3Int max)
        {
            foreach (int i in edge)
            foreach (var point in paths[i])
            {
                var target = point + delta;
                if (target.x < min.x || target.x > max.x || target.y < min.y || target.y > max.y) return false;
            }
            return true;
        }

        void ShiftPaths(List<int> indices, Vector3 delta)
        {
            foreach (int i in indices) ShiftPath(i, delta);
        }

        void ShiftPath(int index, Vector3 delta)
        {
            var path = paths[index];
            for (int j = 0; j < path.Count; j++) path[j] += delta;
        }

        bool RebuildPlacedOccupancy()
        {
            occupied.Clear();
            for (int i = 0; i < paths.Length; i++)
            foreach (var point in paths[i])
            {
                var key = ArrowBoard.Key(point);
                if (occupied.ContainsKey(key)) return false;
                occupied.Add(key, i);
            }
            return true;
        }

        bool SameBlockers(Vector2Int size, List<int>[] blockers, int[] visited)
        {
            System.Array.Clear(visited, 0, visited.Length);
            for (int i = 0; i < paths.Length; i++)
                if (!ScanBlockers(i, size, visited, null, blockers[i])) return false;
            return true;
        }

        bool ScanBlockers(int index, Vector2Int size, int[] visited, List<int> record, List<int> expected)
        {
            var path = paths[index];
            var head = ArrowBoard.Key(path[path.Count - 1]);
            var direction = ArrowBoard.Key(directions[index]);
            int count = 0;
            for (var cell = head + direction;
                cell.x >= 0 && cell.y >= 0 && cell.x < size.x && cell.y < size.y; cell += direction)
            {
                if (!occupied.TryGetValue(cell, out int owner)) continue;
                if (owner == index) return false;
                if (visited[owner] == index + 1) continue;
                if (requireOneStep && count == 0 && cell != head + direction) return false;
                if (expected != null && (count >= expected.Count || expected[count] != owner)) return false;
                visited[owner] = index + 1;
                record?.Add(owner);
                count++;
            }
            return expected == null || count == expected.Count;
        }

        bool TryPlaceOnGrid(out Vector2Int size)
        {
            int shortSide = ArrowBoard.FlatGridSteps;
            size = Vector2Int.one * (shortSide + 1);
            if (paths.Length == 0) return true;
            GetPathBounds(out var min, out var max);
            int spanX = max.x - min.x;
            int spanY = max.y - min.y;
            bool transpose = spanY > shortSide && spanY > spanX;
            if (transpose)
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    var path = paths[i];
                    for (int j = 0; j < path.Count; j++)
                        path[j] = new Vector3(path[j].y, path[j].x);
                    directions[i] = new Vector3(directions[i].y, directions[i].x);
                }
                GetPathBounds(out min, out max);
                spanX = max.x - min.x;
                spanY = max.y - min.y;
            }
            int longSide = spanX <= shortSide ? shortSide : ArrowBoard.ExtendedGridSteps;
            if (spanX > longSide || spanY > shortSide) return false;
            var offset = new Vector3((longSide - spanX) / 2 - min.x, (shortSide - spanY) / 2 - min.y);
            foreach (var path in paths)
                for (int j = 0; j < path.Count; j++) path[j] += offset;
            size = new Vector2Int(longSide + 1, shortSide + 1);
            return true;
        }

        void NormalizeGaps()
        {
            for (int i = 0; i < paths.Length; i++)
            {
                var path = paths[i];
                ArrowBoard.TrimFlatTail(path, directions[i]);
                if (path.Count > 0) originalHeads[i] = path[path.Count - 1];
                if (!FitThroughFreeCells(path) ||
                    sourcePaths != null && SourceSteps(i) > 1 && path.Count != SourceSteps(i) + 1)
                {
                    Relocate(i);
                    continue;
                }
                SetOccupied(i, true);
            }
            for (int i = 0; i < paths.Length; i++)
            {
                if (paths[i].Count == 1) FitShortArrow(i);
                else if (paths[i].Count > 1) AlignHead(i);
            }

            int cycle;
            while ((cycle = FindCycle()) >= 0)
            {
                if (TryReroute(cycle, 0)) continue;
                SetOccupied(cycle, false);
                Relocate(cycle);
            }

            var rerouted = new bool[paths.Length];
            while (true)
            {
                int firstGap = -1;
                bool extended = false;
                for (int i = 0; i < paths.Length; i++)
                {
                    int gap = Gap(i);
                    if (gap < 2) continue;
                    if (firstGap < 0) firstGap = i;
                    extended |= TryExtend(i, gap);
                }
                if (extended) continue;
                if (firstGap < 0) break;

                int gapCount = CountGaps();
                if (TryRerouteGap(gapCount, rerouted)) continue;
                SetOccupied(firstGap, false);
                Relocate(firstGap);
            }

            relocated.RemoveAll(TryFitDisplaced);
            // Use an isolated corner if the original board has no suitable space.
            if (relocated.Count == 0) return;
            if (sourcePaths != null)
            {
                PackLengthRows(width, height, Mathf.Max(width, 100), true);
                return;
            }
            int perRow = Mathf.CeilToInt(Mathf.Sqrt(relocated.Count * .5f));
            for (int n = 0; n < relocated.Count; n++)
            {
                int i = relocated[n];
                var tail = new Vector3(width + n % perRow * 2, height + n / perRow);
                paths[i].Add(tail);
                paths[i].Add(tail + Vector3.right * GridStep);
                directions[i] = Vector3.right;
            }
        }

        int SourceSteps(int index) => Mathf.Max(GridStep, sourcePaths[index].Count - 1);

        void PackLengthRows(int left, int top, int widthLimit, bool displacedOnly)
        {
            int x = left, y = top;
            int count = displacedOnly ? relocated.Count : paths.Length;
            for (int n = 0; n < count; n++)
            {
                int i = displacedOnly ? relocated[n] : n;
                int length = SourceSteps(i);
                if (x > left && x + length > left + widthLimit)
                {
                    x = left;
                    y += GridStep;
                }
                paths[i] = StraightRoute(new Vector3(x + length, y), Vector3.right, length);
                directions[i] = Vector3.right;
                x += length + GridStep;
            }
        }

        static List<Vector3> StraightRoute(Vector3 head, Vector3 direction, int steps)
        {
            var path = new List<Vector3>(steps + 1);
            for (int step = steps; step >= 0; step--) path.Add(head - direction * step);
            return path;
        }

        void GetPathBounds(out Vector3Int min, out Vector3Int max)
        {
            min = new Vector3Int(int.MaxValue, int.MaxValue, 0);
            max = new Vector3Int(int.MinValue, int.MinValue, 0);
            foreach (var path in paths)
            foreach (var point in path)
            {
                var cell = ArrowBoard.Key(point);
                min = Vector3Int.Min(min, cell);
                max = Vector3Int.Max(max, cell);
            }
        }

        void Resample(Vector3Int min, float scaleX, float scaleY)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                var compressed = new List<Vector3>();
                foreach (var point in paths[i])
                {
                    var offset = point - (Vector3)min;
                    AddGridPoint(compressed, new Vector3(Mathf.FloorToInt(offset.x * scaleX),
                        Mathf.FloorToInt(offset.y * scaleY)));
                }
                paths[i] = compressed;
            }
        }

        void CompactStraightRuns()
        {
            var corners = new List<Vector3>[paths.Length];
            var usedColumns = new SortedSet<int>();
            var usedRows = new SortedSet<int>();
            for (int i = 0; i < paths.Length; i++)
            {
                var path = paths[i];
                var vertices = new List<Vector3>();
                for (int j = 0; j < path.Count; j++)
                {
                    if (j > 0 && j < path.Count - 1 &&
                        path[j] - path[j - 1] == path[j + 1] - path[j]) continue;
                    var point = path[j];
                    vertices.Add(point);
                    usedColumns.Add((int)point.x);
                    usedRows.Add((int)point.y);
                }
                corners[i] = vertices;
            }

            var columns = RankCoordinates(usedColumns);
            var rows = RankCoordinates(usedRows);
            for (int i = 0; i < paths.Length; i++)
            {
                var compact = new List<Vector3>();
                foreach (var corner in corners[i])
                    AddGridPoint(compact, new Vector3(columns[(int)corner.x], rows[(int)corner.y]));
                paths[i] = compact;
            }
        }

        static Dictionary<int, int> RankCoordinates(SortedSet<int> coordinates)
        {
            var ranks = new Dictionary<int, int>(coordinates.Count);
            foreach (int coordinate in coordinates) ranks.Add(coordinate, ranks.Count);
            return ranks;
        }

        static void AddGridPoint(List<Vector3> path, Vector3 end)
        {
            if (path.Count == 0)
            {
                path.Add(end);
                return;
            }
            var last = path[path.Count - 1];
            var delta = end - last;
            var direction = delta.normalized;
            int steps = Mathf.RoundToInt(Mathf.Abs(delta.x) + Mathf.Abs(delta.y));
            for (int step = 1; step <= steps; step++) path.Add(last + direction * step);
        }

        void PackRows()
        {
            // Each row is a chain that exits to the right. Keep room for each
            // remaining arrow in the row before using cells for longer bodies.
            const int span = ArrowBoard.ExtendedGridSteps;
            const int height = ArrowBoard.FlatGridSteps;
            const int separation = GridStep;
            int rowCapacity = (span + separation) / (1 + separation);
            int rowCount = (paths.Length + rowCapacity - 1) / rowCapacity;
            if ((rowCount - 1) * separation > height)
                throw new System.InvalidOperationException("Too many arrows for the flat grid clearance.");
            int perRow = (paths.Length + rowCount - 1) / rowCount;
            for (int first = 0; first < paths.Length; first += perRow)
            {
                int count = Mathf.Min(perRow, paths.Length - first);
                int x = 0;
                int y = (height - (rowCount - 1) * separation) / 2 + first / perRow * separation;
                int spare = span - count - (count - 1) * separation;
                for (int j = 0; j < count; j++)
                {
                    int i = first + j;
                    int extra = Mathf.Min(paths[i].Count - 2, spare / (count - j));
                    var path = new List<Vector3>();
                    for (int step = 0; step <= extra + 1; step++) path.Add(new Vector3(x + step, y));
                    x += extra + 1 + separation;
                    spare -= extra;
                    paths[i] = path;
                    directions[i] = Vector3.right;
                }
            }
        }

        bool FitThroughFreeCells(List<Vector3> path)
        {
            if (path.Count == 0) return false;
            var used = new HashSet<Vector3Int>();
            for (int i = path.Count - 1; i >= 0; i--)
            {
                var point = path[i];
                var cell = ArrowBoard.Key(point);
                if (point == (Vector3)cell && IsFree(cell) && used.Add(cell)) continue;
                path.RemoveRange(0, i + 1);
                break;
            }
            return path.Count > 0;
        }

        void FitShortArrow(int index)
        {
            var head = paths[index][0];
            if (TryAddTail(index, head, directions[index])) return;
            foreach (var direction in CardinalDirections)
            {
                if (TryAddTail(index, head, direction)) return;
            }
            SetOccupied(index, false);
            Relocate(index);
        }

        void AlignHead(int index)
        {
            var path = paths[index];
            var head = path[path.Count - 1];
            var lastStep = head - path[path.Count - 2];
            if (lastStep == directions[index] * GridStep) return;

            var next = head + directions[index] * GridStep;
            var cell = ArrowBoard.Key(next);
            if (sourcePaths == null && IsFree(cell))
            {
                path.Add(next);
                occupied.Add(cell, index);
                return;
            }

            SetOccupied(index, false);
            directions[index] = lastStep / GridStep;
            ArrowBoard.TrimFlatTail(path, directions[index]);
            if (sourcePaths != null && path.Count != SourceSteps(index) + 1)
            {
                Relocate(index);
                return;
            }
            SetOccupied(index, true);
        }

        bool TryAddTail(int index, Vector3 head, Vector3 direction)
        {
            var tail = head - direction * GridStep;
            var cell = ArrowBoard.Key(tail);
            if (!IsFree(cell)) return false;
            occupied.Add(cell, index);
            paths[index].Insert(0, tail);
            directions[index] = direction;
            return true;
        }

        void SetOccupied(int index, bool place)
        {
            foreach (var point in paths[index])
            {
                var cell = ArrowBoard.Key(point);
                if (place) occupied[cell] = index;
                else occupied.Remove(cell);
            }
        }

        void Relocate(int index)
        {
            paths[index].Clear();
            relocated.Add(index);
        }

        bool InBounds(Vector3Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height;
        bool IsFree(Vector3Int cell) => InBounds(cell) && !occupied.ContainsKey(cell);

        int Gap(int index)
        {
            var path = paths[index];
            if (path.Count == 0) return 0;
            var cell = ArrowBoard.Key(path[path.Count - 1]);
            var direction = ArrowBoard.Key(directions[index]);
            int distance = 0;
            for (cell += direction; InBounds(cell); cell += direction)
            {
                distance++;
                if (occupied.TryGetValue(cell, out int owner))
                    return owner == index ? 0 : distance;
            }
            return 0;
        }

        int CountGaps()
        {
            int count = 0;
            for (int i = 0; i < paths.Length; i++)
                if (Gap(i) > 1) count++;
            return count;
        }

        void Extend(int index, int gap)
        {
            var path = paths[index];
            var head = path[path.Count - 1];
            for (int step = 1; step < gap; step++)
            {
                var point = head + directions[index] * (step * GridStep);
                path.Add(point);
                occupied.Add(ArrowBoard.Key(point), index);
            }
        }

        bool TryExtend(int index, int gap)
        {
            if (sourcePaths != null) return TryAdvanceWithFixedLength(index, gap);
            var path = paths[index];
            int length = path.Count;
            Extend(index, gap);
            if (!ClosesCycle(index, index, new bool[paths.Length])) return true;
            for (int i = length; i < path.Count; i++)
                occupied.Remove(ArrowBoard.Key(path[i]));
            path.RemoveRange(length, path.Count - length);
            return false;
        }

        bool TryAdvanceWithFixedLength(int index, int gap)
        {
            var original = paths[index];
            var direction = directions[index];
            int advance = gap - 1;
            SetOccupied(index, false);
            var translated = new List<Vector3>(original.Count);
            foreach (var point in original) translated.Add(point + direction * advance);
            if (TryFixedRoute(index, translated, direction, -1)) return true;
            if (TryFixedRoute(index, SlideRoute(original, direction, advance), direction, -1)) return true;
            paths[index] = original;
            directions[index] = direction;
            SetOccupied(index, true);
            return false;
        }

        static List<Vector3> SlideRoute(List<Vector3> original, Vector3 direction, int advance)
        {
            var path = new List<Vector3>(original);
            var head = path[path.Count - 1];
            for (int step = 1; step <= advance; step++) path.Add(head + direction * step);
            path.RemoveRange(0, advance);
            return path;
        }

        // A negative gap limit checks only this route. A nonnegative limit also
        // prevents this replacement from creating more unresolved gaps.
        bool TryFixedRoute(int index, List<Vector3> candidate, Vector3 direction, int gapLimit)
        {
            ArrowBoard.TrimFlatTail(candidate, direction);
            if (candidate.Count != SourceSteps(index) + 1) return false;
            var used = new HashSet<Vector3Int>();
            foreach (var point in candidate)
            {
                var cell = ArrowBoard.Key(point);
                if (!IsFree(cell) || !used.Add(cell)) return false;
            }

            paths[index] = candidate;
            directions[index] = direction;
            SetOccupied(index, true);
            if (Gap(index) <= 1 && (gapLimit < 0 || CountGaps() <= gapLimit) &&
                !ClosesCycle(index, index, new bool[paths.Length])) return true;
            SetOccupied(index, false);
            return false;
        }

        bool TryRerouteGap(int gapCount, bool[] rerouted)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                if (rerouted[i] || Gap(i) < 2) continue;
                if (!TryReroute(i, gapCount)) continue;
                rerouted[i] = true;
                return true;
            }
            return false;
        }

        bool TryReroute(int index, int gapCount)
        {
            var original = paths[index];
            var direction = directions[index];
            SetOccupied(index, false);
            var source = new List<Vector3>(original);
            source.Reverse();
            if (sourcePaths != null)
            {
                var reversedDirection = source[source.Count - 1] - source[source.Count - 2];
                int limit = gapCount == 0 ? -1 : gapCount - 1;
                if (TryFixedRoute(index, source, reversedDirection, limit)) return true;
                paths[index] = original;
                directions[index] = direction;
                SetOccupied(index, true);
                return false;
            }
            for (int end = source.Count; end >= 2; end--)
            {
                if (TryRoute(index, source, end, gapCount)) return true;
            }
            for (int end = original.Count - 1; end >= 2; end--)
            {
                if (TryRoute(index, original, end, gapCount)) return true;
            }
            var head = original[original.Count - 1];
            foreach (var newDirection in CardinalDirections)
            {
                var tail = head - newDirection;
                if (IsFree(ArrowBoard.Key(tail)) &&
                    TryRoute(index, new List<Vector3> { tail, head }, 2, gapCount)) return true;
            }
            paths[index] = original;
            directions[index] = direction;
            SetOccupied(index, true);
            return false;
        }

        bool TryRoute(int index, List<Vector3> source, int count, int gapCount)
        {
            var candidate = source.GetRange(0, count);
            var direction = candidate[count - 1] - candidate[count - 2];
            if (Mathf.Abs(direction.x) + Mathf.Abs(direction.y) != GridStep) return false;
            if (ArrowBoard.TrimSelfPointingTail(candidate, direction)) return false;
            paths[index] = candidate;
            directions[index] = direction;
            SetOccupied(index, true);
            Extend(index, Gap(index));
            if (!ClosesCycle(index, index, new bool[paths.Length]) &&
                (gapCount == 0 || CountGaps() < gapCount)) return true;
            SetOccupied(index, false);
            return false;
        }

        bool TryFitDisplaced(int index)
        {
            if (sourcePaths != null) return TryFitLengthDisplaced(index);
            var anchor = originalHeads[index];
            var path = paths[index];
            for (int radius = 0; radius <= 2; radius++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                int dy = radius - Mathf.Abs(dx);
                if (TryFitPair(index, anchor + new Vector3(dx, dy))) return true;
                if (dy > 0 && TryFitPair(index, anchor + new Vector3(dx, -dy))) return true;
            }
            path.Clear();
            return false;
        }

        bool TryFitLengthDisplaced(int index)
        {
            var source = sourcePaths[index];
            var anchor = ArrowBoard.Key(originalHeads[index]);
            for (int radius = 0; radius < width + height; radius++)
            for (int x = Mathf.Max(0, anchor.x - radius); x <= Mathf.Min(width - 1, anchor.x + radius); x++)
            {
                int dy = radius - Mathf.Abs(anchor.x - x);
                if (TryFitLengthAt(index, new Vector3Int(x, anchor.y + dy, 0), source)) return true;
                if (dy != 0 && TryFitLengthAt(index, new Vector3Int(x, anchor.y - dy, 0), source)) return true;
            }
            if (TryFitBentLength(index)) return true;
            paths[index] = new List<Vector3>();
            return false;
        }

        bool TryFitBentLength(int index)
        {
            // Bound the search in tight layouts. A complete candidate still
            // has to respect the one-step gap and pass the dependency check.
            int budget = 12000;
            var route = new List<Vector3>();
            var used = new HashSet<Vector3Int>();
            for (int y = 0; y < height && budget > 0; y++)
            for (int x = 0; x < width && budget > 0; x++)
            {
                var head = new Vector3Int(x, y, 0);
                if (!IsFree(head)) continue;
                foreach (var direction in CardinalDirections)
                {
                    var forward = ArrowBoard.Key(direction);
                    var previous = head - forward;
                    if (!IsFree(previous) || !CanEndAt(head, forward)) continue;
                    route.Clear();
                    used.Clear();
                    route.Add(head);
                    route.Add(previous);
                    used.Add(head);
                    used.Add(previous);
                    if (SearchVacantTail(index, head, forward, previous, -forward, route, used, ref budget))
                        return true;
                }
            }
            return false;
        }

        bool CanEndAt(Vector3Int head, Vector3Int direction)
        {
            for (var cell = head + direction; InBounds(cell); cell += direction)
                if (occupied.ContainsKey(cell)) return cell == head + direction;
            return true;
        }

        bool SearchVacantTail(int index, Vector3Int head, Vector3Int forward, Vector3Int tail,
            Vector3Int backward, List<Vector3> route, HashSet<Vector3Int> used, ref int budget)
        {
            if (budget-- <= 0) return false;
            if (route.Count == SourceSteps(index) + 1)
            {
                var candidate = new List<Vector3>(route);
                candidate.Reverse();
                return TryFixedRoute(index, candidate, forward, 0);
            }

            var side = new Vector3Int(-backward.y, backward.x, 0);
            for (int turn = 0; turn < 3 && budget > 0; turn++)
            {
                var step = turn == 0 ? backward : turn == 1 ? side : -side;
                var cell = tail + step;
                var relative = cell - head;
                if (relative.x * forward.y == relative.y * forward.x &&
                    relative.x * forward.x + relative.y * forward.y > 0) continue;
                if (!IsFree(cell) || !used.Add(cell)) continue;
                route.Add(cell);
                if (SearchVacantTail(index, head, forward, cell, step, route, used, ref budget)) return true;
                route.RemoveAt(route.Count - 1);
                used.Remove(cell);
            }
            return false;
        }

        bool TryFitLengthAt(int index, Vector3Int head, List<Vector3> source)
        {
            if (!IsFree(head)) return false;
            if (source.Count >= 2)
            {
                var candidate = new List<Vector3>(source.Count);
                var delta = (Vector3)head - source[source.Count - 1];
                foreach (var point in source) candidate.Add(point + delta);
                var direction = candidate[candidate.Count - 1] - candidate[candidate.Count - 2];
                if (TryFixedRoute(index, candidate, direction, 0)) return true;
            }
            foreach (var direction in CardinalDirections)
            {
                if (!IsFree(ArrowBoard.Key((Vector3)head - direction * SourceSteps(index)))) continue;
                if (TryFixedRoute(index, StraightRoute(head, direction, SourceSteps(index)), direction, 0)) return true;
            }
            paths[index] = new List<Vector3>();
            return false;
        }

        bool TryFitPair(int index, Vector3 head)
        {
            if (!IsFree(ArrowBoard.Key(head))) return false;
            var path = paths[index];
            foreach (var direction in CardinalDirections)
            {
                var tail = head - direction * GridStep;
                if (!IsFree(ArrowBoard.Key(tail))) continue;
                path.Add(tail);
                path.Add(head);
                directions[index] = direction;
                SetOccupied(index, true);
                if (CountGaps() == 0 && !ClosesCycle(index, index, new bool[paths.Length])) return true;
                SetOccupied(index, false);
                path.Clear();
            }
            return false;
        }

        bool ClosesCycle(int target, int index, bool[] visited)
        {
            if (visited[index]) return false;
            visited[index] = true;
            var path = paths[index];
            var direction = ArrowBoard.Key(directions[index]);
            for (var cell = ArrowBoard.Key(path[path.Count - 1]) + direction;
                InBounds(cell); cell += direction)
            {
                if (!occupied.TryGetValue(cell, out int owner) || owner == index) continue;
                if (owner == target || ClosesCycle(target, owner, visited)) return true;
            }
            return false;
        }

        int FindCycle()
        {
            var visited = new byte[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                int cycle = Visit(i, visited);
                if (cycle >= 0) return cycle;
            }
            return -1;
        }

        int Visit(int index, byte[] visited)
        {
            if (visited[index] == 1) return index;
            if (visited[index] == 2 || paths[index].Count == 0) return -1;
            visited[index] = 1;
            var path = paths[index];
            var direction = ArrowBoard.Key(directions[index]);
            for (var cell = ArrowBoard.Key(path[path.Count - 1]) + direction;
                InBounds(cell); cell += direction)
            {
                if (!occupied.TryGetValue(cell, out int owner) || owner == index) continue;
                int cycle = Visit(owner, visited);
                if (cycle >= 0) return cycle;
            }
            visited[index] = 2;
            return -1;
        }
    }
}
