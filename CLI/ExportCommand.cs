using Ripple.Services;

namespace Ripple.Cli;

public static class ExportCommand
{
    public static Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            CliHelper.Error("Нужно: ripple export <id>");
            return Task.FromResult(1);
        }

        var lecture = CliHelper.FindLecture(args[0]);
        if (lecture is null)
        {
            return Task.FromResult(1);
        }

        var format = "md";
        string? output = null;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--format" when i + 1 < args.Length:
                    format = args[++i].ToLowerInvariant();
                    break;
                case "--output" when i + 1 < args.Length:
                    output = args[++i];
                    break;
                default:
                    CliHelper.Error($"Неизвестный ключ «{args[i]}». Доступно: --format md|docx, --output <файл>");
                    return Task.FromResult(1);
            }
        }

        if (format is not ("md" or "markdown" or "docx"))
        {
            CliHelper.Error($"Формат «{format}» не поддерживается. Доступно: md, docx.");

            return Task.FromResult(1);
        }

        var isDocx = format == "docx";
        var path = output ?? DefaultPath(lecture, isDocx);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        try
        {
            if (isDocx)
            {
                AppServices.Exporter.ExportDocx(lecture, path);
            }

            else
            {
                AppServices.Exporter.ExportMarkdown(lecture, path);
            }

            CliHelper.Ok($"Экспортировано: {path}");

            return Task.FromResult(0);
        }

        catch (Exception ex)
        {
            CliHelper.Error($"Не удалось сохранить: {ex.Message}");

            return Task.FromResult(1);
        }
    }

    private static string DefaultPath(Lecture lecture, bool isDocx)
    {
        var bad = Path.GetInvalidFileNameChars();
        var safe = new string(lecture.Title.Select(c => bad.Contains(c) ? '_' : c).ToArray());

        return Path.Combine("exports", $"{safe}-{lecture.Id[..8]}{(isDocx ? ".docx" : ".md")}");
    }
}
