using Ripple.Services;

namespace Ripple.Cli;

public static class CliHelper
{
    public static void Info(string message) => Console.WriteLine(message);

    public static void Error(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void Ok(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void Progress(string message)
    {
        Console.Write($"\r{message,-80}");
    }

    public static void ProgressDone()
    {
        Console.WriteLine();
    }

    public static bool IsAudioFile(string path) =>
        PipelineHelper.AudioExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    public static Lecture? FindLecture(string idPrefix)
    {
        var all = AppServices.Library.LoadAll();
        var matches = all.Where(l => l.Id.StartsWith(idPrefix, StringComparison.OrdinalIgnoreCase)).ToList();

        if (matches.Count == 0)
        {
            Error($"Лекция с ID, начинающимся на «{idPrefix}», не найдена.");

            return null;
        }

        if (matches.Count > 1)
        {
            Error($"ID «{idPrefix}» неоднозначен ({matches.Count} совпадений). Уточните.");

            return null;
        }

        return matches[0];
    }

    public static async Task<int> StatusAsync()
    {
        Info("Проверка окружения...");

        var whisperOk = await AppServices.CheckWhisperAsync();
        Info(whisperOk ? "  whisper:   доступен" : "  whisper:   НЕ установлен (pip install openai-whisper)");

        var llmOk = await AppServices.CheckLlmAsync();
        Info(llmOk ? "  llama-server: отвечает" : "  llama-server: не запущен");

        Info($"  Данные:     {Storage.Root}");
        Info($"  Веса whisper: {ModuleService.Format(Storage.GetSize(Storage.WhisperDir))}");
        Info($"  Модули:     {ModuleService.Format(Storage.GetSize(Storage.ModulesDir))}");
        Info($"  Аудио:      {ModuleService.Format(Storage.GetSize(Storage.AudioDir))}");

        var count = AppServices.Library.LoadAll().Count;
        Info($"  Лекций в библиотеке: {count}");

        return whisperOk ? 0 : 1;
    }

    public static void PrintHelp()
    {
        Info("""
            ripple — консольные конспекты лекций

            Использование: ripple <команда> [аргументы]

            Лекции
              import <файл|папка|url> [--title <текст>] [--summary|--no-summary]
              retry  <id> [--summary|--no-summary]
              list
              show   <id> [--summary-only|--transcript-only]
              delete <id>
              export <id> [--format md|docx] [--output <файл>]
              record [--output <файл>] [--no-summary]

            Окружение
              status
              modules [list|install <id>|remove <id>]
              config [show|set <ключ> <значение>]
              help

            Модули: ffmpeg, ytdlp, whisper, whisper-model, llamacpp, gguf
            Ключи config: whisper-model, python, ffmpeg, ytdlp, llama-server,
                          llama-model, llama-host, llama-port, auto-summarize, data-root

            Лекции опознаются по короткому ID (первые 8 символов).
            """);
    }
}
