namespace Ripple.Services;

public interface IConfigStore
{
    AppConfig Load();

    void Save(AppConfig config);
}

public interface ILibraryStore
{
    IReadOnlyList<Lecture> LoadAll();

    void SaveAll(IEnumerable<Lecture> lectures);

    void Upsert(Lecture lecture);

    bool Remove(string id);
}

public interface IProcessRunner
{
    Task<int> RunAsync(
        string exe,
        string args,
        TimeSpan timeout,
        Action<string?>? onLine = null,
        Action<string?>? onErrLine = null,
        CancellationToken ct = default);
}

public interface IMediaService
{
    IProgress<double>? Progress { get; set; }

    Task<string> ToWavAsync(string inputPath, CancellationToken ct = default);

    Task<double> GetDurationAsync(string mediaPath, CancellationToken ct = default);

    Task<string> DownloadUrlAsync(string url, CancellationToken ct = default);
}

public interface ITranscriptionService
{
    Task<bool> IsAvailableAsync();

    Task<List<TranscriptSegment>> TranscribeAsync(
        string audioPath,
        IProgress<double>? onProgress = null,
        CancellationToken ct = default);
}

public interface ISummarizationService
{
    Task<bool> IsServerAliveAsync();

    Task EnsureServerAsync(CancellationToken ct = default);

    Task<string> SummarizeAsync(Lecture lecture, CancellationToken ct = default);
}

public sealed record EnvironmentStatus(bool WhisperOk, bool LlmOk);

public interface IHealthProbe
{
    Task<EnvironmentStatus> ProbeAsync();
}

public interface IDocumentExporter
{
    string BuildMarkdown(Lecture lecture, bool includeTranscript = true);

    void ExportMarkdown(Lecture lecture, string path, bool includeTranscript = true);

    void ExportDocx(Lecture lecture, string path, bool includeTranscript = true);
}

public interface IRecordingService : IDisposable
{
    bool IsRecording { get; }

    string StartSystemAudio();

    string StopSystemAudio();
}
