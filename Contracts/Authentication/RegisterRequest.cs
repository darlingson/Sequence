namespace Sequence.Contracts.Authentication;

public record RegisterRequest(string Email, string Password, string FirstName, string LastName)
{
    public string Email { get; set; } = Email;
    public string Password { get; set; } = Password;
    public string FirstName { get; set; } = FirstName;
    public string LastName { get; set; } = LastName;
}