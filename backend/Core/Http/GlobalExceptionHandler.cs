using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using TecNM.Api.Core.Exceptions;

namespace TecNM.Api.Core.Http;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is DbUpdateException { InnerException: PostgresException pg }) exception = pg;
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        var (statusCode, title, code, detail, errors) = exception switch
        {
            DbUpdateConcurrencyException => (409, "Conflicto de actualización", "documents.concurrent_update", "Otro usuario modificó este registro. Actualiza e intenta nuevamente.", (IDictionary<string, string[]>?)null),
            PostgresException pgEx when pgEx.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected => (409, "Conflicto concurrente", "documents.concurrent_update", "La operación coincidió con otra actualización. Actualiza e intenta nuevamente.", null),
            AppException appEx => (
                appEx.StatusCode,
                appEx.Title,
                appEx.Code,
                appEx.Message,
                appEx.Errors),

            PostgresException pgEx when pgEx.SqlState == PostgresErrorCodes.UniqueViolation => (
                StatusCodes.Status409Conflict,
                "Conflicto de unicidad",
                "database.unique_violation",
                "Ya existe un registro con los mismos datos únicos especificados.",
                null),

            PostgresException pgEx when pgEx.SqlState == PostgresErrorCodes.ForeignKeyViolation => (
                StatusCodes.Status409Conflict,
                "Conflicto de integridad referencial",
                "database.foreign_key_violation",
                "La operación no puede realizarse porque el registro tiene dependencias asociadas.",
                null),

            PostgresException pgEx when pgEx.SqlState == PostgresErrorCodes.CheckViolation => (
                StatusCodes.Status400BadRequest,
                "Violación de restricción de datos",
                "database.check_violation",
                "Uno o más valores no cumplen con las reglas de validación de la base de datos.",
                null),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Error interno del servidor",
                "server.error",
                "Ha ocurrido un error inesperado al procesar la solicitud.",
                null)
        };

        if (statusCode >= 500)
        {
            logger.LogError(exception, "Unhandled error caught by GlobalExceptionHandler: {Message}", exception.Message);
        }
        else
        {
            logger.LogWarning("Handled application exception: {Code} - {Detail}", code, detail);
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path,
            Extensions =
            {
                ["code"] = code,
                ["traceId"] = traceId,
                ["errors"] = errors
            }
        };

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
