using HospitalInformationSystem.Contracts.Auth;
using Microsoft.AspNetCore.Identity;

namespace HospitalInformationSystem.Infrastructure.Identity;

public sealed class AccountUserNameValidator : IUserValidator<HospitalUser>
{
    public Task<IdentityResult> ValidateAsync(UserManager<HospitalUser> manager, HospitalUser user) =>
        Task.FromResult(AccountUserNameRules.HasValidLength(user.UserName)
            ? IdentityResult.Success
            : IdentityResult.Failed(new IdentityError
            {
                Code = "InvalidUserNameLength",
                Description = "Логин должен содержать от 3 до 16 символов."
            }));
}
