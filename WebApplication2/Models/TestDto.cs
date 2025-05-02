using System.Runtime.Serialization;
using Microsoft.AspNetCore.Http;

namespace WebApplication2.Models;

public class TestDto
{
    [IgnoreLogger]
    public IFormFile? File { get; set; } //Большой текстовый файл, чтобы игнорился в логах
    
    public string Name { get; set; } 
} 