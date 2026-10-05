using Ripple.Services;

namespace Ripple.Cli;

public static class PipelineHelper
{
    public static async Task RunAsync(Lecture lecture, bool? summarize, CancellationToken ct)
    {
        var media = AppServices.Media;
        media.Progress = new Progress<double>(p =>
            CliHelper.Progress($"[{p,7:P0}] Скачивание..."));

        if (lecture.Source == SourceKind.File || lecture.Source == SourceKind.Recording)
        {
            CliHelper.Progress("Конвертация в WAV...");
            lecture.SourcePath = await media.ToWavAsync(lecture.SourcePath, ct);
        }

        CliHelper.Progress("Определение длительности...");
        lecture.DurationSeconds = await media.GetDurationAsync(lecture.SourcePath, ct);
        AppServices.Library.Upsert(lecture);

        media.Progress = null;
        CliHelper.ProgressDone();

        await AppServices.Pipeline.RunAsync(lecture, summarize, l =>
        {
            if (l.Status == LectureStatus.Transcribing)
                CliHelper.Progress($"[{l.Progress,7:P0}] Транскрибация...");
            else if (l.Status == LectureStatus.Summarizing)
                CliHelper.Progress("Конспект (gpt-oss-20b)...");
        }, ct);

        media.Progress = null;
    }

    public static IReadOnlyList<string> AudioExtensions { get; } = new[]
    {
        ".mp3", ".wav", ".m4a", ".aac", ".flac", ".ogg",
        ".mp4", ".mkv", ".avi", ".webm", ".mov"
    };
}
