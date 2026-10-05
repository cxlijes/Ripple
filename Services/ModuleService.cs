using System.Diagnostics;
using System.IO.Compression;

namespace Ripple.Services;

public enum ModuleState
{
    Missing,
    Ready,
    Installing,
    Failed
}

public sealed class ModuleInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Purpose { get; init; }
    public required long ApproxBytes { get; init; }

    public ModuleState State { get; set; } = ModuleState.Missing;
    public string? InstalledPath { get; set; }
    public string StatusDetail { get; set; } = "";

    public double? Progress { get; set; }

    public long DownloadedBytes { get; set; }
    public long? TotalBytes { get; set; }

    public bool CanRemove =>
        State == ModuleState.Ready
        && !string.IsNullOrEmpty(InstalledPath)
        && InstalledPath.StartsWith(Storage.ModulesDir, StringComparison.OrdinalIgnoreCase);

    public string SizeText =>
        TotalBytes is > 0 && State == ModuleState.Installing
            ? $"{ModuleService.Format(DownloadedBytes)} / {ModuleService.Format(TotalBytes.Value)}"
            : ModuleService.Format(ApproxBytes);
}

public sealed class ModuleService
{
    private readonly AppConfig _config;
    private readonly IProcessRunner _runner;
    private readonly IConfigStore _configStore;
    private readonly ITranscriptionService _transcriber;
    private static readonly HttpClient _http = new() { Timeout = Timeout.InfiniteTimeSpan };

    public event Action? Changed;

    private readonly Dictionary<string, ModuleInfo> _modules = new();
    private readonly Dictionary<string, CancellationTokenSource> _cancellations = new();

    public ModuleService(
        AppConfig config,
        IProcessRunner runner,
        IConfigStore configStore,
        ITranscriptionService transcriber)
    {
        _config = config;
        _runner = runner;
        _configStore = configStore;
        _transcriber = transcriber;
        BuildCatalog();
    }

    public IReadOnlyList<ModuleInfo> Modules => _modules.Values.ToList();

    private void Notify() => Changed?.Invoke();

    private void Set(ModuleInfo m, ModuleState state, string detail = "", double? progress = null)
    {
        m.State = state;
        m.StatusDetail = detail;
        m.Progress = progress;
        Notify();
    }

    private void BuildCatalog()
    {
        void Add(ModuleInfo m) => _modules[m.Id] = m;

        Add(new ModuleInfo
        {
            Id = "ffmpeg",
            Name = "FFmpeg",
            Purpose = "Конвертация аудио и длительность. Нужен для импорта и записи.",
            ApproxBytes = 130L * 1024 * 1024
        });
        Add(new ModuleInfo
        {
            Id = "ytdlp",
            Name = "yt-dlp",
            Purpose = "Скачивание аудио по ссылке (YouTube, облако).",
            ApproxBytes = 3L * 1024 * 1024
        });
        Add(new ModuleInfo
        {
            Id = "whisper",
            Name = "openai-whisper",
            Purpose = "Движок транскрибации. Ставится в ваш Python через pip.",
            ApproxBytes = 0
        });
        Add(new ModuleInfo
        {
            Id = "whisper-model",
            Name = "Модель Whisper",
            Purpose = "Веса распознавания речи. large-v3 — лучшее качество, ~3 ГБ.",
            ApproxBytes = 3L * 1024 * 1024 * 1024
        });
        Add(new ModuleInfo
        {
            Id = "llamacpp",
            Name = "llama.cpp",
            Purpose = "Локальный сервер для генерации конспектов.",
            ApproxBytes = 40L * 1024 * 1024
        });
        Add(new ModuleInfo
        {
            Id = "gguf",
            Name = "Модель gpt-oss-20b",
            Purpose = "GGUF-веса для конспектов. MXFP4 — баланс качества и размера, ~12 ГБ.",
            ApproxBytes = 12L * 1024 * 1024 * 1024
        });
    }

    public async Task RefreshAsync()
    {
        foreach (var m in _modules.Values.Where(m => m.State != ModuleState.Installing))
            m.State = ModuleState.Missing;
        Notify();

        ProbeBinary("ffmpeg", "ffmpeg.exe", _config.FfmpegPath, SetFfmpegPath);
        ProbeBinary("ytdlp", "yt-dlp.exe", _config.YtDlpPath, SetYtDlpPath);
        ProbeBinary("llamacpp", "llama-server.exe", _config.LlamaServerPath, SetLlamaPath);

        await ProbeWhisperAsync();
        ProbeWhisperModel();
        ProbeGguf();

        Notify();
    }

