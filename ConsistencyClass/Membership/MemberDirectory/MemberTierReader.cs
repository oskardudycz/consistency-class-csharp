namespace ConsistencyClass.Membership.MemberDirectory;

public delegate ValueTask<Tier> GetMemberTier(MemberId memberId);

public class MemberTierReader(DatabaseCollection<Member> members)
{
    public async ValueTask<Tier> GetTier(MemberId memberId)
    {
        var member = await members.Find(memberId.Value) ?? throw new InvalidOperationException("Unknown member");
        return member.Tier;
    }
}
