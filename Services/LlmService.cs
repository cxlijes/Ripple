using System.Net.Http.Json;
using System.Text.Json;

namespace Ripple.Services;

public class LlmService : ISummarizationService
{
    private readonly AppConfig _config;
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(10) };

    public LlmService(AppConfig config) => _config = config;

    public async Task<bool> IsServerAliveAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var resp = await _http.GetAsync($"{_config.LlamaBaseUrl}/v1/models", cts.Token);

            return resp.IsSuccessStatusCode;
        }

        catch
        {
            return false;
        }
    }

    public async Task EnsureServerAsync(CancellationToken ct = default)
    {
        if (await IsServerAliveAsync())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_config.LlamaModelPath) || !File.Exists(_config.LlamaModelPath))
        {
            throw new FileNotFoundException(
                "Путь к GGUF-модели gpt-oss-20b не задан или файл не найден. " +
                "Укажите его в Настройках (репо ggml-org/gpt-oss-20b-GGUF на Hugging Face).");
        }

        var args =
            $"-m \"{_config.LlamaModelPath}\" " +
            $"--host {_config.LlamaHost} --port {_config.LlamaPort} " +
            "-c 8192 --jinja -np 1";

        _ = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = _config.LlamaServerPath,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException($"Не удалось запустить '{_config.LlamaServerPath}'.");

        for (var i = 0; i < 60; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (await IsServerAliveAsync()) return;
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }

        throw new TimeoutException("llama-server не поднялся за 2 минуты.");
    }

    public async Task<string> SummarizeAsync(Lecture lecture, CancellationToken ct = default)
    {
        await EnsureServerAsync(ct);

        const int chunkChars = 24000;
        var text = lecture.FullText.Trim();
        if (text.Length == 0) throw new InvalidOperationException("Транскрипт пуст.");

        var system =
            "Ты — ассистент студента, делающий конспект лекции на русском языке. " +
            "Отвечай только на русском. Пиши Markdown: заголовки, тезисы, списки, " +
            "выделяй ключевые термины и формулы. Будь точен, ничего не выдумывай — " +
            "опирайся только на текст транскрипции. Не добавляй вступлений вроде «вот конспект».";

        var question =
            $"Лекция «{lecture.Title}». Составь конспект:\n" +
            "1. Краткое содержание (3–5 предложений)\n" +
            "2. Основные тезисы (нумерованный список)\n" +
            "3. Ключевые термины и определения\n" +
            "4. Вопросы для повторения (5–7 штук)\n\n" +
            $"Транскрипция:\n\"\"\"\n{text}\n\"\"\"";

        if (question.Length <= chunkChars)
            return await ChatAsync(system, question, ct);

        var chunks = SplitText(text, chunkChars);
        var partials = new List<string>();
        for (var i = 0; i < chunks.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            partials.Add(await ChatAsync(system,
                $"Это часть {i + 1} из {chunks.Count} лекции «{lecture.Title}». " +
                $"Выдели тезисы и термины этой части:\n\"\"\"\n{chunks[i]}\n\"\"\"", ct));
        }

        return await ChatAsync(system,
            $"Лекция «{lecture.Title}» разбита на {partials.Count} частей. " +
            "Сведи конспекты частей в один конспект по плану:\n" +
            "1. Краткое содержание\n2. Основные тезисы\n3. Ключевые термины\n4. Вопросы для повторения\n\n" +
            string.Join("\n\n---\n\n", partials), ct);
    }

    public async Task<string> ChatAsync(string system, string user, CancellationToken ct = default)
    {
        var payload = new
        {
            model = string.IsNullOrWhiteSpace(_config.LlamaModelName) ? "gpt-oss-20b" : _config.LlamaModelName,
            messages = new object[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user }
            },
            temperature = 0.3,
            max_tokens = 4096
        };

        using var resp = await _http.PostAsJsonAsync($"{_config.LlamaBaseUrl}/v1/chat/completions", payload, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(
            await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return content?.Trim() ?? "";
    }

    internal static List<string> SplitText(string text, int size)
    {
        var parts = new List<string>();
        var pos = 0;
        while (pos < text.Length)
        {
            var len = Math.Min(size, text.Length - pos);
            if (len < text.Length - pos)
            {
                var nl = text.LastIndexOf("\n\n", pos + len, StringComparison.Ordinal);
                if (nl > pos + size / 2)
                {
                    len = nl - pos;
                }
            }

            parts.Add(text.Substring(pos, len));
            pos += len;
        }

        return parts;
    }
}
