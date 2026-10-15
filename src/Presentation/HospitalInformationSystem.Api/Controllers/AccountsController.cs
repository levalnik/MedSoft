using HospitalInformationSystem.Api.Security;
using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalInformationSystem.Api.Controllers;

[ApiController]
[Authorize(Policy = ApiSecurityPolicies.Administrator)]
[Route("api/accounts")]
public sealed class AccountsController(IAccountService accounts) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AccountDto>>> GetAsync(CancellationToken cancellationToken) =>
        Ok(await accounts.ListAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<AccountDto>> CreateAsync(CreateAccountRequest request,
        CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await accounts.CreateAsync(request, cancellationToken));
}
