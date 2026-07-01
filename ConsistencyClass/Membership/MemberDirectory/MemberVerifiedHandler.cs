namespace ConsistencyClass.Membership.MemberDirectory;

public record MemberVerified(MemberId MemberId, Tier Tier);

public class MemberVerifiedHandler(DatabaseCollection<Member> members)
{
    public ValueTask Handle(MemberVerified @event) =>
        members.Save(@event.MemberId.Value, new Member(@event.MemberId, @event.Tier));
}
