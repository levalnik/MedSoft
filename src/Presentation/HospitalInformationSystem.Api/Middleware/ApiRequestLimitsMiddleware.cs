using HospitalInformationSystem.Api.Security;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Metadata;

namespace HospitalInformationSystem.Api.Middleware;

public sealed class ApiRequestLimitsMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var limit = context.GetEndpoint()?.Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize
                    ?? ApiSecurityRegistration.MaxJsonBodyBytes;
        if (context.Request.ContentLength > limit)
        {
            await Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Размер запроса превышает допустимый.").ExecuteAsync(context);
            return;
        }

        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } sizeFeature)
            sizeFeature.MaxRequestBodySize = limit;
        if (context.Request.ContentLength is null)
        {
            context.Request.EnableBuffering(bufferThreshold: 32 * 1024, bufferLimit: limit);
            try
            {
                if (context.Request.Body.CanSeek && context.Request.Body.Length > limit)
                    throw new IOException();
                await context.Request.Body.CopyToAsync(Stream.Null, 8192, context.RequestAborted);
                if (context.Request.Body.Position > limit)
                    throw new IOException();
                context.Request.Body.Position = 0;
            }
            catch (IOException)
            {
                await Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge,
                    title: "Размер запроса превышает допустимый.").ExecuteAsync(context);
                return;
            }
        }
        await next(context);
    }
}
