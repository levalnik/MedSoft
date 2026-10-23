namespace HospitalInformationSystem.Contracts.Auth;

public static class AccountUserNameRules
{
    public const int MinLength = 3;
    public const int MaxLength = 16;
    public const string AllowedCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

    public static bool HasValidLength(string? userName) =>
        userName is not null && userName.Length is >= MinLength and <= MaxLength;

    public static bool IsValid(string? userName) => HasValidLength(userName) &&
        userName!.All(character => AllowedCharacters.Contains(character));
}
