using Microsoft.AspNetCore.Http;
using WebApplication2;

namespace WebApplication2.Models;

// Класс без атрибутов - должен автоматически скрыть поля типа password, key и т.д.
public class UserCredentials
{
    public string Username { get; set; } = "test_user";
    public string Password { get; set; } = "секретный_пароль";
    public string ApiKey { get; set; } = "api_key_123456";
    public string DisplayName { get; set; } = "Тестовый пользователь";
}

// Класс со вложенными объектами
public class UserProfile
{
    public int Id { get; set; } = 123;
    public string Name { get; set; } = "Тестовый пользователь";
    public string Email { get; set; } = "user@example.com";
    
    public IFormFile Document { get; set; }
    [IgnoreLogger]
    public UserCredentials Credentials { get; set; } = new UserCredentials();
    
    public UserSettings Settings { get; set; } = new UserSettings
    {
        Theme = "dark",
        Language = "ru",
        NotificationsEnabled = true,
        SecretToken = "notify_token_12345" 
    };
}

public class UserSettings
{
    public string Theme { get; set; }
    public string Language { get; set; }
    public bool NotificationsEnabled { get; set; }
    public string SecretToken { get; set; }
} 