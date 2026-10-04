using Ripple;
using Ripple.Services;
using System.Text.Json;
using System.Text.Json.Serialization;

public class JsonLibraryStore : ILibraryStore
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _lock = new();

    public IReadOnlyList<Lecture> LoadAll()
    {
        lock (_lock) return LoadCore();
    }

    public void SaveAll(IEnumerable<Lecture> lectures)
    {
        lock (_lock) SaveCore(lectures);
    }

    public void Upsert(Lecture lecture)
    {
        lock (_lock)
        {
            var all = LoadCore();
            var idx = all.FindIndex(l => l.Id == lecture.Id);
            if (idx >= 0)
            {
                all[idx] = lecture;
            }

            else
            {
                all.Insert(0, lecture);
            }

            SaveCore(all);
        }
    }

    public bool Remove(string id)
    {
        lock (_lock)
        {
            var all = LoadCore();
            var removed = all.RemoveAll(l => l.Id == id) > 0;
            if (removed) SaveCore(all);
            return removed;
        }
    }

    private static List<Lecture> LoadCore()
    {
        if (!File.Exists(Storage.LibraryPath))
        {
            return new();
        }

        try
        {
            return JsonSerializer.Deserialize<List<Lecture>>(
                File.ReadAllText(Storage.LibraryPath), _jsonOptions) ?? new();
        }

        catch
        {
            return new();
        }
    }

    private static void SaveCore(IEnumerable<Lecture> lectures)
        => AtomicFile.Write(Storage.LibraryPath, JsonSerializer.Serialize(lectures, _jsonOptions));
}
