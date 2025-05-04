using System.Runtime.Serialization;
using Microsoft.AspNetCore.Http;

namespace WebApplication2.Models;

public class FormResponse
{
    public IFormFile? Document { get; set; }
    public string Name { get; set; } 
    public string? Class { get; set; }
    public string? Type{ get; set; }
} 