using System.Reflection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Serilog;
using WebApplication2;
using WebApplication2.Endpoints;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.Debug()
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog(logger);

builder.Services.AddEndpoints(Assembly.GetExecutingAssembly());

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

app.UseExceptionHandler();

// Включаем буферизацию для запросов с JSON
app.Use(async (context, next) =>
{
    // Определяем главный тип содержимого (до первого ';')
    var contentType = context.Request.ContentType;
    string? mainContentType = contentType?.Split(';').FirstOrDefault()?.Trim().ToLowerInvariant();
    
    var bufferableTypes = new[] { "application/json", "application/xml", "text/plain", "application/x-www-form-urlencoded" };
    if (mainContentType != null && bufferableTypes.Contains(mainContentType))
    {
        context.Request.EnableBuffering();
    }

    
    await next(context);
});

app.MapEndpoints();

app.Run();