using FxMap.Fluent;
using Service2.Dtos;
using Shared.DistributedKeys;

namespace Service2.Profiles;

public class UserResponseProfile : ProfileOf<UserResponse>
{
    protected override void Configure()
    {
        UseDistributedKey<UserDistributedKey>()
            .Of(x => x.Id + x.Email)
            .For(x => x.Name)
            .For(x => x.Email, "UserEmail");
    }
}