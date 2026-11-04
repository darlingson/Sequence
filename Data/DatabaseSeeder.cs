using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sequence.Authorization;
using Sequence.Models;

namespace Sequence.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db          = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await db.Database.MigrateAsync();

        var existingPerms = await db.Permissions.Select(p => p.Name).ToListAsync();
        foreach (var name in Permissions.All().Except(existingPerms))
            db.Permissions.Add(new Permission { Name = name, Description = name });
        await db.SaveChangesAsync();

        await EnsureRoleAsync(roleManager, "Admin",   "Full access");
        await EnsureRoleAsync(roleManager, "Manager", "Read/write access");
        await EnsureRoleAsync(roleManager, "User",    "Read-only access");

        var permsByName = await db.Permissions.ToDictionaryAsync(p => p.Name);
        var admin   = await roleManager.FindByNameAsync("Admin");
        var manager = await roleManager.FindByNameAsync("Manager");
        var user    = await roleManager.FindByNameAsync("User");

        await GrantAsync(db, admin!.Id, permsByName.Keys, permsByName);

        await GrantAsync(db, manager!.Id, new[]
        {
            Permissions.Products.Read, Permissions.Products.Write,
            Permissions.Users.Read,    Permissions.Roles.Read
        }, permsByName);

        await GrantAsync(db, user!.Id, new[]
        {
            Permissions.Products.Read
        }, permsByName);

        const string adminEmail    = "admin@sequence.local";
        const string adminPassword = "Admin!123";

        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var adminUser = new ApplicationUser
            {
                UserName       = adminEmail,
                Email          = adminEmail,
                FirstName      = "System",
                LastName       = "Admin",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(adminUser, adminPassword);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join("; ",
                    result.Errors.Select(e => e.Description)));

            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }

    private static async Task EnsureRoleAsync(RoleManager<ApplicationRole> roleManager, string name, string description)
    {
        if (await roleManager.RoleExistsAsync(name)) return;
        await roleManager.CreateAsync(new ApplicationRole { Name = name, Description = description });
    }

    private static async Task GrantAsync(
        AppDbContext db,
        string roleId,
        IEnumerable<string> permissionNames,
        Dictionary<string, Permission> permsByName)
    {
        var already = await db.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.PermissionId)
            .ToListAsync();

        foreach (var name in permissionNames)
        {
            if (!permsByName.TryGetValue(name, out var perm)) continue;
            if (already.Contains(perm.Id)) continue;

            db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = perm.Id });
        }

        await db.SaveChangesAsync();
    }
}