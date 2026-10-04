using HospitalInformationSystem.Application.Diagnoses.Commands.CreateDiagnosis;
using HospitalInformationSystem.Application.Diagnoses.Commands.UpdateDiagnosis;
using HospitalInformationSystem.Application.Diagnoses.Commands.DeleteDiagnosis;
using HospitalInformationSystem.Application.Diagnoses.Queries.GetDiagnosis;
using HospitalInformationSystem.Application.Diagnoses.Queries.GetDiagnoses;
using HospitalInformationSystem.Contracts.Diagnoses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HospitalInformationSystem.Api.Controllers;

[ApiController]
[Route("api/diagnoses")]
public sealed class DiagnosesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DiagnosisDefinitionDto>>> GetListAsync(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetDiagnosesQuery(), cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DiagnosisDefinitionDto>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetDiagnosisQuery(id), cancellationToken));

    [HttpPost]
    [ProducesResponseType<DiagnosisDefinitionDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<DiagnosisDefinitionDto>> CreateAsync(SaveDiagnosisRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateDiagnosisCommand(request.Code, request.Name), cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DiagnosisDefinitionDto>> UpdateAsync(Guid id, SaveDiagnosisRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new UpdateDiagnosisCommand(id, request.Code, request.Name), cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteDiagnosisCommand(id), cancellationToken);
        return NoContent();
    }
}

