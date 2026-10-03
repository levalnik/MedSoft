namespace HospitalInformationSystem.Web.ApiClients;

public sealed class ApiException(string message) : Exception(message);
