namespace Ripple.Services;

public static class Storage
{
    public static string DataDir
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ripple");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string Root { get; set; } = DataDir;

    public static string AudioDir => Ensure(Path.Combine(Root, "audio"));

    public static string ModulesDir => Ensure(Path.Combine(Root, "modules"));

    public static string WhisperDir => Ensure(Path.Combine(Root, "whisper"));

    public static string LibraryPath => Path.Combine(DataDir, "library.json");
    public static string ConfigPath => Path.Combine(DataDir, "config.json");

    private static string Ensure(string dir)
    {
        Directory.CreateDirectory(dir);

        return dir;
    }

    public static long GetSize(string dir)
    {
        try
        {
            return Directory.Exists(dir)
                ? new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length)
                : 0;
        }

        catch
        {
            return 0;
        }
    }
}
