using Auth.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace Auth.Api.Data;

/// <summary>
/// Ідемпотентний сідер базових ролей (User, Admin) та тестового адміністратора при старті Auth Service.
/// </summary>
public static class IdentityDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services, ILogger? logger = null)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        string[] roles = ["User", "Admin"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                logger?.LogInformation("Created Identity role '{Role}'", role);
            }
        }

        const string adminEmail = "admin@studio.ua";
        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = "admin",
                Email = adminEmail,
                FullName = "Platform Administrator",
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await userManager.CreateAsync(admin, "Admin123!@#");
            if (createResult.Succeeded)
            {
                await userManager.AddToRolesAsync(admin, ["Admin", "User"]);
                logger?.LogInformation("Seeded default Admin user '{Email}'", adminEmail);
            }
        }
    }
}
