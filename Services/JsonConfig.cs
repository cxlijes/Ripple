using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ripple.Services;

public class JsonConfigStore : IConfigStore
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _lock = new();

    public AppConfig Load()
    {
        lock (_lock)
        {
            if (!File.Exists(Storage.ConfigPath)) return new();
            try
            {
                return JsonSerializer.Deserialize<AppConfig>(
                    File.ReadAllText(Storage.ConfigPath), _jsonOptions) ?? new();
            }
            catch
            {
                return new();
            }
        }
    }

    public void Save(AppConfig config)
    {
        lock (_lock) AtomicFile.Write(Storage.ConfigPath, JsonSerializer.Serialize(config, _jsonOptions));
    }
}
