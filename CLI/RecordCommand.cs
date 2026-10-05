using NAudio.Wave;
using Ripple.Services;

namespace Ripple.Cli;

public static class RecordCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        string? outPath = null;
        bool? summarize = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--output" when i + 1 < args.Length:
                    outPath = args[++i];
                    break;
                case "--no-summary":
                    summarize = false;
                    break;
                case "--summary":
                    summarize = true;
                    break;
                default:
                    CliHelper.Error($"Неизвестный ключ «{args[i]}». Доступно: --output <файл>, --summary, --no-summary.");
                    return 1;
            }
        }

        var capture = new WasapiLoopbackCapture();
        var wavPath = outPath ?? Path.Combine(
            Storage.AudioDir, $"recording-{DateTime.Now:yyyyMMdd-HHmmss}.wav");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(wavPath))!);

        var writer = new WaveFileWriter(wavPath, capture.WaveFormat);
        var written = 0L;
        var startedAt = DateTime.Now;

        capture.DataAvailable += (_, e) =>
        {
            writer.Write(e.Buffer, 0, e.BytesRecorded);
            written += e.BytesRecorded;
            CliHelper.Progress($"Запись {TimeSpan.FromSeconds(written / (capture.WaveFormat.AverageBytesPerSecond)):hh\\:mm\\:ss}…");
        };
        capture.RecordingStopped += (_, _) => writer.Dispose();

        capture.StartRecording();
        CliHelper.ProgressDone();
        CliHelper.Info($"Запись идёт: {wavPath}");
        CliHelper.Info("Ctrl+C — остановить и обработать запись.");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            await Task.Delay(Timeout.Infinite, cts.Token);
        }

        catch (OperationCanceledException) { }

        Console.CancelKeyPress -= null;
        capture.StopRecording();
        capture.Dispose();
        CliHelper.ProgressDone();

        var seconds = (DateTime.Now - startedAt).TotalSeconds;
        
        if (seconds < 1 || !File.Exists(wavPath))
        {
            CliHelper.Error("Запись пуста, файл не создан.");

            return 1;
        }

        CliHelper.Ok($"Записано {TimeSpan.FromSeconds(seconds):hh\\:mm\\:ss} → {wavPath}");

        var lecture = new Lecture
        {
            Title = $"Запись {DateTime.Now:dd.MM.yyyy HH:mm}",
            Source = SourceKind.Recording,
            SourcePath = wavPath,
            Status = LectureStatus.Draft
        };
        AppServices.Library.Upsert(lecture);
        AppServices.NotifyLibraryChanged();
        CliHelper.Info($"Лекция: {lecture.Id[..8]}");

        await PipelineHelper.RunAsync(lecture, summarize, CancellationToken.None);

        return ImportCommand.ReportResult(lecture);
    }
}
