namespace ConsistencyClass.Core.Projections;

public interface IProjection<in TEvent>
{
    public ValueTask Apply(IReadOnlyList<TEvent> events);
}

public sealed class Projection<TDocument, TEvent>(
    DatabaseCollection<TDocument> collection,
    IReadOnlySet<Type> canHandle,
    Func<TEvent, string> getDocumentId,
    Func<TDocument?, TEvent, TDocument?> evolve): IProjection<TEvent>
    where TDocument : class
    where TEvent : notnull
{
    public async ValueTask Apply(IReadOnlyList<TEvent> events)
    {
        foreach (var @event in events)
        {
            if (!canHandle.Contains(@event.GetType()))
                continue;

            var id = getDocumentId(@event);
            var current = await collection.Find(id);
            var next = evolve(current, @event);

            if (next is null)
                continue;

            await collection.Save(id, next);
        }
    }
}

public static class Projections
{
    public static async ValueTask ApplyProjections<TEvent>(
        IReadOnlyList<IProjection<TEvent>> projections,
        IReadOnlyList<TEvent> events)
    {
        foreach (var projection in projections)
            await projection.Apply(events);
    }
}
