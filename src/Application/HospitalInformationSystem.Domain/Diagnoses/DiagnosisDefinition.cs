using HospitalInformationSystem.Domain.Common;

namespace HospitalInformationSystem.Domain.Diagnoses;

public sealed class DiagnosisDefinition
{
    public DiagnosisDefinition(Guid id, string code, string name,
        DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc)
    {
        Id = id;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        Update(code, name, updatedAtUtc);
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static DiagnosisDefinition Create(string code, string name, DateTimeOffset now) =>
        new(Guid.NewGuid(), code, name, now, now);

    public void Update(string code, string name, DateTimeOffset now)
    {
        var validCode = code?.Trim();
        var validName = name?.Trim();
        if (validCode?.Length != 6)
            throw new DomainValidationException("Код диагноза: требуется ровно 6 символов.");
        if (validName is null || validName.Length is < 3 or > 24)
            throw new DomainValidationException("Название диагноза: требуется от 3 до 24 символов.");
        Code = validCode;
        Name = validName;
        UpdatedAtUtc = now.ToUniversalTime();
    }
}

