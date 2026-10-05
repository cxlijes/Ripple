using System.Globalization;
using System.Text;

namespace Ripple.Services;

public class MediaService : IMediaService
{
    private readonly AppConfig _config;
    private readonly IProcessRunner _runner;

    public IProgress<double>? Progress { get; set; }

    public MediaService(AppConfig config, IProcessRunner runner)
    {
        _config = config;
        _runner = runner;
    }

    public async Task<double> GetDurationAsync(
        string mediaPath, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        try
        {
            var exit = await _runner.RunAsync(
                ResolveTool("ffprobe"),
                $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{mediaPath}\"",
                TimeSpan.FromSeconds(15),
                line => { if (line != null) sb.AppendLine(line); }, null, ct);

            if (exit == 0 && double.TryParse(sb.ToString().Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d0))
            {
                return d0;
            }
        }

        catch { }

        sb.Clear();
        try
        {
            await _runner.RunAsync(
                ResolveTool("ffmpeg"),
                $"-i \"{mediaPath}\"",
                TimeSpan.FromSeconds(15),
                line => { if (line != null) sb.AppendLine(line); }, null, ct);
        }

        catch { }

        var m = System.Text.RegularExpressions.Regex.Match(sb.ToString(),
            @"Duration:\s*(\d+):(\d+):(\d+\.?\d*)");

        if (m.Success &&
            double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var hh) &&
            double.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mm) &&
            double.TryParse(m.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var ss))
        {
            return hh * 3600 + mm * 60 + ss;
        }

        return 0;
    }

    private string ResolveTool(string tool)
    {
        var exe = tool + ".exe";
        var dir = Path.GetDirectoryName(_config.FfmpegPath);

        if (!string.IsNullOrEmpty(dir))
        {
            var sibling = Path.Combine(dir, exe);

            if (File.Exists(sibling))
            {
                return sibling;
            }
        }

        return tool;
    }

    public async Task<string> ToWavAsync(
        string inputPath, CancellationToken ct = default)
    {
        var outPath = Path.Combine(Path.GetTempPath(),
            $"ripple-{Guid.NewGuid():N}.wav");
        var args = $"-y -i \"{inputPath}\" -vn -ac 1 -ar 16000 -c:a pcm_s16le \"{outPath}\"";
        var err = new StringBuilder();

        var exit = await _runner.RunAsync(
            ResolveTool("ffmpeg"), args,
            TimeSpan.FromHours(2),
            null,
            line => { if (line != null) err.AppendLine(line); }, ct);

        if (exit != 0 || !File.Exists(outPath))
        {
            throw new InvalidOperationException($"ffmpeg не смог конвертировать файл:\n{err}");
        }

        return outPath;
    }

    public async Task<string> DownloadUrlAsync(string url, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Storage.AudioDir);

        var before = Directory.GetFiles(Storage.AudioDir, "*.wav");

        var outTemplate = Path.Combine(Storage.AudioDir, "%(title)s.%(ext)s");
        var args =
            $"-x --audio-format wav --audio-quality 0 " +
            $"-o \"{outTemplate}\" --no-playlist --newline \"{url}\"";

        var exit = await _runner.RunAsync(
            _config.YtDlpPath, args,
            TimeSpan.FromHours(1),
            line =>
            {
                var pct = ParseYtDlpProgress(line);
                if (pct is null) return;
                Progress?.Report(Math.Clamp(pct.Value / 100.0, 0, 1));
            },
            null, ct);

        if (exit != 0)
        {
            throw new InvalidOperationException($"yt-dlp завершился с кодом {exit} (проверьте ссылку).");
        }

        var created = Directory.GetFiles(Storage.AudioDir, "*.wav")
            .Where(f => !before.Contains(f, StringComparer.OrdinalIgnoreCase))
            .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
            .FirstOrDefault();

        return created ?? throw new FileNotFoundException(
            "yt-dlp не создал новый .wav в папке аудио.");
    }

    private static double? ParseYtDlpProgress(string? line)
    {
        if (line == null) return null;
        var open = line.IndexOf('[', StringComparison.Ordinal);
        if (open < 0) return null;
        var close = line.IndexOf('%', open);
        if (close <= open + 1) return null;

        var text = line[(open + 1)..close].Trim().TrimEnd('.');
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var pct)
            ? pct
            : null;
    }
}
