namespace HospitalInformationSystem.Domain.Common;

public sealed class DomainValidationException(string message) : Exception(message);
