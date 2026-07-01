namespace ConsistencyClass.Membership;

public record MemberId(string Value)
{
    public static MemberId Of(string value) => new(value);
    public static MemberId Random() => new(Guid.NewGuid().ToString());
}
