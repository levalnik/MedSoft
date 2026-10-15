using System.Security.Claims;
using HospitalInformationSystem.Api.Security;
using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Contracts.Auth;
using HospitalInformationSystem.Infrastructure.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HospitalInformationSystem.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    UserManager<HospitalUser> userManager,
    SignInManager<HospitalUser> signInManager,
    IAccountService accounts,
    FailedLoginPasswordVerifier failedLoginVerifier) : ControllerBase
{
    [HttpGet("csrf")]
    [AllowAnonymous]
    public ActionResult<AntiforgeryTokenDto> GetAntiforgeryToken(IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new AntiforgeryTokenDto(tokens.RequestToken!));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(ApiSecurityPolicies.LoginRateLimit)]
    public async Task<ActionResult<SessionDto>> LoginAsync(LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || request.UserName.Length > 128 ||
            string.IsNullOrEmpty(request.Password) || request.Password.Length > 128)
            return InvalidCredentials();

        var user = await userManager.FindByNameAsync(request.UserName.Trim());
        if (user is null)
        {
            failedLoginVerifier.Verify(request.Password);
            return InvalidCredentials();
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
            return InvalidCredentials();

        var session = await accounts.GetSessionAsync(user.Id, cancellationToken);
        if (session is null)
            return InvalidCredentials();

        await signInManager.SignInAsync(user, isPersistent: false);
        return Ok(session);
    }

    [HttpGet("session")]
    [Authorize]
    public async Task<ActionResult<SessionDto>> GetSessionAsync(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            return Unauthorized();
        var session = await accounts.GetSessionAsync(id, cancellationToken);
        if (session is not null)
            return Ok(session);
        await signInManager.SignOutAsync();
        return Unauthorized();
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> LogoutAsync()
    {
        await signInManager.SignOutAsync();
        return NoContent();
    }

    private ObjectResult InvalidCredentials() => Problem(statusCode: StatusCodes.Status401Unauthorized,
        title: "Не удалось войти.", detail: "Проверьте имя пользователя и пароль или повторите попытку позднее.");
}
