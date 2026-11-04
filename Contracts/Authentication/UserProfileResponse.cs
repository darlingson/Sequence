namespace Sequence.Contracts.Authentication;

public record UserProfileResponse(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    IEnumerable<string> Roles,
    IEnumerable<string> Permissions);