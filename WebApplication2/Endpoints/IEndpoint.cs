using Microsoft.AspNetCore.Routing;

namespace WebApplication2.Endpoints;

public interface IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app);
} 