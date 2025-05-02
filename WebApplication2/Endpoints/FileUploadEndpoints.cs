using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WebApplication2.Models;

namespace WebApplication2.Endpoints;

public class FileUploadEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/upload-file", async ([FromForm] TestDto test, CancellationToken cancellationToken) =>
        {
            throw new Exception();
        })
        .WithOpenApi();

        // Эндпоинт для тестирования с множеством файлов
        app.MapPost("/upload-multiple", ([FromForm] UserProfile test, CancellationToken cancellationToken) =>
        {
            throw new Exception();
        })
        .WithOpenApi();
    }
} 