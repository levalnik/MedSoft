namespace HospitalInformationSystem.Application.Common;

public sealed class EntityNotFoundException(string message) : Exception(message);
