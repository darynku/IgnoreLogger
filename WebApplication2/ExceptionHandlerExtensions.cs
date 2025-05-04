using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace WebApplication2;

internal sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Cyrillic),
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
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
            var request = httpContext.Request;
            string? bodyContent;
            // Читаем тело запроса
            if (request.HasFormContentType)
            {
                try
                {
                    var form = await request.ReadFormAsync(cancellationToken);
                    var formData = new Dictionary<string, object>();
                    
                    foreach (var file in form.Files)
                    {
                        formData[file.Name] = $"[Файл: {file.FileName}, размер: {file.Length} байт]";
                    }
                    
                    foreach (var key in form.Keys)
                    {
                        formData[key] = form[key].ToString();
                    }
                    
                    bodyContent = JsonSerializer.Serialize(formData, JsonOptions);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при чтении формы");
                    bodyContent = $"[Ошибка при чтении формы: {ex.Message}]";
                }
            }
            else
            {
                bodyContent = await ReadRequestBodyAsync(request);
                
                // Если тело запроса - JSON, попробуем отформатировать его для лучшей читаемости
                if (!string.IsNullOrEmpty(bodyContent) && bodyContent.StartsWith("{") && bodyContent.EndsWith("}"))
                {
                    try
                    {
                        var jsonObj = JsonSerializer.Deserialize<object>(bodyContent);
                        bodyContent = JsonSerializer.Serialize(jsonObj, JsonOptions);
                    }
                    catch
                    {
                        // Если не удалось преобразовать JSON, оставляем как есть
                    }
                }
            }

            // Логируем исключение вместе с данными запроса
            _logger.LogError(
                exception,
                "Произошла ошибка. Тело запроса: {RawBody}, Запрос: {Query}",
                bodyContent ?? "[Нет тела]",
                string.IsNullOrEmpty(request.QueryString.Value) ? "[Нет параметров запроса]" : request.QueryString.Value);

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Ошибка сервера"
            };

            httpContext.Response.StatusCode = problemDetails.Status.Value;
            await httpContext.Response.WriteAsJsonAsync(problemDetails, JsonOptions, cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в самом обработчике исключений");
            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await httpContext.Response.WriteAsync("Произошла ошибка сервера", cancellationToken);
            return true;
        }
    }

    private static async Task<string?> ReadRequestBodyAsync(HttpRequest request) 
    {
        try
        {
            request.Body.Position = 0;

            using var memoryStream = new MemoryStream();
            await request.Body.CopyToAsync(memoryStream);

            if (memoryStream.Length == 0)
                return null;

            memoryStream.Position = 0;
            using var reader = new StreamReader(memoryStream, Encoding.UTF8);
            var bodyContent = await reader.ReadToEndAsync();
            
            request.Body.Position = 0;
            
            return bodyContent;
        }
        catch (Exception ex)
        {
            return $"[Ошибка чтения тела запроса: {ex.Message}]";
        }
    }
}