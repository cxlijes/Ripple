namespace Ripple.Services;

public class DiagnosticsService : IHealthProbe
{
    private readonly ITranscriptionService _transcriber;
    private readonly ISummarizationService _summarizer;

    public DiagnosticsService(ITranscriptionService transcriber, ISummarizationService summarizer)
    {
        _transcriber = transcriber;
        _summarizer = summarizer;
    }

    public async Task<EnvironmentStatus> ProbeAsync()
    {
        var whisperTask = _transcriber.IsAvailableAsync();
        var llmTask = _summarizer.IsServerAliveAsync();
        await Task.WhenAll(whisperTask, llmTask);

        return new EnvironmentStatus(whisperTask.Result, llmTask.Result);
    }
}
