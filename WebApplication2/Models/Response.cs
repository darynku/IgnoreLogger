namespace WebApplication2.Models;

public class Response
{
    public Guid Id { get; set; }
    public IFormFile File { get; set; }
    public string Name { get; set; }
    public IReadOnlyList<string> Tags { get; set; }
}