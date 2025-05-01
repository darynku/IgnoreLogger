using Destructurama.Attributed;

namespace WebApplication2;

public class User
{
    public string Username { get; set; } = "Administrator";

    [IgnoreLogger]
    public string Password { get; set; } = "123456";

    public string Role { get; set; } = "Administrator";
}