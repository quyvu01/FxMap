using FxMap.Fluent;
using Shared.DistributedKeys;

namespace Service1.Contract.Responses;

public class SimpleMemberResponse
{
    public string UserId { get; set; }
    public string UserEmail { get; set; }
}

public class SimpleMemberResponseProfile : ProfileOf<SimpleMemberResponse>
{
    protected override void Configure()
    {
        UseDistributedKey<UserDistributedKey>()
            .Of(x => x.UserId)
            .For(x => x.UserEmail, "UserEmail");
    }
}