using System.Reflection;
using Serilog.Core;
using Serilog.Events;

namespace WebApplication2;

/// <summary>
/// Политика деструктуризации для Serilog, которая игнорирует поля, помеченные атрибутом IgnoreLogger
/// и стандартные чувствительные поля
/// </summary>
public class IgnoreLoggerDestructuringPolicy : IDestructuringPolicy
{
    // Кеш полей, которые нужно игнорировать для каждого типа
    private static readonly Dictionary<Type, HashSet<string>> _ignoredPropertiesCache = new();
    
    // Поля, которые всегда будут игнорироваться, независимо от наличия атрибута
    private static readonly HashSet<string> DefaultSensitiveFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "pwd", "secret", "key", "token", "apikey", "api_key", 
        "accesstoken", "access_token", "refreshtoken", "refresh_token",
        "credential", "pin", "passcode", "pass", "privatekey", "private_key",
        "file", "files", "attachment", "upload", "document", "binary", "content", "stream"
    };

    public bool TryDestructure(object? value, ILogEventPropertyValueFactory propertyValueFactory, out LogEventPropertyValue result)
    {
        result = null!;
        
        // Не обрабатываем null и примитивные типы
        if (value == null || 
            value.GetType().IsPrimitive || 
            value is string || 
            value is DateTime || 
            value is DateTimeOffset || 
            value is TimeSpan || 
            value is decimal)
        {
            return false;
        }
            
        var type = value.GetType();
        var logProps = new List<LogEventProperty>();
        
        // Получаем свойства, которые нужно игнорировать для данного типа
        var ignoredProperties = GetIgnoredProperties(type);
        
        // Получаем публичные свойства объекта
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && !ShouldIgnoreProperty(p.Name, ignoredProperties));
            
        // Перебираем свойства и добавляем их в лог, если они не игнорируемые
        foreach (var prop in properties)
        {  
            try
            {
                var propValue = prop.GetValue(value);
                var logEventValue = propertyValueFactory.CreatePropertyValue(propValue, destructureObjects: true);
                logProps.Add(new LogEventProperty(prop.Name, logEventValue));
            }
            catch
            {
                // Игнорируем ошибки чтения свойств
                continue;
            }
        }
        
        // Создаем структурированное значение
        result = new StructureValue(logProps, type.Name);
        return true;
    }
    
    /// <summary>
    /// Получает список свойств, которые нужно игнорировать для данного типа
    /// </summary>
    private static HashSet<string> GetIgnoredProperties(Type type)
    {
        // Если уже есть в кеше, возвращаем из кеша
        if (_ignoredPropertiesCache.TryGetValue(type, out var cachedResult))
            return cachedResult;
            
        // Иначе ищем свойства с атрибутом IgnoreLogger
        var ignoredProps = new HashSet<string>();
        
        var propsWithAttribute = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<IgnoreLoggerAttribute>() != null)
            .Select(p => p.Name);
            
        foreach (var prop in propsWithAttribute)
            ignoredProps.Add(prop);
            
        // Сохраняем в кеш для будущих вызовов
        _ignoredPropertiesCache[type] = ignoredProps;
        
        return ignoredProps;
    }
    
    /// <summary>
    /// Проверяет, нужно ли игнорировать свойство
    /// </summary>
    private static bool ShouldIgnoreProperty(string propertyName, HashSet<string> classIgnoredProperties)
    {
        // Проверяем, есть ли свойство в списке игнорируемых для конкретного класса
        if (classIgnoredProperties.Contains(propertyName))
            return true;
            
        // Проверяем, содержит ли имя свойства чувствительное слово
        foreach (var sensitiveWord in DefaultSensitiveFields)
        {
            if (propertyName.Contains(sensitiveWord, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        
        // Проверяем, содержит ли имя свойства слово "file" - это может быть IFormFile
        if (propertyName.Contains("file", StringComparison.OrdinalIgnoreCase))
            return true;
        
        return false;
    }
}