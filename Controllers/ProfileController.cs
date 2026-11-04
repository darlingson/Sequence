using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sequence.Services.Interfaces;

namespace Sequence.Controllers;

[Authorize]
[Route("api/profile")]
[ApiController]
public class ProfileController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;

    public ProfileController(IAuthenticationService authenticationService)
        => _authenticationService = authenticationService;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userId = User.FindFirst("sub")?.Value
                     ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var profile = await _authenticationService.GetProfileAsync(userId);
        return Ok(profile);
    }
}