namespace Ripple.Services;

public static class TempCleanup
{
    public static void Run()
    {
        try
        {
            var tmp = Path.GetTempPath();

            foreach (var wav in Directory.GetFiles(tmp, "ripple-*.wav"))
            {
                TryDelete(wav);
            }

            foreach (var dir in Directory.GetDirectories(tmp, "ripple-whisper-*"))
            {
                TryDeleteDirectory(dir);
            }
        }

        catch { }
    }

    public static void TryDeleteTempFile(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var tmp = Path.GetTempPath();

            if (File.Exists(path) && path.StartsWith(tmp, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(path);
            }
        }

        catch { }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { }
    }
}
