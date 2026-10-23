using HospitalInformationSystem.Application.MedicalWorkers.Commands.CreateMedicalWorker;
using HospitalInformationSystem.Application.MedicalWorkers.Commands.DeleteMedicalWorker;
using HospitalInformationSystem.Application.MedicalWorkers.Commands.UpdateMedicalWorker;
using HospitalInformationSystem.Application.MedicalWorkers.Queries.GetMedicalWorker;
using HospitalInformationSystem.Application.MedicalWorkers.Queries.GetMedicalWorkers;
using HospitalInformationSystem.Contracts.MedicalWorkers;
using HospitalInformationSystem.Api.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalInformationSystem.Api.Controllers;

[ApiController]
[Authorize(Policy = ApiSecurityPolicies.CatalogRead)]
[Route("api/medical-workers")]
public sealed class MedicalWorkersController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MedicalWorkerDto>>> GetListAsync(
        CancellationToken cancellationToken, [FromQuery] bool includeDeleted = false) =>
        Ok(await sender.Send(new GetMedicalWorkersQuery(includeDeleted), cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MedicalWorkerDto>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetMedicalWorkerQuery(id), cancellationToken));

    [HttpPost]
    [Authorize(Policy = ApiSecurityPolicies.Administrator)]
    [ProducesResponseType<MedicalWorkerDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public async Task<ActionResult<MedicalWorkerDto>> CreateAsync(SaveMedicalWorkerRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Role is not null) return MedicalRoleUnavailable();
        var worker =
            await sender.Send(new CreateMedicalWorkerCommand(request.FirstName, request.LastName, request.Position),
                cancellationToken);
        return CreatedAtAction("Get", new { id = worker.Id }, worker);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = ApiSecurityPolicies.Administrator)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public async Task<ActionResult<MedicalWorkerDto>> UpdateAsync(Guid id, SaveMedicalWorkerRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Role is not null) return MedicalRoleUnavailable();
        return Ok(await sender.Send(
            new UpdateMedicalWorkerCommand(id, request.FirstName, request.LastName, request.Position), cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = ApiSecurityPolicies.Administrator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteMedicalWorkerCommand(id), cancellationToken);
        return NoContent();
    }

    private ObjectResult MedicalRoleUnavailable() => Problem(statusCode: StatusCodes.Status501NotImplemented,
        title: "Настройка медицинской роли пока недоступна.",
        detail: "Изменения не сохранены. Сервер пока не поддерживает медицинские роли.");
}
