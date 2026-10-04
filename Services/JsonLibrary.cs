using Ripple.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ripple.Services;

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

            if (idx >= 0) all[idx] = lecture; else all.Insert(0, lecture);

            SaveCore(all);
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
            var json = File.ReadAllText(Storage.LibraryPath);
            var lectures = JsonSerializer.Deserialize<List<Lecture>>(json, _jsonOptions);

            if (lectures != null)
            {
                return lectures;
            }
            else
            {
                return new List<Lecture>();
            }
        }

        catch
        {
            return new();
        }
    }

    private static void SaveCore(IEnumerable<Lecture> lectures)
    {
        AtomicFile.Write(Storage.LibraryPath, JsonSerializer.Serialize(lectures, _jsonOptions));
    }
}
