namespace ConsistencyClass.Core;

using ConsistencyClass.Core.Projections;

internal class EventStore<TEvent>(IReadOnlyList<IProjection<TEvent>> projections) where TEvent : notnull
{
    private readonly DatabaseCollection<EventStream> streams = Database.Collection<EventStream>();

    public async ValueTask<IReadOnlyList<T>> ReadEvents<T>(string streamId)
    {
        var stream = await ExistingEventStreamOrEmpty(streamId);
        return stream.EventsOfType<T>();
    }

    public async ValueTask AppendToStream<T>(
        string streamId,
        IReadOnlyList<T> events,
        bool requireNew = false) where T : notnull
    {
        if (events.Count == 0)
            return;

        var stream = await ExistingEventStreamOrEmpty(streamId);
        if (requireNew && stream.Events.Count != 0)
            throw new InvalidOperationException("Stream already exists");
        var version = stream.Events.Count;
        var newEvents = events.Select(e => EventEnvelope.From(streamId, e, ++version)).ToList();

        await streams.Save(streamId, stream.Append(newEvents));
        await ConsistencyClass.Core.Projections.Projections.ApplyProjections(
            projections,
            newEvents.Select(e => e.Data).OfType<TEvent>().ToList());
    }

    private async ValueTask<EventStream> ExistingEventStreamOrEmpty(string streamId) =>
        await streams.Find(streamId) ?? EventStream.Empty(streamId);
}

internal record EventStream(string Id, IReadOnlyList<EventEnvelope> Events)
{
    public static EventStream Empty(string id) => new(id, []);

    public EventStream Append(IReadOnlyList<EventEnvelope> events) =>
        new(Id, [.. Events, .. events]);

    public IReadOnlyList<T> EventsOfType<T>() =>
        Events.Select(e => e.Data).OfType<T>().ToList();
}

internal record EventMetadata(
    string StreamId,
    string EventType,
    Guid EventId,
    int Version,
    DateTime OccurredAt
)
{
    public static EventMetadata From(Type eventType, string streamId, int version) =>
        new(streamId, eventType.FullName ?? eventType.Name, Guid.NewGuid(), version, DateTime.UtcNow);
}

internal record EventEnvelope(object Data, EventMetadata Metadata)
{
    public static EventEnvelope From(string streamId, object eventObj, int version) =>
        new(eventObj, EventMetadata.From(eventObj.GetType(), streamId, version));
}
