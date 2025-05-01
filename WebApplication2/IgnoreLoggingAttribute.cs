using Microsoft.AspNetCore.Mvc.Filters;

namespace WebApplication2;

[AttributeUsage(AttributeTargets.Property)]
public class IgnoreLoggerAttribute : Attribute
{
}