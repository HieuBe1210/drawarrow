namespace ArrowLevelEditor;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length is 2 or 3 && args[0] == "--verify")
        {
            var levels = LevelFiles.Read(File.ReadAllText(args[1]), "test");
            var roundTrip = LevelFiles.Read(LevelFiles.Write(levels, "test", true), "test");
            if (levels.Count != roundTrip.Count) throw new InvalidDataException("Level count changed.");
            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i].Routes.Count != roundTrip[i].Routes.Count) throw new InvalidDataException("Arrow count changed.");
                for (int j = 0; j < levels[i].Routes.Count; j++)
                    if (!levels[i].Routes[j].SequenceEqual(roundTrip[i].Routes[j]))
                        throw new InvalidDataException("Route changed.");
            }
            if (args.Length == 3)
            {
                Directory.CreateDirectory(args[2]);
                for (int i = 0; i < levels.Count; i++)
                    BoardCanvas.SavePng(levels[i], Path.Combine(args[2], $"{i + 1:D3}.png"));
            }
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new EditorWindow());
    }
}
