namespace ConsistencyClass.LoyaltyWallets.Access;

using ConsistencyClass.Membership;

// note, we assume here, all members have the same level of access
// if we want to allow read only, this will need to be a wrapper
public sealed record WalletAccess
{
    private readonly HashSet<MemberId> _members;

    private WalletAccess(HashSet<MemberId> members) => _members = members;

    public IReadOnlySet<MemberId> Members => _members;
    public int Size => _members.Count;
    public bool Has(MemberId memberId) => _members.Contains(memberId);

    public WalletAccess Add(MemberId memberId)
    {
        var newMembers = new HashSet<MemberId>(_members) { memberId };
        return new WalletAccess(newMembers);
    }

    public WalletAccess Revoke(MemberId memberId)
    {
        var newMembers = new HashSet<MemberId>(_members);
        newMembers.Remove(memberId);
        return new WalletAccess(newMembers);
    }

    public static WalletAccess Empty() => new(new HashSet<MemberId>());
    public static WalletAccess Of(params MemberId[] members) => new(new HashSet<MemberId>(members));

    public bool Equals(WalletAccess? other) =>
        other is not null && _members.SetEquals(other._members);

    public override int GetHashCode()
    {
        var hash = 0;
        foreach (var m in _members)
            hash ^= m.GetHashCode();
        return hash;
    }
}
