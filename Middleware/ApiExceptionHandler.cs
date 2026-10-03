using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using otw.fings.api.management.Services;

namespace otw.fings.api.management.Middleware;

public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            ValidationException => (StatusCodes.Status422UnprocessableEntity, "Business validation failed"),
            ReceiptImageQualityException => (StatusCodes.Status422UnprocessableEntity, "Receipt image quality insufficient"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            IntegrationUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Integration unavailable"),
            DbUpdateException => (StatusCodes.Status409Conflict, "Persistence conflict"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error")
        };
        if (status >= 500)
        {
            logger.LogError(exception, "Unhandled API error");
        }
        else
        {
            logger.LogWarning("API request failed with {StatusCode}: {Message}", status, exception.Message);
        }

        context.Response.StatusCode = status;
        var extensions = exception is ReceiptImageQualityException qualityException
            ? new Dictionary<string, object?>
            {
                ["code"] = "receipt_image_quality_insufficient",
                ["imageQuality"] = qualityException.Quality
            }
            : null;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = status == 500 ? "Ocorreu um erro inesperado." : exception.Message,
                Extensions = extensions ?? new Dictionary<string, object?>()
            },
            Exception = exception
        });
    }
}
