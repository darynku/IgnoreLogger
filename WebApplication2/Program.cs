using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using WebApplication2;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Настраиваем лимиты для загрузки файлов
builder.Services.Configure<FormOptions>(options =>
{
    // Увеличиваем лимиты для демонстрации
    options.MultipartBodyLengthLimit = 100 * 1024 * 1024; // 100 MB
    options.ValueLengthLimit = 100 * 1024 * 1024; // 100 MB
    options.MultipartHeadersLengthLimit = 100 * 1024 * 1024; // 100 MB
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Настраиваем Serilog с нашей пользовательской политикой деструктуризации
var logger = new LoggerConfiguration()
    .Destructure.With<IgnoreLoggerDestructuringPolicy>()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog(logger);

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Включаем обработку исключений
app.UseExceptionHandler();

// Включаем буферизацию для всех запросов
app.Use(async (context, next) =>
{
    context.Request.EnableBuffering();
    await next(context);
});

// Регистрируем маршруты для тестирования
app.MapPost("/test-attribute", (TestDto test) =>
{
    throw new Exception("Тестовое исключение для проверки игнорирования свойств через атрибут");
})
.WithName("TestAttribute")
.WithOpenApi();

app.MapPost("/test-default", (UserCredentials creds) =>
{
    throw new Exception("Тестовое исключение для проверки игнорирования стандартных чувствительных полей");
})
.WithName("TestDefault")
.WithOpenApi();

app.MapPost("/test-complex", (UserProfile profile) =>
{
    throw new Exception("Тестовое исключение для проверки игнорирования вложенных объектов");
})
.WithName("TestComplex")
.WithOpenApi();

app.MapPost("/test", (RealResponse response) =>
{
    throw new Exception("Тестовое исключение для проверки полного удаления полей");
})
.WithName("TestReal")
.WithOpenApi();

// Дополнительный тест для проверки работы с различными типами JSON-значений
app.MapPost("/test-mixed", (MixedDataTest data) =>
{
    throw new Exception("Тест удаления полей с разными типами данных");
})
.WithName("TestMixed")
.WithOpenApi();

// Тест для проверки удаления единственного поля
app.MapPost("/test-single", (SingleFieldTest data) =>
{
    throw new Exception("Тест удаления единственного поля");
})
.WithName("TestSingle")
.WithOpenApi();

// Эндпоинт для тестирования с загрузкой файлов
app.MapPost("/upload-file", async (FileUploadModel model) =>
{
    if (model.File != null)
    {
        // Имитация обработки файла
        using var stream = new MemoryStream();
        await model.File.CopyToAsync(stream);
        var fileSize = stream.Length;
        
        // Вызываем исключение для тестирования логгирования
        throw new Exception($"Тестовое исключение при загрузке файла. Размер файла: {fileSize} байт");
    }
    throw new Exception("Файл не был предоставлен");
})
.WithName("UploadFile")
.WithOpenApi();

// Эндпоинт для тестирования с множеством файлов
app.MapPost("/upload-multiple", async ([FromForm] MultipleFilesModel model) =>
{
    if (model.Files != null && model.Files.Any())
    {
        // Имитация обработки нескольких файлов
        long totalSize = 0;
        foreach (var file in model.Files)
        {
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            totalSize += stream.Length;
        }
        
        // Вызываем исключение для тестирования логгирования
        throw new Exception($"Тестовое исключение при загрузке нескольких файлов. Общий размер: {totalSize} байт");
    }
    throw new Exception("Файлы не были предоставлены");
})
.WithName("UploadMultiple")
.WithOpenApi();

app.Run();

// Тестовые классы
public class TestDto
{
    [IgnoreLogger]
    public string Password { get; set; } = "чувствительные_данные";
    
    public string Name { get; set; } = "Имя пользователя";
}

// Класс без атрибутов - должен автоматически скрыть поля типа password, key и т.д.
public class UserCredentials
{
    public string Username { get; set; } = "test_user";
    public string Password { get; set; } = "секретный_пароль";
    public string ApiKey { get; set; } = "api_key_123456";
    public string DisplayName { get; set; } = "Тестовый пользователь";
}

// Класс со вложенными объектами
public class UserProfile
{
    public int Id { get; set; } = 123;
    public string Name { get; set; } = "Тестовый пользователь";
    public string Email { get; set; } = "user@example.com";
    
    [IgnoreLogger]
    public UserCredentials Credentials { get; set; } = new UserCredentials();
    
    public UserSettings Settings { get; set; } = new UserSettings
    {
        Theme = "dark",
        Language = "ru",
        NotificationsEnabled = true,
        SecretToken = "notify_token_12345" // Это поле должно быть замаскировано
    };
}

public class UserSettings
{
    public string Theme { get; set; }
    public string Language { get; set; }
    public bool NotificationsEnabled { get; set; }
    public string SecretToken { get; set; }
}

public class RealResponse
{
    [IgnoreLogger]
    public string Name { get; set; }
    public string Email { get; set; }
}

// Тестовые классы для дополнительных тестов
public class MixedDataTest
{
    public string PublicInfo { get; set; } = "Публичная информация";
    
    [IgnoreLogger]
    public IFormFile SecretNumber { get; set; }
    
    [IgnoreLogger]
    public string[] SecretCodes { get; set; } = new[] { "code1", "code2" };
    
    public List<string> PublicTags { get; set; } = new List<string> { "tag1", "tag2" };
    
    [IgnoreLogger]
    public Dictionary<string, string> SecretDict { get; set; } = new Dictionary<string, string> 
    { 
        { "key1", "value1" }, 
        { "key2", "value2" } 
    };
}

public class SingleFieldTest
{
    [IgnoreLogger]
    public string OnlySecret { get; set; } = "This should be removed completely";
}

// Модель для загрузки файла
public class FileUploadModel
{
    public string Description { get; set; } = "Описание файла";
    
    [IgnoreLogger] // Игнорируем файл в логах
    public IFormFile File { get; set; }
    
    public bool IsPublic { get; set; } = true;
}

// Модель для загрузки нескольких файлов
public class MultipleFilesModel
{
    public string BatchName { get; set; } = "Группа файлов";
    
    [IgnoreLogger] // Игнорируем список файлов в логах
    public IFormFileCollection Files { get; set; }
    
    public string Category { get; set; } = "Документы";
}