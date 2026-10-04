using NAudio.Wave;

namespace Ripple.Services;

public class RecordingService : IRecordingService
{
    private WasapiLoopbackCapture? capture;
    private WaveFileWriter? writer;
    private string? wavPath;
    private readonly object _lock = new();

    public bool IsRecording { get; private set; }

    public string StartSystemAudio()
    {
        if (IsRecording) throw new InvalidOperationException("Запись уже идёт.");

        wavPath = Path.Combine(Storage.AudioDir,
            $"recording-{DateTime.Now:yyyyMMdd-HHmmss}.wav");

        capture = new WasapiLoopbackCapture();
        writer = new WaveFileWriter(wavPath, capture.WaveFormat);
        capture.DataAvailable += (_, e) =>
        {
            lock (_lock)
            {
                writer?.Write(e.Buffer, 0, e.BytesRecorded);
                writer?.Flush();
            }
        };
        capture.RecordingStopped += (_, _) =>
        {
            lock (_lock)
            {
                writer?.Dispose();
                writer = null;
            }
        };

        capture.StartRecording();
        IsRecording = true;

        return wavPath;
    }

    public string StopSystemAudio()
    {
        if (!IsRecording || capture == null || wavPath == null)
            throw new InvalidOperationException("Запись не идёт.");

        capture.StopRecording();
        capture.Dispose();
        capture = null;
        IsRecording = false;

        lock (_lock)
        {
            writer?.Dispose();
            writer = null;
        }
        return wavPath;
    }

    public void Dispose()
    {
        if (!IsRecording)
        {
            return;
        }

        try
        {
            StopSystemAudio();
        }

        catch { }
    }
}
