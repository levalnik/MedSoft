using FluentValidation;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Domain.Common;
using HospitalInformationSystem.Api.Security;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace HospitalInformationSystem.Api.Middleware;

public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception,
        CancellationToken cancellationToken)
    {
        ProblemDetails problem = exception switch
        {
            ValidationException validation => new ValidationProblemDetails(validation.Errors
                .GroupBy(x => x.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(x => x.ErrorMessage).Distinct().ToArray()))
            {
                Status = 400, Title = "Проверьте введённые данные."
            },
            DomainValidationException => new ProblemDetails
                { Status = 400, Title = "Некорректные данные.", Detail = exception.Message },
            BadHttpRequestException badRequest => new ProblemDetails
            {
                Status = badRequest.StatusCode, Title = "Некорректный HTTP-запрос."
            },
            ForbiddenOperationException => new ProblemDetails
                { Status = 403, Title = "Недостаточно прав для выполнения операции." },
            EntityNotFoundException => new ProblemDetails
                { Status = 404, Title = "Запись не найдена.", Detail = exception.Message },
            DomainEntityNotFoundException => new ProblemDetails
                { Status = 404, Title = "Запись не найдена.", Detail = exception.Message },
            BusinessConflictException or BusinessRuleConflictException => new ProblemDetails
            {
                Status = 409, Title = "Операция конфликтует с текущим состоянием данных.", Detail = exception.Message
            },
            _ => new ProblemDetails { Status = 500, Title = "Не удалось выполнить запрос." }
        };
        if (problem.Status == 500)
            logger.LogError("Unexpected {ExceptionType} processing {Method}; trace {TraceId}; stack {StackTrace}",
                exception.GetType().FullName, context.Request.Method, context.TraceIdentifier, exception.StackTrace);
        context.Response.StatusCode = problem.Status!.Value;
        problem.Instance = context.Request.Path;
        problem.Extensions["traceId"] = context.TraceIdentifier;
        if (!await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
                { HttpContext = context, ProblemDetails = problem }))
        {
            await context.Response.WriteAsJsonAsync((object)problem, options: null,
                contentType: "application/problem+json", cancellationToken: cancellationToken);
        }

        return true;
    }
}
