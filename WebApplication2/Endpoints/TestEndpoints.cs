using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WebApplication2.Models;

namespace WebApplication2.Endpoints;

public class TestEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {

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

        app.MapPost("/test-real", (RealResponse response) =>
        {
            throw new Exception("Тестовое исключение для проверки модели RealResponse");
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
    }
} 