using System.Net;

namespace HospitalInformationSystem.Web.ApiClients;

public sealed class ApiException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null)
    : Exception(message, innerException)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;
}
