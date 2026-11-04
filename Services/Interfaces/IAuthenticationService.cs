using Sequence.Contracts.Authentication;

namespace Sequence.Services.Interfaces;

public interface IAuthenticationService
{
    Task<UserProfileResponse> RegisterAsync(RegisterRequest request);
    Task<TokenResponse> LoginAsync(LoginRequest request);
    Task<TokenResponse> RefreshAsync(string refreshTokenString);
    Task<UserProfileResponse> GetProfileAsync(string userId);
}