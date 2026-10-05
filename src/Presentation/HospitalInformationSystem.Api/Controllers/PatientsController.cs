using HospitalInformationSystem.Application.Patients.Commands.CreatePatient;
using HospitalInformationSystem.Application.Patients.Commands.UpdatePatient;
using HospitalInformationSystem.Application.Patients.Commands.DeletePatient;
using HospitalInformationSystem.Application.Patients.Queries.GetPatient;
using HospitalInformationSystem.Application.Patients.Queries.GetPatients;
using HospitalInformationSystem.Contracts.Patients;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HospitalInformationSystem.Api.Controllers;

[ApiController]
[Route("api/patients")]
public sealed class PatientsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PatientDto>>> GetListAsync(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetPatientsQuery(), cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PatientDto>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetPatientQuery(id), cancellationToken));

    [HttpPost]
    [ProducesResponseType<PatientDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<PatientDto>> CreateAsync(SavePatientRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreatePatientCommand(request.FirstName, request.LastName, request.MiddleName,
            request.BirthDate, request.Sex, request.Comment), cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PatientDto>> UpdateAsync(Guid id, SavePatientRequest request,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new UpdatePatientCommand(id, request.FirstName, request.LastName, request.MiddleName,
            request.BirthDate, request.Sex, request.Comment), cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeletePatientCommand(id), cancellationToken);
        return NoContent();
    }
}
