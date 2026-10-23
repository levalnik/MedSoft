using FluentAssertions;
using HospitalInformationSystem.Contracts.Auth;
using HospitalInformationSystem.Infrastructure.Identity;

namespace HospitalInformationSystem.UnitTests;

public sealed class AccountUserNameValidatorTests
{
    [Theory]
    [InlineData("a._@+-9", true)]
    [InlineData("User.123", true)]
    [InlineData("abc/123", false)]
    [InlineData("Логин", false)]
    [InlineData("a b", false)]
    [InlineData(null, false)]
    public void ValidateUserName_SharedRules_ShouldPreserveAllowedAlphabet(string? name, bool valid)
    {
        AccountUserNameRules.IsValid(name).Should().Be(valid);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("ab", false)]
    [InlineData("abc", true)]
    [InlineData("abcdefghijklmnop", true)]
    [InlineData("abcdefghijklmnopq", false)]
    public async Task ValidateAsync_BoundaryLengths_ShouldMatchAccountServiceRules(string? name, bool valid)
    {
        var result = await new AccountUserNameValidator().ValidateAsync(null!, new HospitalUser { UserName = name });

        result.Succeeded.Should().Be(valid);
        if (!valid)
            result.Errors.Should().ContainSingle().Which.Code.Should().Be("InvalidUserNameLength");
    }
}
