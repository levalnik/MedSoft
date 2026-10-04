namespace HospitalInformationSystem.Application.Common;

public sealed class BusinessConflictException(string message, Exception? innerException = null)
    : Exception(message, innerException);
