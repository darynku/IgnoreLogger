using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace WebApplication2;

internal sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    
    // Поля, которые всегда будут маскироваться, независимо от наличия атрибута
    private static readonly HashSet<string> DefaultSensitiveFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "file", "files" // Оставляем только поля, связанные с файлами
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
        try
        {
    var method = httpContext.Request.Method;
    var path = httpContext.Request.Path;
    var contentType = httpContext.Request.ContentType;

            // Переменная для логирования тела запроса
            string? bodyContent = null;

            // Определяем главный тип содержимого (до первого ';')
            string? mainContentType = contentType?.Split(';').FirstOrDefault()?.Trim().ToLowerInvariant();

            // Обрабатываем по-разному в зависимости от типа контента
            if (mainContentType == "application/json")
            {
                // Для JSON используем исходную логику
                bodyContent = await ReadRequestBodyAsync(httpContext.Request);
                if (!string.IsNullOrWhiteSpace(bodyContent))
                {
                    try
                    {
                        // Проверяем, является ли bodyContent валидным JSON
                        using var doc = JsonDocument.Parse(bodyContent);
                        // Если дошли сюда, значит это валидный JSON
                        bodyContent = MaskSensitiveFieldsInJson(bodyContent);
                    }
                    catch (JsonException)
                    {
                        // Если это не валидный JSON, оставляем как есть
                    }
                }
            }
            else if (mainContentType == "multipart/form-data")
            {
                // Для form-data создаем JSON из формы, исключая файлы
                try
                {
                    // Получаем данные формы
                    var form = await httpContext.Request.ReadFormAsync(cancellationToken);
                    
                    // Собираем не-файловые поля в словарь
                    var formData = new Dictionary<string, object>();
                    
                    // Сначала обработаем обычные поля формы (не файлы)
                    foreach (var key in form.Keys)
                    {
                        // Если поле не является файлом
                        if (!form.Files.Any(f => f.Name == key))
                        {
                            // Проверяем, является ли поле чувствительным
                            if (!IsSensitiveProperty(key, GetSensitiveFieldNames()))
                            {
                                // Добавляем значение поля в JSON
                                formData[key] = form[key].ToString();
                            }
                        }
                    }
                    
                    // Добавляем информацию о файлах (только имена и размеры)
                    var fileInfos = new List<object>();
                    foreach (var file in form.Files)
                    {
                        // Включаем только безопасную информацию о файлах
                        fileInfos.Add(new
                        {
                            FieldName = file.Name,
                            FilePresent = true,
                            Size = file.Length,
                            ContentType = file.ContentType
                        });
                    }
                    
                    if (fileInfos.Any())
                    {
                        formData["_fileInfo"] = fileInfos;
                    }
                    
                    // Серилизуем в JSON
                    bodyContent = JsonSerializer.Serialize(formData, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                }
                catch (Exception ex)
                {
                    bodyContent = $"[Error processing multipart form data: {ex.Message}]";
                }
            }
            else
            {
                // Для других типов контента просто читаем тело запроса
                bodyContent = await ReadRequestBodyAsync(httpContext.Request);
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
        catch (Exception ex)
        {
            // Логируем ошибку в обработчике исключений
            _logger.LogError(ex, "Error in exception handler itself");
            
            // Возвращаем минимальный ответ об ошибке
            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await httpContext.Response.WriteAsync("Server error occurred", cancellationToken);

    return true;
}
    }

    /// <summary>
    /// Получает имена всех полей, помеченных атрибутом IgnoreLogger в приложении
    /// </summary>
    private HashSet<string> GetSensitiveFieldNames()
    {
        var sensitiveFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        // Добавляем поля, связанные с файлами
        sensitiveFields.Add("file");
        sensitiveFields.Add("files");
        
        // Добавляем поля, помеченные атрибутом IgnoreLogger
        var annotatedTypes = from a in AppDomain.CurrentDomain.GetAssemblies()
                             from t in a.GetTypes()
                             where t.IsClass && !t.IsAbstract && t.GetProperties()
                                   .Any(p => p.GetCustomAttribute<IgnoreLoggerAttribute>() != null)
                             select t;

        foreach (var type in annotatedTypes)
        {
            var ignoredProps = type.GetProperties()
                .Where(p => p.GetCustomAttribute<IgnoreLoggerAttribute>() != null)
                .Select(p => p.Name.ToLowerInvariant());
            
            foreach (var prop in ignoredProps)
            {
                sensitiveFields.Add(prop);
            }
        }
        
        return sensitiveFields;
    }

    private static async Task<string?> ReadRequestBodyAsync(HttpRequest request) 
    {
        // Проверяем тип контента (берем основной тип, без параметров)
        string? mainContentType = request.ContentType?.Split(';').FirstOrDefault()?.Trim().ToLowerInvariant();
        
        // Для multipart/form-data не читаем содержимое, чтобы избежать проблем с потоками
        if (mainContentType == "multipart/form-data")
        {
            return "[form-data content - not read for security reasons]";
        }

        if (!request.Body.CanRead)
            return null;

        // Для application/json и других типов нужна буферизация
        if (mainContentType == "application/json" || (mainContentType != null && !mainContentType.Contains("multipart")))
        {
            // Включаем буферизацию для повторного чтения тела
            request.EnableBuffering();
        }

        try
        {
            // Только если поток поддерживает позиционирование и не является form-data
            if (request.Body.CanSeek)
            {
                request.Body.Position = 0;

            using var memoryStream = new MemoryStream();
            await request.Body.CopyToAsync(memoryStream);

            if (memoryStream.Length == 0)
                return null;

            memoryStream.Position = 0;
            using var reader = new StreamReader(memoryStream, Encoding.UTF8);
                var bodyContent = await reader.ReadToEndAsync();
                
                // Сбрасываем позицию для последующих чтений
                request.Body.Position = 0;
                
                return string.IsNullOrWhiteSpace(bodyContent) ? null : bodyContent;
            }
            else
            {
                // Для потоков, которые не поддерживают позиционирование, выводим информационное сообщение
                return "[Request body can't be read - stream doesn't support seeking]";
            }
        }
        catch
        {
            return "[Error reading request body]";
        }
    }

    /// <summary>
    /// Маскирует конфиденциальные поля в JSON на основе атрибута IgnoreLogger
    /// и стандартных имен конфиденциальных полей
    /// </summary>
    private string MaskSensitiveFieldsInJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return json;
            
        try
        {
            // Собираем все имена полей, которые нужно маскировать
            var fieldsToMask = GetSensitiveFieldNames();

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
            catch (JsonException)
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
            
        // Проверяем, есть ли свойство в списке стандартных чувствительных полей
        if (DefaultSensitiveFields.Contains(propertyName))
            return true;
        
        return false;
    }
}