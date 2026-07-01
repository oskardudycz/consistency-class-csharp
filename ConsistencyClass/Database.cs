using System.Collections.Concurrent;

namespace ConsistencyClass;

public class Database
{
    public static DatabaseCollection<T> Collection<T>() => new();
}

public class DatabaseCollection<T>
{
    private readonly ConcurrentDictionary<string, T> _entries = new();

    public ValueTask Save(string id, T record)
    {
        _entries[id] = record;
        return ValueTask.CompletedTask;
    }

    public ValueTask<T?> Find(string id) =>
        ValueTask.FromResult(_entries.TryGetValue(id, out var record) ? record : default);

    public ValueTask<IReadOnlyList<T>> GetAll() =>
        ValueTask.FromResult<IReadOnlyList<T>>([.. _entries.Values]);
}
