using OfX.Fluent;
using Shared.Attributes;

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
        UseAnnotate<UserOfAttribute>()
            .Of(x => x.UserId)
            .For(x => x.UserEmail, "UserEmail");
    }
}