using System.Drawing.Drawing2D;

namespace ArrowLevelEditor;

sealed class EditorWindow : Form
{
    readonly List<ArrowLevel> levels = new();
    readonly Stack<ArrowLevel> history = new();
    readonly List<Point> draft = new();
    readonly ComboBox levelPicker = new() { Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly ListBox arrowPicker = new() { Dock = DockStyle.Fill };
    readonly TextBox nameBox = new() { Width = 240 };
    readonly Label status = new() { Dock = DockStyle.Bottom, Height = 33, Padding = new Padding(8, 7, 0, 0) };
    readonly Label routeLabel = new() { Height = 62, Dock = DockStyle.Top, AutoEllipsis = true };
    readonly Panel viewport = new() { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.WhiteSmoke };
    readonly BoardCanvas canvas = new();
    readonly Button draw, editRoute, finish, undoPoint, cancel, reverse, remove, undo, verify, export, exportPack;
    readonly Button previous, next, exportImage, exportImages;
    readonly Label levelCount = new() { AutoSize = true, Margin = new Padding(5, 9, 5, 0) };
    bool refreshing;
    bool drawing;
    bool loading;
    int editingIndex = -1;
    int CurrentIndex => levelPicker.SelectedIndex;
    ArrowLevel? Current => CurrentIndex >= 0 && CurrentIndex < levels.Count ? levels[CurrentIndex] : null;

    public EditorWindow()
    {
        Text = "Arrow Level Editor";
        Font = new Font("Segoe UI", 10);
        ClientSize = new Size(1180, 760);
        MinimumSize = new Size(760, 480);
        KeyPreview = true;
        AllowDrop = true;

        var fileBar = Bar();
        AddButton(fileBar, "Mở .bytes / .json", async () => await OpenFiles());
        AddButton(fileBar, "Level mới", NewLevel);
        previous = AddButton(fileBar, "‹ Trước", () => MoveLevel(-1));
        fileBar.Controls.Add(levelPicker);
        next = AddButton(fileBar, "Sau ›", () => MoveLevel(1));
        fileBar.Controls.Add(levelCount);
        fileBar.Controls.Add(nameBox);
        export = AddButton(fileBar, "Xuất màn JSON", () => Save(false));
        exportPack = AddButton(fileBar, "Xuất cả bộ JSON", () => Save(true));
        exportImage = AddButton(fileBar, "Xuất PNG", SaveImage);
        exportImages = AddButton(fileBar, "Xuất nhiều PNG", SaveImages);

        var editBar = Bar();
        draw = AddButton(editBar, "Vẽ mũi tên", BeginArrow);
        editRoute = AddButton(editBar, "Sửa đường", EditRoute);
        finish = AddButton(editBar, "Lưu mũi tên", FinishArrow);
        undoPoint = AddButton(editBar, "Lùi điểm", () => { draft.RemoveAt(draft.Count - 1); RefreshBoard(); });
        cancel = AddButton(editBar, "Hủy nét", () => { ClearDraft(); RefreshBoard(); });
        reverse = AddButton(editBar, "Đảo đầu", ReverseArrow);
        remove = AddButton(editBar, "Xóa mũi tên", RemoveArrow);
        undo = AddButton(editBar, "Hoàn tác", Undo);
        verify = AddButton(editBar, "Kiểm tra lời giải", Verify);
        AddButton(editBar, "−", () => Zoom(canvas.CellSize - 2));
        AddButton(editBar, "+", () => Zoom(canvas.CellSize + 2));
        AddButton(editBar, "Vừa khung", () => Zoom((Math.Min(viewport.ClientSize.Width, viewport.ClientSize.Height) - 80) / LevelFiles.Size));

        viewport.Controls.Add(canvas);
        var side = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        side.Controls.Add(arrowPicker);
        side.Controls.Add(routeLabel);
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        body.Controls.Add(viewport, 0, 0);
        body.Controls.Add(side, 1, 0);

        Controls.Add(body);
        Controls.Add(status);
        Controls.Add(editBar);
        Controls.Add(fileBar);

        canvas.MouseClick += (_, e) => OnBoardClick(e.Location);
        DragEnter += (_, e) => CheckDrop(e);
        DragDrop += async (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
                await LoadFiles(paths.Where(IsLevelFile));
        };
        canvas.MouseMove += (_, e) =>
        {
            var point = canvas.CellAt(e.Location);
            canvas.AccessibleDescription = point is Point p ? LabelPoint(p) : "Lưới 140 × 140";
            if (!drawing && point is Point cell)
                routeLabel.Text = SelectedRoute() + "\r\nÔ " + LabelPoint(cell);
        };
        nameBox.TextChanged += (_, _) =>
        {
            if (!refreshing && Current is { Editable: true } level)
            {
                int index = CurrentIndex;
                levels[index] = level with { Name = nameBox.Text };
                refreshing = true;
                levelPicker.Items[index] = $"{index + 1}. {nameBox.Text}";
                refreshing = false;
            }
        };
        levelPicker.SelectedIndexChanged += (_, _) =>
        {
            if (refreshing) return;
            ClearDraft(); history.Clear();
            viewport.AutoScrollPosition = Point.Empty;
            RefreshBoard();
            ShowLevelStatus();
        };
        arrowPicker.SelectedIndexChanged += (_, _) =>
        {
            if (!refreshing) UpdateSelection();
        };
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && drawing && !finish.Enabled) return;
            if (e.KeyCode == Keys.Enter && drawing) { FinishArrow(); e.Handled = true; }
            if (e.KeyCode == Keys.Escape && drawing) { cancel.PerformClick(); e.Handled = true; }
        };
        NewLevel();
    }

    static FlowLayoutPanel Bar() => new()
    {
        Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(6), BackColor = Color.White
    };

    static Button AddButton(FlowLayoutPanel bar, string text, Action action)
    {
        var button = new Button { AutoSize = true, Text = text, Padding = new Padding(5, 2, 5, 2) };
        button.Click += (_, _) => action();
        bar.Controls.Add(button);
        return button;
    }

    static string LabelPoint(Point p) => $"C{p.X + 1}/H{p.Y + 1}";
    void Say(string text) => status.Text = text;

    void NewLevel()
    {
        if (loading || draft.Count != 0) { Say("Chờ mở file hoặc lưu nét đang vẽ trước."); return; }
        levels.Add(new ArrowLevel("level-moi", Array.Empty<Point[]>(), true));
        RefreshLevels(levels.Count - 1);
    }

    async Task OpenFiles()
    {
        if (loading || draft.Count != 0) { Say("Lưu hoặc hủy nét đang vẽ trước khi mở file."); return; }
        using var dialog = new OpenFileDialog
        {
            Filter = "Level Arrow (*.json;*.bytes)|*.json;*.bytes",
            Multiselect = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        await LoadFiles(dialog.FileNames);
    }

    async Task LoadFiles(IEnumerable<string> paths)
    {
        if (loading || draft.Count != 0) { Say("Lưu hoặc hủy nét đang vẽ trước khi mở file."); return; }
        int first = levels.Count;
        var errors = new List<string>();
        loading = true;
        RefreshBoard();
        Say("Đang đọc file…");
        try
        {
            foreach (string path in paths)
            {
                try
                {
                    levels.AddRange(LevelFiles.Read(await File.ReadAllTextAsync(path), Path.GetFileNameWithoutExtension(path)));
                }
                catch (Exception error) when (error is IOException or InvalidDataException or System.Text.Json.JsonException
                    or InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException or FormatException)
                {
                    errors.Add(Path.GetFileName(path) + ": " + error.Message);
                }
            }
            if (levels.Count > first)
            {
                // Drop the empty startup level once the first real file is open.
                if (first == 1 && levels[0].Name == "level-moi" && levels[0].Routes.Count == 0)
                { levels.RemoveAt(0); first = 0; }
                RefreshLevels(first);
            }
            if (errors.Count != 0)
                MessageBox.Show(this, string.Join("\r\n", errors.Take(8)), "File không đọc được", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { loading = false; RefreshBoard(arrowPicker.SelectedIndex); }
    }

    static bool IsLevelFile(string path) =>
        Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(path).Equals(".bytes", StringComparison.OrdinalIgnoreCase);

    void CheckDrop(DragEventArgs e)
    {
        e.Effect = !loading && draft.Count == 0 &&
            e.Data?.GetData(DataFormats.FileDrop) is string[] paths && paths.Any(IsLevelFile)
            ? DragDropEffects.Copy : DragDropEffects.None;
    }

    void MoveLevel(int offset)
    {
        int index = CurrentIndex + offset;
        if (levelPicker.Enabled && index >= 0 && index < levels.Count) levelPicker.SelectedIndex = index;
    }

    void RefreshLevels(int index)
    {
        refreshing = true;
        levelPicker.Items.Clear();
        for (int i = 0; i < levels.Count; i++) levelPicker.Items.Add($"{i + 1}. {levels[i].Name}");
        levelPicker.SelectedIndex = index;
        refreshing = false;
        history.Clear(); ClearDraft();
        RefreshBoard();
        ShowLevelStatus();
    }

    void ShowLevelStatus() => Say(Current?.Editable == true
        ? "Chọn Vẽ mũi tên, rồi bấm đuôi → điểm rẽ → đầu."
        : "File nguồn đang mở ở chế độ xem.");

    void BeginArrow()
    {
        drawing = true;
        arrowPicker.ClearSelected();
        Say("Bấm ô đuôi, các điểm rẽ, rồi ô đầu.");
        RefreshBoard();
        canvas.Focus();
    }

    void EditRoute()
    {
        if (Current is not { Editable: true } level || arrowPicker.SelectedIndex < 0) return;
        editingIndex = arrowPicker.SelectedIndex;
        draft.AddRange(level.Routes[editingIndex]);
        drawing = true;
        Say("Bấm Lùi điểm về chỗ muốn sửa, rồi chọn điểm mới.");
        RefreshBoard();
        canvas.Focus();
    }

    void ClearDraft()
    {
        draft.Clear();
        drawing = false;
        editingIndex = -1;
    }

    void FinishArrow()
    {
        if (Current is not { Editable: true } level || draft.Count < 2) return;
        var route = draft.ToArray();
        int index = editingIndex;
        try { LevelFiles.CheckRoute(route, level.Routes.Where((_, i) => i != index)); }
        catch (InvalidDataException error) { Say(error.Message); return; }
        ClearDraft();
        if (index < 0) ReplaceRoutes(level.Routes.Append(route).ToArray(), level.Routes.Count);
        else
        {
            var routes = level.Routes.ToArray();
            routes[index] = route;
            ReplaceRoutes(routes, index);
        }
        Say("Đã lưu mũi tên.");
    }

    void OnBoardClick(Point mouse)
    {
        if (loading || Current is not { } level || canvas.CellAt(mouse) is not Point point) return;
        if (!drawing)
        {
            arrowPicker.SelectedIndex = canvas.FindRoute(point);
            return;
        }
        if (!level.Editable) return;
        if (draft.Count > 0)
        {
            Point previous = draft[^1];
            if (point == previous) return;
            if (point.X != previous.X && point.Y != previous.Y)
            { Say("Điểm kế tiếp phải cùng hàng hoặc cùng cột."); return; }
        }
        draft.Add(point);
        Say($"Đang vẽ tại {LabelPoint(point)} · {draft.Count} điểm");
        RefreshBoard();
    }

    void ReplaceRoutes(Point[][] routes, int selected)
    {
        if (Current is not { } level) return;
        history.Push(level);
        levels[CurrentIndex] = level with { Routes = routes };
        RefreshBoard(selected);
    }

    void ReverseArrow()
    {
        if (Current is not { Editable: true } level || arrowPicker.SelectedIndex < 0) return;
        int index = arrowPicker.SelectedIndex;
        var routes = level.Routes.ToArray();
        var reversed = routes[index].Reverse().ToArray();
        try { LevelFiles.CheckRoute(reversed, routes.Where((_, i) => i != index)); }
        catch (InvalidDataException error) { Say(error.Message); return; }
        routes[index] = reversed;
        ReplaceRoutes(routes, index);
    }

    void RemoveArrow()
    {
        if (Current is not { Editable: true } level || arrowPicker.SelectedIndex < 0) return;
        ReplaceRoutes(level.Routes.Where((_, index) => index != arrowPicker.SelectedIndex).ToArray(), -1);
    }

    void Undo()
    {
        if (!history.TryPop(out var before) || Current is not { } level) return;
        levels[CurrentIndex] = before with { Name = level.Name };
        RefreshBoard();
    }

    void Verify()
    {
        if (Current is not { Editable: true } level) return;
        int solved = LevelFiles.Solve(level.Routes).Length;
        Say(solved == level.Routes.Count ? $"Giải được cả {solved} mũi tên." :
            $"Dọn được {solved}/{level.Routes.Count} mũi tên. Các mũi còn lại đang chặn nhau.");
    }

    void Save(bool pack)
    {
        if (Current is not { Editable: true } level) return;
        var items = pack ? levels.Where(item => item.Editable && item.Routes.Count != 0).ToArray() : new[] { level };
        if (items.Length == 0) return;
        string name = pack ? "bo-level-arrow" : (string.IsNullOrWhiteSpace(level.Name) ? "level-moi" : level.Name);
        string json;
        try { json = LevelFiles.Write(items, name, pack); }
        catch (InvalidDataException error) { Say(error.Message); return; }
        using var dialog = new SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = string.Concat(name.Select(c =>
            Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)) + ".json", DefaultExt = "json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { File.WriteAllText(dialog.FileName, json); Say($"Đã xuất {items.Length} màn."); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { Say(error.Message); }
    }

    static string FileStem(string name)
    {
        var stem = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim().TrimEnd('.');
        if (stem.Length == 0) return "level";
        return stem[..Math.Min(stem.Length, 80)];
    }

    void SaveImage()
    {
        if (Current is not { Editable: true } level || level.Routes.Count == 0) return;
        using var dialog = new SaveFileDialog
        {
            Filter = "PNG (*.png)|*.png", FileName = FileStem(level.Name) + ".png", DefaultExt = "png"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { BoardCanvas.SavePng(level, dialog.FileName); Say("Đã xuất PNG của màn đang xem."); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or System.Runtime.InteropServices.ExternalException) { Say(error.Message); }
    }

    void SaveImages()
    {
        var items = levels.Select((level, index) => (level, index))
            .Where(item => item.level.Editable && item.level.Routes.Count != 0).ToArray();
        if (items.Length == 0) return;
        using var dialog = new FolderBrowserDialog { Description = "Chọn nơi lưu ảnh các level" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        string stem = "arrow-levels-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string folder = Path.Combine(dialog.SelectedPath, stem);
        int suffix = 2;
        while (Directory.Exists(folder) || File.Exists(folder))
            folder = Path.Combine(dialog.SelectedPath, stem + "-" + suffix++);
        int written = 0;
        try
        {
            Directory.CreateDirectory(folder);
            foreach (var (level, index) in items)
            {
                BoardCanvas.SavePng(level, Path.Combine(folder, $"{index + 1:D3}-{FileStem(level.Name)}.png"));
                written++;
            }
            Say($"Đã xuất {written} ảnh trong {Path.GetFileName(folder)}.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or System.Runtime.InteropServices.ExternalException)
        { Say($"Đã xuất {written}/{items.Length} ảnh. {error.Message}"); }
    }

    void Zoom(int size)
    {
        canvas.SetCellSize(Math.Clamp(size, 2, 42));
        canvas.Invalidate();
    }

    string SelectedRoute()
    {
        if (Current is not { } level || arrowPicker.SelectedIndex < 0) return "";
        return string.Join(" → ", level.Routes[arrowPicker.SelectedIndex].Reverse().Select(LabelPoint));
    }

    void UpdateSelection()
    {
        var editable = Current?.Editable == true && !loading && !drawing;
        editRoute.Enabled = reverse.Enabled = remove.Enabled = editable && arrowPicker.SelectedIndex >= 0;
        canvas.Selected = arrowPicker.SelectedIndex;
        routeLabel.Text = SelectedRoute();
        canvas.Invalidate();
    }

    void RefreshBoard(int selected = -1)
    {
        var level = Current;
        bool editable = level?.Editable == true;
        refreshing = true;
        if (nameBox.Text != level?.Name) nameBox.Text = level?.Name ?? "";
        nameBox.ReadOnly = !editable || loading;
        arrowPicker.BeginUpdate();
        arrowPicker.Items.Clear();
        if (level != null)
            for (int i = 0; i < level.Routes.Count; i++)
                arrowPicker.Items.Add($"{i + 1}. {LabelPoint(level.Routes[i][^1])}");
        arrowPicker.SelectedIndex = selected;
        arrowPicker.EndUpdate();
        refreshing = false;
        levelPicker.Enabled = !loading && draft.Count == 0;
        previous.Enabled = levelPicker.Enabled && CurrentIndex > 0;
        next.Enabled = levelPicker.Enabled && CurrentIndex >= 0 && CurrentIndex < levels.Count - 1;
        levelCount.Text = CurrentIndex >= 0 ? $"{CurrentIndex + 1}/{levels.Count}" : "0";
        draw.Enabled = editable && !loading && !drawing;
        finish.Enabled = editable && draft.Count >= 2;
        undoPoint.Enabled = drawing && draft.Count > 0;
        cancel.Enabled = drawing;
        undo.Enabled = editable && !drawing && history.Count > 0;
        verify.Enabled = export.Enabled = editable && !loading && !drawing && level!.Routes.Count > 0;
        exportPack.Enabled = editable && !loading && !drawing && levels.Count(item => item.Editable && item.Routes.Count != 0) > 1;
        exportImage.Enabled = editable && !loading && !drawing && level!.Routes.Count > 0;
        exportImages.Enabled = !loading && !drawing && levels.Count(item => item.Editable && item.Routes.Count != 0) > 1;
        canvas.Routes = level?.Routes ?? Array.Empty<Point[]>();
        canvas.Draft = draft;
        canvas.Editing = editingIndex;
        UpdateSelection();
    }
}

sealed class BoardCanvas : Control
{
    const int MarginSize = 40;
    public int CellSize { get; private set; } = 16;
    internal IReadOnlyList<Point[]> Routes = Array.Empty<Point[]>();
    internal IReadOnlyList<Point> Draft = Array.Empty<Point>();
    internal int Selected = -1;
    internal int Editing = -1;

    public BoardCanvas()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(255, 249, 234);
        SetCellSize(CellSize);
    }

    public void SetCellSize(int size)
    {
        CellSize = size;
        Width = Height = MarginSize * 2 + size * LevelFiles.Size;
    }

    public Point? CellAt(Point pointer)
    {
        var point = new Point((pointer.X - MarginSize) / CellSize, (pointer.Y - MarginSize) / CellSize);
        return pointer.X >= MarginSize && pointer.Y >= MarginSize &&
            point.X < LevelFiles.Size && point.Y < LevelFiles.Size ? point : null;
    }

    public int FindRoute(Point point)
    {
        for (int i = 0; i < Routes.Count; i++)
        {
            var route = Routes[i];
            if (route.Contains(point)) return i;
            for (int j = 1; j < route.Length; j++)
            {
                Point a = route[j - 1], b = route[j];
                if (a.X == b.X && point.X == a.X && point.Y >= Math.Min(a.Y, b.Y) && point.Y <= Math.Max(a.Y, b.Y)) return i;
                if (a.Y == b.Y && point.Y == a.Y && point.X >= Math.Min(a.X, b.X) && point.X <= Math.Max(a.X, b.X)) return i;
            }
        }
        return -1;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        int span = LevelFiles.Size * CellSize;
        using var thin = new Pen(Color.FromArgb(223, 217, 226));
        using var strong = new Pen(Color.FromArgb(170, 160, 184));
        using var labels = new SolidBrush(Color.FromArgb(82, 69, 103));
        int every = Math.Max(1, (int)Math.Ceiling(35f / CellSize));
        for (int i = 0; i <= LevelFiles.Size; i++)
        {
            int coordinate = MarginSize + i * CellSize;
            var pen = i % 10 == 0 ? strong : thin;
            graphics.DrawLine(pen, coordinate, MarginSize, coordinate, MarginSize + span);
            graphics.DrawLine(pen, MarginSize, coordinate, MarginSize + span, coordinate);
            if (i < LevelFiles.Size && (i == 0 || i == LevelFiles.Size - 1 || (i + 1) % every == 0))
            {
                graphics.DrawString("C" + (i + 1), Font, labels, coordinate, MarginSize - 24);
                graphics.DrawString("H" + (i + 1), Font, labels, 2, coordinate);
            }
        }
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var origin = new Point(MarginSize, MarginSize);
        for (int i = 0; i < Routes.Count; i++)
            if (i != Selected && i != Editing) DrawRoute(graphics, Routes[i], Color.FromArgb(125, 56, 174), CellSize, origin);
        if (Selected >= 0 && Selected < Routes.Count && Selected != Editing)
            DrawRoute(graphics, Routes[Selected], Color.DarkOrange, CellSize, origin);
        if (Draft.Count > 0) DrawRoute(graphics, Draft, Color.Teal, CellSize, origin);
    }

    internal static void SavePng(ArrowLevel level, string path)
    {
        using var image = RenderImage(level);
        image.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    internal static Bitmap RenderImage(ArrowLevel level)
    {
        if (!level.Editable || level.Routes.Count == 0)
            throw new InvalidDataException("Chỉ xuất ảnh từ level thiết kế có mũi tên.");
        const int cellSize = 26;
        const int padding = 24;
        var positions = level.Routes.SelectMany(route => route).ToArray();
        int minX = positions.Min(p => p.X), maxX = positions.Max(p => p.X);
        int minY = positions.Min(p => p.Y), maxY = positions.Max(p => p.Y);
        int width = (maxX - minX + 1) * cellSize + padding * 2;
        int height = (maxY - minY + 1) * cellSize + padding * 2;
        var image = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(image);
        graphics.Clear(Color.FromArgb(255, 249, 234));
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var origin = new Point(padding - minX * cellSize, padding - minY * cellSize);
        foreach (var route in level.Routes)
            DrawRoute(graphics, route, Color.FromArgb(125, 56, 174), cellSize, origin);
        return image;
    }

    static void DrawRoute(Graphics graphics, IReadOnlyList<Point> route, Color color, int cellSize, Point origin)
    {
        var points = route.Select(p => new PointF(origin.X + (p.X + .5f) * cellSize,
            origin.Y + (p.Y + .5f) * cellSize)).ToArray();
        using var pen = new Pen(color, Math.Max(1f, cellSize * .13f))
            { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var fill = new SolidBrush(color);
        PointF head = points[^1];
        if (points.Length == 1) { graphics.FillEllipse(fill, head.X - 2, head.Y - 2, 4, 4); return; }
        graphics.DrawLines(pen, points);
        PointF neck = points[^2];
        float dx = head.X - neck.X, dy = head.Y - neck.Y;
        float magnitude = MathF.Sqrt(dx * dx + dy * dy);
        if (magnitude <= 0) return;
        dx /= magnitude; dy /= magnitude;
        float length = Math.Max(2, cellSize * .48f), wing = length * .48f;
        graphics.FillPolygon(fill, new[]
        {
            head,
            new PointF(head.X - dx * length - dy * wing, head.Y - dy * length + dx * wing),
            new PointF(head.X - dx * length + dy * wing, head.Y - dy * length - dx * wing)
        });
    }
}
