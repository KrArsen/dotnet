using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Platform.IdentityServer.Data;

namespace Platform.IdentityServer.Controllers;

/// <summary>
/// Quickstart UI Account Controller для Authorization Code Flow (сторінки Login, Logout, Consent).
/// </summary>
public sealed class AccountController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IIdentityServerInteractionService interaction) : Controller
{
    [HttpGet("/Account/Login")]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        var context = await interaction.GetAuthorizationContextAsync(returnUrl);
        ViewData["ReturnUrl"] = returnUrl ?? "/";
        ViewData["ClientName"] = context?.Client.ClientName ?? "CRM Platform";
        return View();
    }

    [HttpPost("/Account/Login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string username, string password, string? returnUrl = null, bool rememberLogin = false)
    {
        var user = await userManager.FindByNameAsync(username) ?? await userManager.FindByEmailAsync(username);
        if (user is not null)
        {
            var result = await signInManager.PasswordSignInAsync(user, password, rememberLogin, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                if (!string.IsNullOrWhiteSpace(returnUrl) &&
                    (interaction.IsValidReturnUrl(returnUrl) || Url.IsLocalUrl(returnUrl)))
                {
                    return Redirect(returnUrl);
                }

                return Redirect("/");
            }
        }

        ViewData["Error"] = "Invalid username or password.";
        ViewData["ReturnUrl"] = returnUrl ?? "/";
        return View();
    }

    [HttpGet("/Account/Logout")]
    [HttpPost("/Account/Logout")]
    public async Task<IActionResult> Logout(string? logoutId = null)
    {
        var logoutContext = await interaction.GetLogoutContextAsync(logoutId);
        await signInManager.SignOutAsync();

        if (!string.IsNullOrWhiteSpace(logoutContext?.PostLogoutRedirectUri))
        {
            return Redirect(logoutContext.PostLogoutRedirectUri);
        }

        return View("LoggedOut");
    }

    [HttpGet("/Home/Error")]
    public async Task<IActionResult> Error(string? errorId = null)
    {
        var message = await interaction.GetErrorContextAsync(errorId);
        return Problem(title: message?.Error ?? "IdentityServer Error", detail: message?.ErrorDescription);
    }
}
