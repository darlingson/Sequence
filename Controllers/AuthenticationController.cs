using Microsoft.AspNetCore.Mvc;
using Sequence.Contracts.Authentication;
using Sequence.Services.Interfaces;

namespace Sequence.Controllers;

[Route("api/authentication")]
[ApiController]
public class AuthenticationController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;

    public AuthenticationController(IAuthenticationService authenticationService)
        => _authenticationService = authenticationService;

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var profile = await _authenticationService.RegisterAsync(request);
        return Ok(profile);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var tokens = await _authenticationService.LoginAsync(request);
        return Ok(tokens);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        var tokens = await _authenticationService.RefreshAsync(request.RefreshToken);
        return Ok(tokens);
    }
}