using ConsistencyClass.Membership;
using ConsistencyClass.Membership.MemberDirectory;

namespace ConsistencyClass.Tests.Membership;

public class MemberVerifiedTests
{
    private readonly DatabaseCollection<Member> _members = Database.Collection<Member>();

    [Fact]
    public async Task RecordsTheMemberInTheDirectoryOnTheirTier()
    {
        var oskar = MemberId.Random();

        // when
        await new MemberVerifiedHandler(_members).Handle(new MemberVerified(oskar, Tier.Gold));

        // then the member is in the directory on their tier
        var member = await _members.Find(oskar.Value);
        member.ShouldNotBeNull();
        member.MemberId.ShouldBe(oskar);
        member.Tier.ShouldBe(Tier.Gold);
    }
}
