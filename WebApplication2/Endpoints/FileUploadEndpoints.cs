using Microsoft.AspNetCore.Mvc;
using WebApplication2.Models;

namespace WebApplication2.Endpoints;

public class FileUploadEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/upload-file", ([FromForm] Response formResponse, CancellationToken cancellationToken) =>
        {
            throw new Exception("Мультформдата");
        })
        .WithOpenApi();
        
        app.MapPost("/upload-json", ([FromBody] BodyResponse bodyResponse, CancellationToken cancellationToken) =>
        {
            throw new Exception("Джсончик");
        })
        .WithOpenApi();
    }
} 