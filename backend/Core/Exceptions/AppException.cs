namespace TecNM.Api.Core.Exceptions;

public class AppException(
    int statusCode,
    string code,
    string message,
    string title = "Error de operación",
    IDictionary<string, string[]>? errors = null)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public string Title { get; } = title;
    public IDictionary<string, string[]>? Errors { get; } = errors;
}

public class NotFoundException(string code, string message)
    : AppException(404, code, message, "Recurso no encontrado");

public class ConflictException(string code, string message, IDictionary<string, string[]>? errors = null)
    : AppException(409, code, message, "Conflicto con el estado actual", errors);

public class BusinessRuleException(string code, string message, IDictionary<string, string[]>? errors = null)
    : AppException(422, code, message, "Regla de negocio no cumplida", errors);

public class ValidationException(IDictionary<string, string[]> errors)
    : AppException(400, "validation.failed", "Uno o más campos presentan errores de validación.", "Error de validación", errors);
