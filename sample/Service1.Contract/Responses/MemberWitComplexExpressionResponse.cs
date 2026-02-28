using FxMap.Fluent;
using Shared.Attributes;

namespace Service1.Contract.Responses;

public class MemberWitComplexExpressionResponse
{
    public string UserId { get; set; }
    public UserResponse User { get; set; }
}

public class MemberWitComplexExpressionResponseProfile : ProfileOf<MemberWitComplexExpressionResponse>
{
    protected override void Configure()
    {
        UseDistributedKey<UserOfAttribute>()
            .Of(x => x.UserId)
            .For(x => x.User, "{Id, UserEmail, ProvinceId}");
    }
}