using LocalRagLab.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace LocalRagLab.Api.Middleware;

public sealed class ApiExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiExceptionMiddleware> _logger;

    public ApiExceptionMiddleware(
        RequestDelegate next,
        ILogger<ApiExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation("Request was cancelled by the client.");
        }
        catch (Exception exception)
        {
            var (statusCode, title) = exception switch
            {
                ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request"),
                OllamaException => (StatusCodes.Status503ServiceUnavailable, "Local AI service unavailable"),
                _ => (StatusCodes.Status500InternalServerError, "Unexpected server error")
            };

            _logger.LogError(exception, "Request failed with status code {StatusCode}.", statusCode);

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/problem+json";

            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = exception.Message,
                Instance = context.Request.Path
            });
        }
    }
}
