namespace Sequence.Contracts.Authentication;

public record LoginRequest(string Email, string Password)
{
    public string Email { get; set; } = Email;
    public string Password { get; set; } = Password;
}