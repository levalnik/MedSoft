namespace HospitalInformationSystem.Contracts.Auth;

public sealed record LoginRequest(string UserName, string Password);

public sealed record AntiforgeryTokenDto(string RequestToken);

public sealed record SessionDto(Guid Id, string UserName, string Role, Guid? MedicalWorkerId);

public sealed record AccountDto(Guid Id, string UserName, string Role, Guid? MedicalWorkerId, bool IsLockedOut);

public sealed record CreateAccountRequest(string UserName, string Password, string Role, Guid? MedicalWorkerId);
