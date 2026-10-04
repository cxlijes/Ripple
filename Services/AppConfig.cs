using System.Text.Json.Serialization;

namespace Ripple.Services;

public class AppConfig
{
    public string WhisperModel { get; set; } =
        Environment.GetEnvironmentVariable("RIPPLE_WHISPER_MODEL") ?? "large-v3";

    public string PythonExecutable { get; set; } = "python";
    public string FfmpegPath { get; set; } = "ffmpeg";
    public string YtDlpPath { get; set; } = "yt-dlp";
    public string LlamaServerPath { get; set; } = "llama-server";

    public string LlamaModelPath { get; set; } = "";

    public string LlamaModelName { get; set; } = "gpt-oss-20b";

    public string LlamaHost { get; set; } = "127.0.0.1";
    public int LlamaPort { get; set; } = 8080;

    public bool AutoSummarize { get; set; } = true;

    public string DataRoot { get; set; } = @"e:\dev\ripple\storage";

    [JsonIgnore]
    public string LlamaBaseUrl => $"http://{LlamaHost}:{LlamaPort}";
}
