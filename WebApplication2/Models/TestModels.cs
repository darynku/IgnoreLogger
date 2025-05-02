using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using WebApplication2;

namespace WebApplication2.Models;

// Тестовые классы для дополнительных тестов
public class MixedDataTest
{
    public string PublicInfo { get; set; } = "Публичная информация";
    
    [IgnoreLogger]
    public IFormFile SecretNumber { get; set; }
    
    [IgnoreLogger]
    public string[] SecretCodes { get; set; } = new[] { "code1", "code2" };
    
    public List<string> PublicTags { get; set; } = new List<string> { "tag1", "tag2" };
    
    [IgnoreLogger]
    public Dictionary<string, string> SecretDict { get; set; } = new Dictionary<string, string> 
    { 
        { "key1", "value1" }, 
        { "key2", "value2" } 
    };
}

public class SingleFieldTest
{
    [IgnoreLogger]
    public string OnlySecret { get; set; } = "This should be removed completely";
}

// Модель для загрузки файла
public class FileUploadModel
{
    public string? Description { get; set; } = "Описание файла";
    
    [IgnoreLogger] // Игнорируем файл в логах
    public IFormFile? File { get; set; }
    
    public bool IsPublic { get; set; } = true;
    
    // Дополнительное поле, которое должно отображаться в логах
    public string? Category { get; set; } = "Документы";
    
    // Дополнительное чувствительное поле, которое должно игнорироваться 
    [IgnoreLogger]
    public string? SecretData { get; set; } = "Конфиденциальные данные";
}

// Модель для загрузки нескольких файлов
public class MultipleFilesModel
{
    public string? BatchName { get; set; } = "Группа файлов";
    
    [IgnoreLogger] // Игнорируем список файлов в логах
    public IFormFileCollection? Files { get; set; }
    
    public string? Category { get; set; } = "Документы";
} 