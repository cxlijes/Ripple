namespace Ripple.Services;

public static class AppServices
{
    private static readonly object _logLock = new();

    static AppServices()
    {
        Configs = new JsonConfigStore();
        Config = Configs.Load();
        Library = new JsonLibraryStore();
        Runner = new ProcessRunner();
        Media = new MediaService(Config, Runner);
        Transcriber = new WhisperService(Config, Runner);
        Summarizer = new LlmService(Config);
        Health = new DiagnosticsService(Transcriber, Summarizer);
        Exporter = new ExportService();

        Storage.Root = string.IsNullOrWhiteSpace(Config.DataRoot) ? Storage.DataDir : Config.DataRoot;
        Pipeline = new LecturePipeline(Media, Transcriber, Summarizer, Library, Config);
        TempCleanup.Run();
    }

    public static AppConfig Config { get; }
    public static IConfigStore Configs { get; }
    public static ILibraryStore Library { get; }
    public static IProcessRunner Runner { get; }
    public static IMediaService Media { get; }
    public static ITranscriptionService Transcriber { get; }
    public static ISummarizationService Summarizer { get; }
    public static IHealthProbe Health { get; }
    public static IDocumentExporter Exporter { get; }

    public static LecturePipeline Pipeline { get; private set; } = null!;

    public static IRecordingService CreateRecording() => new RecordingService();

    private static ModuleService? _modules;

    public static ModuleService Modules => _modules ??= new(Config, Runner, Configs, Transcriber);

    public static ModuleService CreateModuleService() => Modules;

    public static event Action? LibraryChanged;

    public static void NotifyLibraryChanged() => LibraryChanged?.Invoke();

    public static void Log(string message)
    {
        try
        {
            lock (_logLock)
            {
                File.AppendAllText(Path.Combine(Storage.DataDir, "ripple.log"),
                    $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
            }
        }
        catch { }
    }

    public static void SaveConfig() => Configs.Save(Config);

    public static Task<bool> CheckWhisperAsync() => Transcriber.IsAvailableAsync();

    public static Task<bool> CheckLlmAsync() => Summarizer.IsServerAliveAsync();

}
