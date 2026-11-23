using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Platform.IdentityServer.Data;

namespace Platform.IdentityServer;

/// <summary>
/// Ідемпотентний сідінг трьох баз даних IdentityServer (ConfigurationDb, PersistedGrantDb, ApplicationDb).
/// </summary>
public static class SeedData
{
    public static async Task EnsureSeedDataAsync(IServiceProvider serviceProvider, ILogger? logger = null)
    {
        var persistedGrantDb = serviceProvider.GetRequiredService<PersistedGrantDbContext>();
        var configurationDb = serviceProvider.GetRequiredService<ConfigurationDbContext>();
        var applicationDb = serviceProvider.GetRequiredService<ApplicationDbContext>();

        await persistedGrantDb.Database.MigrateAsync();
        await configurationDb.Database.MigrateAsync();
        await applicationDb.Database.MigrateAsync();

        if (!await configurationDb.Clients.AnyAsync())
        {
            foreach (var client in Config.Clients)
            {
                configurationDb.Clients.Add(client.ToEntity());
            }
            await configurationDb.SaveChangesAsync();
            logger?.LogInformation("Seeded IdentityServer Clients (postman, aggregator_service, swagger)");
        }

        if (!await configurationDb.IdentityResources.AnyAsync())
        {
            foreach (var resource in Config.IdentityResources)
            {
                configurationDb.IdentityResources.Add(resource.ToEntity());
            }
            await configurationDb.SaveChangesAsync();
        }

        if (!await configurationDb.ApiScopes.AnyAsync())
        {
            foreach (var scope in Config.ApiScopes)
            {
                configurationDb.ApiScopes.Add(scope.ToEntity());
            }
            await configurationDb.SaveChangesAsync();
        }

        if (!await configurationDb.ApiResources.AnyAsync())
        {
            foreach (var resource in Config.ApiResources)
            {
                configurationDb.ApiResources.Add(resource.ToEntity());
            }
            await configurationDb.SaveChangesAsync();
        }

        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in new[] { "Admin", "Manager", "User" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        if (await userManager.FindByNameAsync("alice") is null)
        {
            var alice = new ApplicationUser
            {
                UserName = "alice",
                Email = "alice@studio.ua",
                FullName = "Alice Admin",
                EmailConfirmed = true
            };
            var res = await userManager.CreateAsync(alice, "Pass123$");
            if (res.Succeeded)
            {
                await userManager.AddToRoleAsync(alice, "Admin");
            }
        }

        if (await userManager.FindByNameAsync("bob") is null)
        {
            var bob = new ApplicationUser
            {
                UserName = "bob",
                Email = "bob@studio.ua",
                FullName = "Bob User",
                EmailConfirmed = true
            };
            var res = await userManager.CreateAsync(bob, "Pass123$");
            if (res.Succeeded)
            {
                await userManager.AddToRoleAsync(bob, "User");
            }
        }
    }
}
