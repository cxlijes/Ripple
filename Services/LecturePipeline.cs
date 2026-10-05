namespace Ripple.Services;

public class LecturePipeline
{
    private readonly IMediaService _media;
    private readonly ITranscriptionService _transcriber;
    private readonly ISummarizationService _summarizer;
    private readonly ILibraryStore _library;
    private readonly AppConfig _config;

    public LecturePipeline(
        IMediaService media,
        ITranscriptionService transcriber,
        ISummarizationService summarizer,
        ILibraryStore library,
        AppConfig config)
    {
        _media = media;
        _transcriber = transcriber;
        _summarizer = summarizer;
        _library = library;
        _config = config;
    }

    public async Task RunAsync(
        Lecture lecture,
        bool? summarize = null,
        Action<Lecture>? onChanged = null,
        CancellationToken ct = default)
    {
        void Update()
        {
            onChanged?.Invoke(lecture);
            AppServices.NotifyLibraryChanged();
        }

        var started = DateTimeOffset.Now;

        try
        {
            var audioPath = lecture.SourcePath;

            if (lecture.Source == SourceKind.Url)
            {
                lecture.Status = LectureStatus.Downloading;
                Update();
                audioPath = await _media.DownloadUrlAsync(lecture.SourcePath, ct);
                lecture.SourcePath = audioPath;
                lecture.Source = SourceKind.File;
                _library.Upsert(lecture);
            }

            lecture.Status = LectureStatus.Transcribing;
            lecture.Progress = 0;
            Update();

            var progress = new Progress<double>(p =>
            {
                lecture.Progress = p;
                Update();
            });

            lecture.Segments = await _transcriber.TranscribeAsync(audioPath, progress, ct);
            TempCleanup.TryDeleteTempFile(audioPath);

            if (lecture.DurationSeconds <= 0)
            {
                lecture.DurationSeconds = lecture.Segments.LastOrDefault()?.End ?? 0;
            }

            var wantSummary = summarize ?? _config.AutoSummarize;
            if (wantSummary && lecture.Segments.Count > 0)
            {
                lecture.Status = LectureStatus.Summarizing;
                lecture.Progress = 1;
                Update();
                _library.Upsert(lecture);
                lecture.Summary = await _summarizer.SummarizeAsync(lecture, ct);
            }

            lecture.Status = LectureStatus.Ready;
            lecture.Error = null;
            lecture.Progress = 0;
            AppServices.Log($"Pipeline OK: {lecture.Title} ({lecture.Segments.Count} сегментов)");
        }

        catch (OperationCanceledException)
        {
            lecture.Status = LectureStatus.Failed;
            lecture.Error = "Отменено";
            AppServices.Log($"Pipeline CANCELLED: {lecture.Title} ({(DateTimeOffset.Now - started).TotalSeconds:0}s)");
        }

        catch (Exception ex)
        {
            lecture.Status = LectureStatus.Failed;
            lecture.Error = ex.Message;
            AppServices.Log($"Pipeline FAIL: {lecture.Title}: {ex}");
        }

        Update();
        _library.Upsert(lecture);
    }
}
