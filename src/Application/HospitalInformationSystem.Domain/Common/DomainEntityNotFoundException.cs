namespace HospitalInformationSystem.Domain.Common;

public sealed class DomainEntityNotFoundException(string message) : Exception(message);
