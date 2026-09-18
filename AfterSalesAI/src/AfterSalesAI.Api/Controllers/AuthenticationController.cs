using AfterSalesAI.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AfterSalesAI.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthenticationController(DemoAuthenticationService authentication) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await authentication.AuthenticateAsync(request.UserName, request.Password, cancellationToken);
        if (user is null) return Unauthorized(new { message = "Invalid username or password." });
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, DemoAuthenticationService.Principal(user));
        return Ok(new { user.UserName, user.DisplayName, user.TenantId, user.TenantName, user.ProductName });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me() => Ok(new
    {
        userName = User.Identity?.Name,
        displayName = User.FindFirst(System.Security.Claims.ClaimTypes.GivenName)?.Value,
        tenantId = User.FindFirst("tenant_id")?.Value,
        tenantName = User.FindFirst("tenant_name")?.Value,
        productName = User.FindFirst("product_name")?.Value
    });
}

public sealed record LoginRequest(string UserName, string Password);
