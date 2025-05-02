using System.Reflection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Serilog;
using WebApplication2;
using WebApplication2.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Увеличиваем максимальный размер HTTP запроса
builder.WebHost.ConfigureKestrel(options => 
{
    options.Limits.MaxRequestBodySize = 100 * 1024 * 1024; // 100 MB
});

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

// Регистрируем наши эндпоинты
builder.Services.AddEndpoints(Assembly.GetExecutingAssembly());

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Включаем обработку исключений
app.UseExceptionHandler();

// Включаем буферизацию для запросов с JSON
app.Use(async (context, next) =>
{
    // Определяем главный тип содержимого (до первого ';')
    var contentType = context.Request.ContentType;
    string? mainContentType = contentType?.Split(';').FirstOrDefault()?.Trim().ToLowerInvariant();
    
    // Для application/json включаем буферизацию
    if (mainContentType == "application/json")
    {
        context.Request.EnableBuffering();
    }
    
    await next(context);
});

// Используем наши эндпоинты
app.MapEndpoints();

app.Run();