using Microsoft.AspNetCore.Identity;

namespace Sequence.Models;

public class ApplicationRole : IdentityRole
{
    public string? Description { get; set; }
}