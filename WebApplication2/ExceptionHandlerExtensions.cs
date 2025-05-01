using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using static System.Diagnostics.Debug;

namespace WebApplication2;

internal sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    
    // Поля, которые всегда будут маскироваться, независимо от наличия атрибута
    private static readonly HashSet<string> DefaultSensitiveFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "pwd", "secret", "key", "token", "apikey", "api_key", 
        "accesstoken", "access_token", "refreshtoken", "refresh_token",
        "credential", "pin", "passcode", "pass", "privatekey", "private_key",
        "file", "files", "attachment", "upload", "document", "binary", "content", "stream"
    };

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
    HttpContext httpContext,
    Exception exception,
    CancellationToken cancellationToken)
{
    var method = httpContext.Request.Method;
    var path = httpContext.Request.Path;
    var contentType = httpContext.Request.ContentType;

        // Загружаем тело запроса
        string? bodyContent = await ReadRequestBodyAsync(httpContext.Request);

        // Маскируем конфиденциальные поля в JSON
        if (!string.IsNullOrWhiteSpace(bodyContent) && 
            contentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
        {
            bodyContent = MaskSensitiveFieldsInJson(bodyContent);
        }

        // Логируем исключение вместе с данными запроса
        _logger.LogError(
            exception,
            "Exception occurred. RawBody: {RawBody}, Method: {Method}, Path: {Path}, ContentType: {ContentType}",
            bodyContent ?? "[No body]",
            method,
            path,
            contentType);

        // Возвращаем стандартный ответ об ошибке
    var problemDetails = new ProblemDetails
    {
        Status = StatusCodes.Status500InternalServerError,
        Title = "Server error"
    };

    httpContext.Response.StatusCode = problemDetails.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

    return true;
}

    private static async Task<string?> ReadRequestBodyAsync(HttpRequest request) 
    {
        if (!request.Body.CanRead)
            return null;

        request.EnableBuffering();

        try
        {
            if (request.Body.CanSeek)
                request.Body.Position = 0;

            using var memoryStream = new MemoryStream();
            await request.Body.CopyToAsync(memoryStream);

            if (memoryStream.Length == 0)
                return null;

            memoryStream.Position = 0;
            using var reader = new StreamReader(memoryStream, Encoding.UTF8);
            var bodyContent = await reader.ReadToEndAsync();

            return string.IsNullOrWhiteSpace(bodyContent) ? null : bodyContent;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (request.Body.CanSeek)
                request.Body.Position = 0;
        }
    }

    /// <summary>
    /// Маскирует конфиденциальные поля в JSON на основе атрибута IgnoreLogger
    /// и стандартных имен конфиденциальных полей
    /// </summary>
    private string MaskSensitiveFieldsInJson(string json)
    {
        try
        {
            // Собираем все имена полей, которые нужно маскировать
            var fieldsToMask = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            // Добавляем стандартные чувствительные поля
            foreach (var field in DefaultSensitiveFields)
            {
                fieldsToMask.Add(field);
            }
            
            // Добавляем поля с атрибутом IgnoreLogger
            var annotatedTypes = from a in AppDomain.CurrentDomain.GetAssemblies()
                               from t in a.GetTypes()
                               where t.IsClass && !t.IsAbstract && t.GetProperties()
                                     .Any(p => p.GetCustomAttribute<IgnoreLoggerAttribute>() != null)
                               select t;

            foreach (var type in annotatedTypes)
            {
                var sensitiveProps = type.GetProperties()
                    .Where(p => p.GetCustomAttribute<IgnoreLoggerAttribute>() != null)
                    .Select(p => p.Name.ToLowerInvariant());
                
                foreach (var prop in sensitiveProps)
                {
                    fieldsToMask.Add(prop);
                }
            }

            // Если нет чувствительных полей, просто возвращаем исходный JSON
            if (fieldsToMask.Count == 0)
                return json;

            // Более надежный вариант - разобрать JSON и заменить поля
            try 
            {
                using var doc = JsonDocument.Parse(json);
                var output = new MemoryStream();
                using var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true });
                
                WriteMaskedElement(writer, doc.RootElement, fieldsToMask);
                
                writer.Flush();
                return Encoding.UTF8.GetString(output.ToArray());
            }
            catch 
            {
                // Если не удалось разобрать JSON, используем регулярные выражения
                var maskedJson = json;
                foreach (var field in fieldsToMask)
                {
                    // Полностью удаляем поля из JSON
                    // Для полей в середине объекта: "field": value,
                    var middlePattern = $@"\s*""{field}""\s*:\s*(\""[^\""]*\""|\[[^\]]*\]|[^,\}}]+)\s*,";
                    maskedJson = Regex.Replace(maskedJson, middlePattern, "", RegexOptions.IgnoreCase);
                    
                    // Для полей в конце объекта: "field": value
                    var endPattern = $@",\s*""{field}""\s*:\s*(\""[^\""]*\""|\[[^\]]*\]|[^,\}}]+)\s*";
                    maskedJson = Regex.Replace(maskedJson, endPattern, "", RegexOptions.IgnoreCase);
                    
                    // Для случая единственного поля в объекте
                    var singlePattern = $@"\{{\s*""{field}""\s*:\s*(\""[^\""]*\""|\[[^\]]*\]|[^,\}}]+)\s*\}}";
                    maskedJson = Regex.Replace(maskedJson, singlePattern, "{}", RegexOptions.IgnoreCase);
                }
                return maskedJson;
            }
        }
        catch
        {
            // В случае ошибки возвращаем исходный JSON
            return json;
        }
    }

    /// <summary>
    /// Рекурсивно записывает элементы JSON, полностью удаляя конфиденциальные поля
    /// </summary>
    private void WriteMaskedElement(Utf8JsonWriter writer, JsonElement element, HashSet<string> sensitiveFields)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    bool isSensitive = IsSensitiveProperty(property.Name, sensitiveFields);
                    
                    // Пропускаем чувствительные свойства полностью
                    if (isSensitive)
                        continue;

                    // Записываем только нечувствительные свойства
                    writer.WritePropertyName(property.Name);
                    WriteMaskedElement(writer, property.Value, sensitiveFields);
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteMaskedElement(writer, item, sensitiveFields);
                }
                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;

            case JsonValueKind.Number:
                writer.WriteNumberValue(element.GetDecimal());
                break;

            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;

            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;

            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
        }
    }
    
    /// <summary>
    /// Проверяет, является ли свойство чувствительным
    /// </summary>
    private bool IsSensitiveProperty(string propertyName, HashSet<string> sensitiveFields)
    {
        // Проверяем, есть ли свойство в списке полей, помеченных атрибутом [IgnoreLogger]
        if (sensitiveFields.Contains(propertyName))
            return true;
            
        // Проверяем, содержит ли имя свойства чувствительное слово
        foreach (var sensitiveWord in DefaultSensitiveFields)
        {
            if (propertyName.Contains(sensitiveWord, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        
        // Проверяем, содержит ли имя свойства слово "file" - это может быть IFormFile
        if (propertyName.Contains("file", StringComparison.OrdinalIgnoreCase))
            return true;
        
        return false;
    }
}