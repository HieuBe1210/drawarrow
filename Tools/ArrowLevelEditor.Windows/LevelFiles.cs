using System.Drawing;
using System.Text.Json;

namespace ArrowLevelEditor;

sealed record ArrowLevel(string Name, IReadOnlyList<Point[]> Routes, bool Editable);

static class LevelFiles
{
    public const int Size = 140;
    const string LevelFormat = "arrow-level-editor-v1";
    const string PackFormat = "arrow-level-editor-pack-v1";

    public static IReadOnlyList<ArrowLevel> Read(string text, string fallbackName)
    {
        using var document = JsonDocument.Parse(text.TrimStart('\uFEFF'));
        var root = document.RootElement;
        if (ReadString(root, "format") == PackFormat)
        {
            var levels = root.GetProperty("levels").EnumerateArray()
                .Select((level, index) => ReadLevel(level, $"{fallbackName} - {index + 1}")).ToArray();
            if (levels.Length == 0) throw new InvalidDataException("Bộ level trống.");
            return levels;
        }
        return new[] { ReadLevel(root, fallbackName) };
    }

    static string? ReadString(JsonElement root, string name)
        => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var field) &&
            field.ValueKind == JsonValueKind.String ? field.GetString() : null;

    static ArrowLevel ReadLevel(JsonElement data, string fallbackName)
    {
        bool shard = data.TryGetProperty("XSize", out var sourceWidth);
        int width = (shard ? sourceWidth : data.GetProperty("width")).GetInt32();
        int height = data.GetProperty(shard ? "YSize" : "height").GetInt32();
        if (width is < 1 or > Size || height is < 1 or > Size)
            throw new InvalidDataException("Bàn phải nằm trong lưới 140 × 140.");

        bool editable = !shard && ReadString(data, "format") == LevelFormat;
        var routes = data.GetProperty(shard ? "Arrows" : "arrows").EnumerateArray().Select(arrow =>
        {
            var points = arrow.GetProperty(shard ? "Points" : "points").EnumerateArray().Select(cell =>
            {
                int index = cell.GetInt32();
                if (index < 0 || index >= width * height) throw new InvalidDataException("Tọa độ ngoài bàn.");
                return new Point(index % width, height - 1 - index / width);
            }).Reverse().ToArray();
            if (points.Length == 0) throw new InvalidDataException("Mũi tên thiếu tọa độ.");
            return points;
        }).ToArray();

        if (editable)
        {
            var checkedRoutes = new List<Point[]>();
            foreach (var route in routes)
            {
                CheckRoute(route, checkedRoutes);
                checkedRoutes.Add(route);
            }
        }
        return new ArrowLevel(ReadString(data, "id") ?? fallbackName, routes, editable);
    }

    public static IEnumerable<Point> Cells(Point[] route)
    {
        yield return route[0];
        for (int i = 1; i < route.Length; i++)
        {
            Point start = route[i - 1], end = route[i];
            int dx = end.X - start.X, dy = end.Y - start.Y;
            if (dx != 0 && dy != 0) throw new InvalidDataException("Điểm kế tiếp phải cùng hàng hoặc cùng cột.");
            for (int step = 1; step <= Math.Abs(dx) + Math.Abs(dy); step++)
                yield return new Point(start.X + Math.Sign(dx) * step, start.Y + Math.Sign(dy) * step);
        }
    }

    public static void CheckRoute(Point[] route, IEnumerable<Point[]> other)
    {
        if (route.Length < 2 || route.Zip(route.Skip(1)).Any(pair => pair.First == pair.Second))
            throw new InvalidDataException("Mũi tên cần hai điểm phân biệt trở lên.");
        var cells = Cells(route).ToArray();
        if (cells.Any(point => !Inside(point))) throw new InvalidDataException("Đường đi ra ngoài lưới.");
        var own = cells.ToHashSet();
        if (own.Count != cells.Length) throw new InvalidDataException("Mũi tên giao với chính nó.");
        if (ExitCells(route).Any(own.Contains)) throw new InvalidDataException("Đầu mũi tên hướng vào thân của chính nó.");
        var occupied = other.SelectMany(Cells).ToHashSet();
        if (cells.Any(occupied.Contains)) throw new InvalidDataException("Đường đi chồng lên mũi tên khác.");
    }

    static bool Inside(Point point) => point.X >= 0 && point.Y >= 0 && point.X < Size && point.Y < Size;

    static IEnumerable<Point> ExitCells(Point[] route)
    {
        Point head = route[^1], neck = route[^2];
        var step = new Size(Math.Sign(head.X - neck.X), Math.Sign(head.Y - neck.Y));
        for (Point point = head + step; Inside(point); point += step) yield return point;
    }

    public static int[] Solve(IReadOnlyList<Point[]> routes)
    {
        var owner = new Dictionary<Point, int>();
        var dependents = new List<int>[routes.Count];
        var order = new List<int>();
        var required = new int[routes.Count];
        for (int i = 0; i < routes.Count; i++)
        {
            dependents[i] = new();
            foreach (var point in Cells(routes[i])) owner.Add(point, i);
        }
        for (int i = 0; i < routes.Count; i++)
        {
            var blockers = ExitCells(routes[i]).Where(owner.ContainsKey).Select(point => owner[point])
                .Where(index => index != i).Distinct().ToArray();
            foreach (int blocker in blockers) dependents[blocker].Add(i);
            required[i] = blockers.Length;
            if (required[i] == 0) order.Add(i);
        }
        for (int i = 0; i < order.Count; i++)
            foreach (int next in dependents[order[i]]) if (--required[next] == 0) order.Add(next);
        return order.ToArray();
    }

    public static string Write(IReadOnlyList<ArrowLevel> levels, string name, bool pack)
    {
        var entries = levels.Select(level =>
        {
            if (!level.Editable) throw new InvalidDataException("Tệp này chỉ dùng để xem.");
            var solution = Solve(level.Routes);
            if (solution.Length == 0 || solution.Length != level.Routes.Count)
                throw new InvalidDataException($"Màn {level.Name} đang trống hoặc các mũi tên đang chặn nhau.");
            var points = level.Routes.SelectMany(route => route).ToArray();
            return new
            {
                format = LevelFormat, id = level.Name, width = Size, height = Size,
                packedFitBounds = new[] { points.Max(p => p.X) - points.Min(p => p.X), points.Max(p => p.Y) - points.Min(p => p.Y) },
                arrows = level.Routes.Select(route => new
                {
                    points = route.Reverse().Select(p => (Size - p.Y - 1) * Size + p.X).ToArray(),
                    dx = Math.Sign(route[^1].X - route[^2].X),
                    dy = Math.Sign(route[^2].Y - route[^1].Y)
                }).ToArray(),
                solution
            };
        }).ToArray();
        var options = new JsonSerializerOptions { WriteIndented = true };
        return pack
            ? JsonSerializer.Serialize(new { format = PackFormat, name, levels = entries }, options)
            : JsonSerializer.Serialize(entries.Single(), options);
    }
}
