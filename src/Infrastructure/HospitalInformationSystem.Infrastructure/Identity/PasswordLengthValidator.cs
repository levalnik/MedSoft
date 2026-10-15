using Microsoft.AspNetCore.Identity;

namespace HospitalInformationSystem.Infrastructure.Identity;

public sealed class PasswordLengthValidator : IPasswordValidator<HospitalUser>
{
    public Task<IdentityResult> ValidateAsync(UserManager<HospitalUser> manager, HospitalUser user, string? password) =>
        Task.FromResult(password is { Length: >= 12 and <= 128 }
            ? IdentityResult.Success
            : IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordLength", Description = "Пароль должен содержать от 12 до 128 символов."
            }));
}