    private void ProbeBinary(string id, string fileName, string configuredPath, Action<string> apply)
    {
        var m = _modules[id];

        foreach (var candidate in new[]
                 {
                     Path.Combine(Storage.ModulesDir, fileName), configuredPath, fileName
                 })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            var resolved = ResolveExisting(candidate);
            if (resolved == null) continue;

            m.State = ModuleState.Ready;
            m.InstalledPath = resolved;
            m.StatusDetail = ShortPath(resolved);
            m.Progress = null;

            if (!string.Equals(configuredPath, resolved, StringComparison.OrdinalIgnoreCase))
                apply(resolved);
            return;
        }

        m.State = ModuleState.Missing;
        m.InstalledPath = null;
        m.StatusDetail = "";
        m.Progress = null;
    }

    private static string? ResolveExisting(string pathOrName)
    {
        try
        {
            if (Path.IsPathRooted(pathOrName))
                return File.Exists(pathOrName) ? pathOrName : null;

            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "")
                         .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var full = Path.Combine(dir.Trim(), pathOrName);
                if (File.Exists(full)) return full;
            }
        }
        catch { }

        return null;
    }

    private async Task ProbeWhisperAsync()
    {
        var m = _modules["whisper"];
        try
        {
            var ok = await _transcriber.IsAvailableAsync();
            m.State = ok ? ModuleState.Ready : ModuleState.Missing;
            m.StatusDetail = ok
                ? $"установлен ({_config.PythonExecutable})"
                : $"не найден в «{_config.PythonExecutable}»";
        }
        catch (Exception ex)
        {
            m.State = ModuleState.Missing;
            m.StatusDetail = "проверка не удалась: " + ex.Message;
        }

        m.Progress = null;
    }

    private void ProbeWhisperModel()
    {
        var m = _modules["whisper-model"];
        var expected = WhisperService.ModelFileName(_config.WhisperModel);

        if (File.Exists(expected))
        {
            m.State = ModuleState.Ready;
            m.InstalledPath = expected;
            m.StatusDetail = Path.GetFileName(expected);
        }
        else
        {
            m.State = ModuleState.Missing;
            m.InstalledPath = null;
            m.StatusDetail = $"модель «{_config.WhisperModel}» ещё не скачана";
        }

        m.Progress = null;
    }

    private void ProbeGguf()
    {
        var m = _modules["gguf"];
        var configured = _config.LlamaModelPath;

        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            m.State = ModuleState.Ready;
            m.InstalledPath = configured;
            m.StatusDetail = Path.GetFileName(configured);
        }
        else
        {
            var guess = Directory.Exists(Storage.ModulesDir)
                ? Directory.GetFiles(Storage.ModulesDir, "*.gguf").FirstOrDefault()
                : null;

            if (guess != null)
            {
                m.State = ModuleState.Ready;
                m.InstalledPath = guess;
                m.StatusDetail = Path.GetFileName(guess);
                SetLlamaModelPath(guess);
            }
            else
            {
                m.State = ModuleState.Missing;
                m.InstalledPath = null;
                m.StatusDetail = "GGUF-файл не найден";
            }
        }

        m.Progress = null;
    }

    public bool Install(string id)
    {
        if (!_modules.TryGetValue(id, out var m)) return false;
        if (_cancellations.ContainsKey(id)) return false;

        var cts = new CancellationTokenSource();
        _cancellations[id] = cts;

        Set(m, ModuleState.Installing, "Подготовка…", 0);
        _ = RunInstallAsync(m, cts);
        return true;
    }

    public void Cancel(string id)
    {
        if (_cancellations.TryGetValue(id, out var cts))
        {
            try { cts.Cancel(); } catch { }
        }
    }

    private async Task RunInstallAsync(ModuleInfo m, CancellationTokenSource cts)
    {
        var ct = cts.Token;
        try
        {
            switch (m.Id)
            {
                case "ffmpeg": await InstallFfmpegAsync(m, ct); break;
                case "ytdlp": await InstallYtDlpAsync(m, ct); break;
                case "whisper": await InstallWhisperAsync(m, ct); break;
                case "whisper-model": await InstallWhisperModelAsync(m, ct); break;
                case "llamacpp": await InstallLlamaCppAsync(m, ct); break;
                case "gguf": await InstallGgufAsync(m, ct); break;
                default: throw new InvalidOperationException($"Неизвестный модуль «{m.Id}»");
            }

            Set(m, ModuleState.Ready, "Установлено", null);
        }
        catch (OperationCanceledException)
        {
            Set(m, ModuleState.Missing, "Установка отменена", null);
        }
        catch (Exception ex)
        {
            Set(m, ModuleState.Failed, ex.Message, null);
            AppServices.Log($"Module install FAIL {m.Id}: {ex}");
        }
        finally
        {
            _cancellations.Remove(m.Id);
            cts.Dispose();
            _configStore.Save(_config);
        }
    }

    private async Task DownloadFileAsync(
        ModuleInfo m, string url, string destPath, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        var partPath = destPath + ".part";

        var alreadyHave = File.Exists(partPath) ? new FileInfo(partPath).Length : 0;

        using var response = await SendAsync(url, partPath, alreadyHave, ct);
        if (response == null)
        {
            m.DownloadedBytes = alreadyHave;
            m.TotalBytes = alreadyHave;
            m.StatusDetail = "Файл уже был скачан целиком";
            CompletePart(partPath, destPath);
            m.Progress = null;
            Notify();
            return;
        }

        var resumed = response.StatusCode == System.Net.HttpStatusCode.PartialContent;
        if (alreadyHave > 0 && !resumed)
        {
            alreadyHave = 0;
            if (File.Exists(partPath)) File.Delete(partPath);
        }

        long? total = response.Content.Headers.ContentLength is long len && len > 0
            ? alreadyHave + len
            : null;

        m.TotalBytes = total;
        m.DownloadedBytes = alreadyHave;
        m.StatusDetail = alreadyHave > 0 ? $"Докачка с {Format(alreadyHave)}…" : "Загрузка…";
        Notify();

        await using (var src = await response.Content.ReadAsStreamAsync(ct))
        await using (var dst = new FileStream(
            partPath, alreadyHave > 0 ? FileMode.Append : FileMode.Create,
            FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            var buffer = new byte[81920];
            long read;
            var lastTick = Environment.TickCount64;
            while ((read = await src.ReadAsync(buffer, ct)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, (int)read), ct);
                m.DownloadedBytes += read;
                m.Progress = total is > 0
                    ? Math.Clamp((double)m.DownloadedBytes / total.Value, 0, 1)
                    : null;

                if (Environment.TickCount64 - lastTick >= 100)
                {
                    lastTick = Environment.TickCount64;
                    Notify();
                }
            }

            await dst.FlushAsync(ct);
        }

        CompletePart(partPath, destPath);
        m.Progress = null;
        Notify();
    }

    private static void CompletePart(string partPath, string destPath)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (File.Exists(destPath)) File.Delete(destPath);
                File.Move(partPath, destPath);
                return;
            }
            catch (IOException) when (attempt < 6)
            {
                Thread.Sleep(250 * attempt);
            }
        }
    }

    private async Task<HttpResponseMessage?> SendAsync(
        string url, string partPath, long alreadyHave, CancellationToken ct)
    {
        if (alreadyHave > 0)
        {
            var remote = await TryContentLengthAsync(url, ct);
            if (remote is > 0 && remote == alreadyHave)
                return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (alreadyHave > 0)
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(alreadyHave, null);

        var response = await _http.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            response.Dispose();
            if (File.Exists(partPath)) File.Delete(partPath);

            using var fresh = new HttpRequestMessage(HttpMethod.Get, url);
            response = await _http.SendAsync(
                fresh, HttpCompletionOption.ResponseHeadersRead, ct);
        }

        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch
        {
            var code = (int)response.StatusCode;
            var reason = response.ReasonPhrase ?? response.StatusCode.ToString();
            response.Dispose();
            throw new InvalidOperationException(
                $"Сервер отклонил загрузку: {code} ({reason}).");
        }

        return response;
    }

    private async Task<long?> TryContentLengthAsync(string url, CancellationToken ct)
    {
        try
        {
            using var head = new HttpRequestMessage(HttpMethod.Head, url);
            using var probe = await _http.SendAsync(
                head, HttpCompletionOption.ResponseHeadersRead, ct);

            return probe.IsSuccessStatusCode
                ? probe.Content.Headers.ContentLength
                : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task InstallFfmpegAsync(ModuleInfo m, CancellationToken ct)
    {
        var url = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
        var zip = Path.Combine(Storage.ModulesDir, "ffmpeg.zip");

        Set(m, ModuleState.Installing, "Загрузка FFmpeg…", 0);
        await DownloadFileAsync(m, url, zip, ct);

        Set(m, ModuleState.Installing, "Распаковка…", null);
        var target = Path.Combine(Storage.ModulesDir, "ffmpeg");
        if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
        ZipFile.ExtractToDirectory(zip, target);

        var exe = Directory.GetFiles(target, "ffmpeg.exe", SearchOption.AllDirectories)
            .FirstOrDefault()
            ?? throw new FileNotFoundException("В архиве нет ffmpeg.exe");

        var ffprobe = Path.Combine(Path.GetDirectoryName(exe)!, "ffprobe.exe");
        if (File.Exists(ffprobe))
            File.Copy(ffprobe, Path.Combine(Storage.ModulesDir, "ffprobe.exe"), true);

        File.Delete(zip);
        SetFfmpegPath(exe);
        m.InstalledPath = exe;
    }

    private async Task InstallYtDlpAsync(ModuleInfo m, CancellationToken ct)
    {
        var url = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
        var dest = Path.Combine(Storage.ModulesDir, "yt-dlp.exe");

        Set(m, ModuleState.Installing, "Загрузка yt-dlp…", 0);
        await DownloadFileAsync(m, url, dest, ct);
        SetYtDlpPath(dest);
        m.InstalledPath = dest;
    }

    private async Task InstallWhisperAsync(ModuleInfo m, CancellationToken ct)
    {
        Set(m, ModuleState.Installing,
            $"pip install openai-whisper ({_config.PythonExecutable})", null);

        var log = new System.Text.StringBuilder();
        var exit = await _runner.RunAsync(
            _config.PythonExecutable,
            "-m pip install --upgrade --no-input openai-whisper",
            TimeSpan.FromHours(1),
            onLine: line =>
            {
                if (line == null) return;
                log.AppendLine(line);
                m.StatusDetail = Shorten(line.Trim(), 90);
                Notify();
            },
            onErrLine: line => { if (line != null) log.AppendLine(line); },
            ct: ct);

        if (exit != 0)
            throw new InvalidOperationException(
                $"pip завершился с кодом {exit}. Проверьте, что Python установлен " +
                $"и путь «{_config.PythonExecutable}» верен.\n{Shorten(log.ToString(), 400)}");
    }

    private async Task InstallWhisperModelAsync(ModuleInfo m, CancellationToken ct)
    {
        var model = _config.WhisperModel;
        var target = WhisperService.ModelFileName(model);
        var before = File.Exists(target) ? new FileInfo(target).Length : 0L;

        Set(m, ModuleState.Installing, $"Загрузка весов модели {model}…", 0);

        var code = $"import whisper; whisper.load_model('{model}', download_root=r'{Storage.WhisperDir}')";
        using var p = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _config.PythonExecutable,
                Arguments = $"-c \"{code}\"",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            }
        };

        var err = new System.Text.StringBuilder();
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) err.AppendLine(e.Data); };

        p.Start();
        p.BeginErrorReadLine();

        while (!p.HasExited)
        {
            ct.ThrowIfCancellationRequested();
            if (File.Exists(target))
            {
                var now = new FileInfo(target).Length;
                if (now > before)
                {
                    m.DownloadedBytes = now - before;
                    m.TotalBytes = null;
                    m.Progress = null;
                    m.StatusDetail = $"Загружено {Format(now - before)}";
                    Notify();
                }
            }

            await Task.Delay(700, ct);
        }

        await p.WaitForExitAsync(ct);

        if (p.ExitCode != 0 || !File.Exists(target))
            throw new InvalidOperationException(
                $"Не удалось скачать модель {model} (код {p.ExitCode}).\n{Shorten(err.ToString(), 400)}");

        m.InstalledPath = target;
    }

    private async Task InstallLlamaCppAsync(ModuleInfo m, CancellationToken ct)
    {
        Set(m, ModuleState.Installing, "Поиск сборки llama.cpp…", null);

        using var rel = await _http.GetAsync(
            "https://api.github.com/repos/ggml-org/llama.cpp/releases/latest", ct);
        rel.EnsureSuccessStatusCode();
        var json = await rel.Content.ReadAsStringAsync(ct);

        var assets = System.Text.RegularExpressions.Regex
            .Matches(json, "\"browser_download_url\"\\s*:\\s*\"(?<url>[^\"]+)\"")
            .Select(x => x.Groups["url"].Value)
            .ToList();

        var pick =
            assets.FirstOrDefault(u => u.EndsWith("-bin-win-cpu-x64.zip", StringComparison.OrdinalIgnoreCase))
            ?? assets.FirstOrDefault(u => u.EndsWith("-bin-win-avx2-x64.zip", StringComparison.OrdinalIgnoreCase))
            ?? assets.FirstOrDefault(u => u.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                "В последнем релизе llama.cpp не нашлось Windows-сборки. " +
                "Скачайте llama-server вручную и укажите путь в настройках.");

        Set(m, ModuleState.Installing, "Загрузка llama.cpp…", 0);
        var zip = Path.Combine(Storage.ModulesDir, "llamacpp.zip");
        await DownloadFileAsync(m, pick, zip, ct);

        Set(m, ModuleState.Installing, "Распаковка…", null);
        var target = Path.Combine(Storage.ModulesDir, "llamacpp");
        if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
        ZipFile.ExtractToDirectory(zip, target);
        File.Delete(zip);

        var exe = Directory.GetFiles(target, "llama-server.exe", SearchOption.AllDirectories)
            .FirstOrDefault()
            ?? throw new FileNotFoundException("В архиве нет llama-server.exe");

        SetLlamaPath(exe);
        m.InstalledPath = exe;
    }

    private async Task InstallGgufAsync(ModuleInfo m, CancellationToken ct)
    {
        const string url =
            "https://huggingface.co/ggml-org/gpt-oss-20b-GGUF/resolve/main/gpt-oss-20b-MXFP4.gguf";

        var dest = Path.Combine(Storage.ModulesDir, "gpt-oss-20b-MXFP4.gguf");
        Set(m, ModuleState.Installing, "Загрузка GGUF ~12 ГБ (можно прервать)…", 0);
        await DownloadFileAsync(m, url, dest, ct);

        SetLlamaModelPath(dest);
        m.InstalledPath = dest;
    }

    public void Remove(string id)
    {
        if (!_modules.TryGetValue(id, out var m)) return;
        if (_cancellations.ContainsKey(id)) return;

        try
        {
            if (!string.IsNullOrEmpty(m.InstalledPath)
                && m.InstalledPath.StartsWith(Storage.ModulesDir, StringComparison.OrdinalIgnoreCase)
                && File.Exists(m.InstalledPath))
            {
                File.Delete(m.InstalledPath);
            }

            switch (id)
            {
                case "ffmpeg": _config.FfmpegPath = "ffmpeg"; break;
                case "ytdlp": _config.YtDlpPath = "yt-dlp"; break;
                case "llamacpp": _config.LlamaServerPath = "llama-server"; break;
                case "gguf": _config.LlamaModelPath = ""; break;
            }

            m.State = ModuleState.Missing;
            m.InstalledPath = null;
            m.StatusDetail = "удалено";
            Notify();
            _configStore.Save(_config);
        }
        catch (Exception ex)
        {
            Set(m, ModuleState.Failed, "Не удалось удалить: " + ex.Message, null);
        }
    }

    private void SetFfmpegPath(string p) => _config.FfmpegPath = p;
    private void SetYtDlpPath(string p) => _config.YtDlpPath = p;
    private void SetLlamaPath(string p) => _config.LlamaServerPath = p;
    private void SetLlamaModelPath(string p) => _config.LlamaModelPath = p;

    public static string Format(long bytes)
    {
        if (bytes <= 0) return "—";
        string[] units = { "Б", "КБ", "МБ", "ГБ", "ТБ" };
        double v = bytes;
        var i = 0;
        while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        return v >= 100 || i == 0 ? $"{v:0} {units[i]}" : $"{v:0.#} {units[i]}";
    }

    private static string ShortPath(string path)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return path.StartsWith(appData, StringComparison.OrdinalIgnoreCase)
            ? "%APPDATA%" + path[appData.Length..].Replace('/', '\\')
            : path;
    }

    private static string Shorten(string s, int max)
    {
        s = s.Trim();
        return s.Length <= max ? s : s[..max] + "…";
    }
}
