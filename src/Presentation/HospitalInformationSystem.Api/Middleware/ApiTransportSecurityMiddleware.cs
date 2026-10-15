namespace HospitalInformationSystem.Api.Middleware;

public sealed class ApiTransportSecurityMiddleware(RequestDelegate next, IWebHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing") && !context.Request.IsHttps)
        {
            await Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Для подключения требуется HTTPS.").ExecuteAsync(context);
            return;
        }

        await next(context);
    }
}
