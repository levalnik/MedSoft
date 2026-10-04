namespace HospitalInformationSystem.Domain.Common;

public sealed class BusinessRuleConflictException(string message) : Exception(message);
