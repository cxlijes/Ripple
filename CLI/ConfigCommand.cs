using System.Globalization;
using Ripple.Services;

namespace Ripple.Cli;

public static class ConfigCommand
{
    private static readonly (string Key, string Description)[] Keys =
    {
        ("whisper-model",  "модель Whisper (large-v3, medium, base, tiny)"),
        ("python",         "путь к python.exe"),
        ("ffmpeg",         "путь к ffmpeg.exe"),
        ("ytdlp",          "путь к yt-dlp.exe"),
        ("llama-server",   "путь к llama-server.exe"),
        ("llama-model",    "путь к GGUF-модели"),
        ("llama-host",     "адрес llama-server"),
        ("llama-port",     "порт llama-server"),
        ("auto-summarize", "автоконспект после транскрибации (true/false)"),
        ("data-root",      "корень тяжёлых данных (пусто = %APPDATA%\\Ripple)"),
    };

    public static Task<int> RunAsync(string[] args)
    {
        var action = args.Length > 0 ? args[0].ToLowerInvariant() : "show";
        switch (action)
        {
            case "show": return Task.FromResult(Show());
            case "get": return Task.FromResult(Get(args));
            case "set": return Task.FromResult(Set(args));
            default:
                CliHelper.Error($"Неизвестное действие «{action}». Доступно: show, get, set <ключ> <значение>.");
                return Task.FromResult(1);
        }
    }

    private static int Show()
    {
        foreach (var (key, _) in Keys)
        {
            Console.WriteLine($"{key}={Value(key)}");
        }

        return 0;
    }

    private static int Get(string[] args)
    {
        if (args.Length < 2) { CliHelper.Error("Нужно: ripple config get <ключ>"); return 1; }

        var key = args[1].ToLowerInvariant();
        if (Keys.All(k => k.Key != key))
        {
            CliHelper.Error($"Неизвестный ключ «{key}».");

            return 1;
        }

        Console.WriteLine(Value(key));

        return 0;
    }

    private static int Set(string[] args)
    {
        if (args.Length < 3)
        {
            CliHelper.Error("Нужно: ripple config set <ключ> <значение>");
            CliHelper.Info("Ключи: " + string.Join(", ", Keys.Select(k => k.Key)));

            return 1;
        }

        var key = args[1].ToLowerInvariant();
        var value = args[2];
        var c = AppServices.Config;

        switch (key)
        {
            case "whisper-model": c.WhisperModel = value; break;
            case "python": c.PythonExecutable = value; break;
            case "ffmpeg": c.FfmpegPath = value; break;
            case "ytdlp": c.YtDlpPath = value; break;
            case "llama-server": c.LlamaServerPath = value; break;
            case "llama-model": c.LlamaModelPath = value; break;
            case "llama-host": c.LlamaHost = value; break;
            case "llama-port":
                if (!int.TryParse(value, out var port) || port is < 1 or > 65535)
                {
                    CliHelper.Error($"Порт должен быть числом 1..65535, получено «{value}».");
                    return 1;
                }
                c.LlamaPort = port;
                break;
            case "auto-summarize":
                if (!bool.TryParse(value, out var auto))
                {
                    CliHelper.Error("Ожидается true или false.");
                    return 1;
                }
                c.AutoSummarize = auto;
                break;
            case "data-root":
                c.DataRoot = value;
                Storage.Root = string.IsNullOrWhiteSpace(value) ? Storage.DataDir : value;
                break;
            default:
                CliHelper.Error($"Неизвестный ключ «{key}».");
                CliHelper.Info("Ключи: " + string.Join(", ", Keys.Select(k => k.Key)));
                return 1;
        }

        AppServices.SaveConfig();
        CliHelper.Ok($"{key} = {Value(key)}");

        return 0;
    }

    private static string Value(string key)
    {
        var c = AppServices.Config;
        return key switch
        {
            "whisper-model" => c.WhisperModel,
            "python" => c.PythonExecutable,
            "ffmpeg" => c.FfmpegPath,
            "ytdlp" => c.YtDlpPath,
            "llama-server" => c.LlamaServerPath,
            "llama-model" => c.LlamaModelPath,
            "llama-host" => c.LlamaHost,
            "llama-port" => c.LlamaPort.ToString(CultureInfo.InvariantCulture),
            "auto-summarize" => c.AutoSummarize ? "true" : "false",
            "data-root" => string.IsNullOrWhiteSpace(c.DataRoot) ? "" : c.DataRoot,
            _ => "?",
        };
    }
}
