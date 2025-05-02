using WebApplication2;

namespace WebApplication2.Models;

public class RealResponse
{
    [IgnoreLogger]
    public string? Name { get; set; }
    public string? Email { get; set; }
} 