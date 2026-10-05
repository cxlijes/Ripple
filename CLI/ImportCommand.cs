using Ripple.Services;

namespace Ripple.Cli;

public static class ImportCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            CliHelper.Error("Укажите файл или URL: ripple import <файл|url>");
            return 1;
        }

        var source = args[0];
        var title = DefaultTitle(source);
        bool? summarize = null;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--title" when i + 1 < args.Length:
                    title = args[++i];
                    break;
                case "--no-summary":
                    summarize = false;
                    break;
                case "--summary":
                    summarize = true;
                    break;
                default:
                    CliHelper.Error($"Неизвестный ключ «{args[i]}». Доступно: --title <текст>, --summary, --no-summary.");
                    return 1;
            }
        }

        if (!TryResolveSource(source, out var kind, out var resolved))
            return 1;

        var lecture = new Lecture
        {
            Title = title,
            Source = kind,
            SourcePath = resolved,
            Status = LectureStatus.Draft
        };

        AppServices.Library.Upsert(lecture);
        AppServices.NotifyLibraryChanged();

        CliHelper.Info($"Импорт «{title}» ({kind.ToString().ToLowerInvariant()}), id = {lecture.Id[..8]}");
        CliHelper.Info("Нажмите Ctrl+C для отмены.");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            CliHelper.Info("");
            CliHelper.Info("Отмена...");
            cts.Cancel();
        };

        try
        {
            await PipelineHelper.RunAsync(lecture, summarize, cts.Token);
        }
        finally
        {
            Console.CancelKeyPress -= null;
            cts.Dispose();
        }

        return ReportResult(lecture);
    }

    private static bool TryResolveSource(string source, out SourceKind kind, out string resolved)
    {
        kind = SourceKind.File;
        resolved = source;

        if (source.Contains("://") && Uri.TryCreate(source, UriKind.Absolute, out _))
        {
            kind = SourceKind.Url;
            return true;
        }

        var full = Path.GetFullPath(source);
        if (File.Exists(full))
        {
            resolved = full;
            return true;
        }

        if (Directory.Exists(full))
        {
            var files = Directory.GetFiles(full).Where(CliHelper.IsAudioFile).ToList();
            if (files.Count == 0)
            {
                CliHelper.Error($"В папке {full} нет аудио- или видеофайлов.");
                return false;
            }
            resolved = files.OrderBy(f => f).First();
            CliHelper.Info($"Папка: берём {Path.GetFileName(resolved)}");
            return true;
        }

        CliHelper.Error($"Файл не найден: {source}");
        return false;
    }

    internal static string DefaultTitle(string source) =>
        source.Contains("://") && Uri.TryCreate(source, UriKind.Absolute, out var uri)
            ? $"Лекция · {uri.Host}"
            : Path.GetFileNameWithoutExtension(source);

    internal static int ReportResult(Lecture lecture)
    {
        CliHelper.ProgressDone();
        if (lecture.Status != LectureStatus.Ready)
        {
            CliHelper.Error($"Не удалось: {lecture.Error}");
            CliHelper.Info($"Лекция сохранена со статусом «{lecture.StatusText}» — её можно перезапустить: ripple retry {lecture.Id[..8]}");
            return 1;
        }

        CliHelper.Ok($"Готово: {lecture.Segments.Count} сегментов, {lecture.DurationText}");
        CliHelper.Info($"  Конспект: {(string.IsNullOrWhiteSpace(lecture.Summary) ? "не создан" : $"{lecture.Summary.Length} символов")}");
        CliHelper.Info($"  Посмотреть: ripple show {lecture.Id[..8]}");
        return 0;
    }
}
