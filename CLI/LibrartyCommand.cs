using Ripple.Services;

namespace Ripple.Cli;

public static class LibraryCommand
{
    public static Task<int> ListAsync(string[] args)
    {
        var all = AppServices.Library.LoadAll()
            .OrderByDescending(l => l.CreatedAt)
            .ToList();

        if (all.Count == 0)
        {
            CliHelper.Info("Библиотека пуста. Импортируйте: ripple import <файл|url>");

            return Task.FromResult(0);
        }

        CliHelper.Info($"{"ID",-10} {"Статус",-15} {"Длит.",-10} Дата              Название");
        CliHelper.Info(new string('-', 72));

        foreach (var l in all)
        {
            CliHelper.Info(
                $"{l.Id[..8],-10} {l.StatusText,-15} {l.DurationText,-10} " +
                $"{l.CreatedAt:dd.MM.yyyy HH:mm,-18}{l.Title}");
        }

        return Task.FromResult(0);
    }

    public static Task<int> ShowAsync(string[] args)
    {
        if (args.Length == 0)
        {
            CliHelper.Error("Нужно: ripple show <id>"); 
            
            return Task.FromResult(1);
        }

        var lecture = CliHelper.FindLecture(args[0]);
        if (lecture is null)
        {
            return Task.FromResult(1);
        }

        var summaryOnly = args.Contains("--summary-only");
        var transcriptOnly = args.Contains("--transcript-only");

        CliHelper.Info($"# {lecture.Title}");
        CliHelper.Info($"{lecture.CreatedAt:dd.MM.yyyy HH:mm} · {lecture.StatusText} · {lecture.DurationText}");
        if (!string.IsNullOrWhiteSpace(lecture.Error))
        {
            CliHelper.Error($"Ошибка: {lecture.Error}");
        }

        if (!transcriptOnly)
        {
            CliHelper.Info("");
            CliHelper.Info("## Конспект");
            CliHelper.Info(string.IsNullOrWhiteSpace(lecture.Summary)
                ? "(конспект ещё не создан — ripple retry <id> --summary)"
                : lecture.Summary);
        }

        if (!summaryOnly)
        {
            CliHelper.Info("");
            CliHelper.Info("## Транскрипт");
            if (lecture.Segments.Count == 0)
            {
                CliHelper.Info("(транскрипт ещё не создан)");
            }

            else
            {
                foreach (var s in lecture.Segments)
                {
                    CliHelper.Info($"[{s.Timestamp}] {s.Text.Trim()}");
                }
            }
        }

        return Task.FromResult(0);
    }

    public static Task<int> DeleteAsync(string[] args)
    {
        if (args.Length == 0)
        {
            CliHelper.Error("Нужно: ripple delete <id>");
            return Task.FromResult(1);
        }
        var lecture = CliHelper.FindLecture(args[0]);
        if (lecture is null)
        {
            return Task.FromResult(1);
        }

        if (AppServices.Library.Remove(lecture.Id))
        {
            CliHelper.Ok($"Удалено: {lecture.Title}");
            AppServices.NotifyLibraryChanged();

            return Task.FromResult(0);
        }

        CliHelper.Error("Лекция не найдена в хранилище.");

        return Task.FromResult(1);
    }

    public static async Task<int> RetryAsync(string[] args)
    {
        if (args.Length == 0)
        {
            CliHelper.Error("Нужно: ripple retry <id>");
            
            return 1;
        }
        var lecture = CliHelper.FindLecture(args[0]);
        if (lecture is null)
        {
            return 1;
        }

        if (lecture.Status == LectureStatus.Ready)
        {
            CliHelper.Info("Лекция уже готова. Чтобы пересоздать конспект, удалите и импортируйте заново.");

            return 0;
        }

        bool? summarize = args.Contains("--no-summary") ? false
            : args.Contains("--summary") ? true : null;

        if (!File.Exists(lecture.SourcePath) && lecture.Source == SourceKind.File)
        {
            CliHelper.Error($"Исходный файл не найден: {lecture.SourcePath}. Импортируйте заново.");

            return 1;
        }

        CliHelper.Info($"Повтор для «{lecture.Title}» (статус был «{lecture.StatusText}»).");
        await PipelineHelper.RunAsync(lecture, summarize, CancellationToken.None);

        return ImportCommand.ReportResult(lecture);
    }
}
