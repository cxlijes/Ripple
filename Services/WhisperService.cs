using Ripple.Models;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ripple.Services;

public class WhisperService : ITranscriptionService
{
    private readonly AppConfig _config;
    private readonly IProcessRunner _runner;

    public WhisperService(AppConfig config, IProcessRunner runner)
    {
        _config = config;
        _runner = runner;
    }

    public static string ModelFileName(string model)
    {
        return Path.Combine(Storage.WhisperDir, $"{model}.pt");
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var exit = await _runner.RunAsync(
                _config.PythonExecutable, "-c \"import whisper; print('ok')\"",
                TimeSpan.FromSeconds(20));

            return exit == 0;
        }

        catch
        {
            return false;
        }
    }

    public async Task<List<TranscriptSegment>> TranscribeAsync(
        string audioPath,
        IProgress<double>? onProgress = null,
        CancellationToken ct = default)
    {
        var outDir = Path.Combine(Path.GetTempPath(), $"ripple-whisper-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outDir);

        var args =
            $"-m whisper \"{audioPath}\" " +
            $"--model {_config.WhisperModel} " +
            $"--model_dir \"{Storage.WhisperDir}\" " +
            "--language ru " +
            "--task transcribe " +
            "--output_format json " +
            $"--output_dir \"{outDir}\" " +
            "--verbose False";

        var stderr = new StringBuilder();
        var progressRegex = new Regex(@"(\d{1,3}(?:\.\d+)?)\s*%\|");

        var exit = await _runner.RunAsync(
            _config.PythonExecutable, args,
            TimeSpan.MaxValue,
            line =>
            {
                if (line == null)
                {
                    return;
                }

                var m = progressRegex.Match(line);

                if (m.Success && double.TryParse(m.Groups[1].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var pct))
                {
                    onProgress?.Report(Math.Clamp(pct / 100.0, 0, 1));
                }
            },
            line => { if (line != null) stderr.AppendLine(line); },
            ct);

        if (exit != 0)
        {
            throw new InvalidOperationException($"whisper завершился с кодом {exit}:\n{stderr}");
        }

        var jsonPath = Directory.GetFiles(outDir, "*.json").FirstOrDefault();

        if (jsonPath == null)
        {
            throw new FileNotFoundException("whisper не создал JSON-файл");
        }

        await using var stream = File.OpenRead(jsonPath);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var segments = new List<TranscriptSegment>();

        foreach (var seg in doc.RootElement.GetProperty("segments").EnumerateArray())
        {
            segments.Add(new TranscriptSegment
            {
                Start = seg.GetProperty("start").GetDouble(),
                End = seg.GetProperty("end").GetDouble(),
                Text = seg.GetProperty("text").GetString() ?? ""
            });
        }

        Directory.Delete(outDir, recursive: true);
        return segments;
    }
}
