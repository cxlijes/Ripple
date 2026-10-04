namespace Ripple.Models;

public enum LectureStatus
{
    Draft,
    Downloading,
    Transcribing,
    Summarizing,
    Ready,
    Failed
}

public enum SourceKind
{
    File,
    Url,
    Recording
}

public class TranscriptSegment
{
    public double Start { get; set; }
    public double End { get; set; }
    public string Text { get; set; } = "";

    public string Timestamp => TimeSpan.FromSeconds(Start).ToString(@"hh\:mm\:ss");
}

public class Lecture
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Без названия";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public SourceKind Source { get; set; }
    public string SourcePath { get; set; } = "";
    public double DurationSeconds { get; set; }
    public LectureStatus Status { get; set; } = LectureStatus.Draft;
    public string? Error { get; set; }
    public double Progress { get; set; }

    public List<TranscriptSegment> Segments { get; set; } = new();

    public string? Summary { get; set; }

    public string DurationText => TimeSpan.FromSeconds(DurationSeconds).ToString(@"hh\:mm\:ss");

    public string StatusText => Status switch
    {
        LectureStatus.Downloading => "Загрузка…",
        LectureStatus.Transcribing => "Транскрибация…",
        LectureStatus.Summarizing => "Конспектирование…",
        LectureStatus.Ready => "Готово",
        LectureStatus.Failed => "Ошибка",
        _ => "Черновик"
    };

    public string FullText => string.Join("\n", Segments.Select(s => s.Text));
}
