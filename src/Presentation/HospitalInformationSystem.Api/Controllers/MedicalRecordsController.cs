using System.Globalization;
using System.Text.RegularExpressions;
using HospitalInformationSystem.Application.MedicalRecords.Commands.AddMedicalRecordDiagnosis;
using HospitalInformationSystem.Application.MedicalRecords.Commands.AddPrescription;
using HospitalInformationSystem.Application.MedicalRecords.Commands.CancelPrescription;
using HospitalInformationSystem.Application.MedicalRecords.Commands.CreateMedicalRecord;
using HospitalInformationSystem.Application.MedicalRecords.Commands.RemoveMedicalRecordDiagnosis;
using HospitalInformationSystem.Application.MedicalRecords.Commands.ReplacePrescription;
using HospitalInformationSystem.Application.MedicalRecords.Queries.GetMedicalRecord;
using HospitalInformationSystem.Application.MedicalRecords.Queries.GetMedicalRecordHistory;
using HospitalInformationSystem.Application.MedicalRecords.Queries.GetMedicalRecordSnapshot;
using HospitalInformationSystem.Application.MedicalRecords.Queries.GetPatientMedicalRecord;
using HospitalInformationSystem.Contracts.MedicalRecords;
using HospitalInformationSystem.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HospitalInformationSystem.Api.Controllers;

[ApiController]
[Route("api/medical-records")]
public sealed partial class MedicalRecordsController(ISender sender) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MedicalRecordDto>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetMedicalRecordQuery(id), cancellationToken));

    [HttpGet("/api/patients/{patientId:guid}/medical-record")]
    public async Task<ActionResult<MedicalRecordDto>> GetForPatientAsync(Guid patientId, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetPatientMedicalRecordQuery(patientId), cancellationToken));

    [HttpPost("/api/patients/{patientId:guid}/medical-record")]
    [ProducesResponseType<MedicalRecordDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<MedicalRecordDto>> CreateAsync(Guid patientId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateMedicalRecordCommand(patientId, RequireAuthorId()), cancellationToken);
        return CreatedAtAction("Get", new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/diagnoses")]
    [ProducesResponseType<MedicalRecordDiagnosisDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<MedicalRecordDiagnosisDto>> AddDiagnosisAsync(
        Guid id, AddMedicalRecordDiagnosisRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new AddMedicalRecordDiagnosisCommand(id, request.DiagnosisId, RequireAuthorId()), cancellationToken);
        return CreatedAtAction("Get", new { id }, result);
    }

    [HttpDelete("{id:guid}/diagnoses/{medicalRecordDiagnosisId:guid}")]
    public async Task<IActionResult> RemoveDiagnosisAsync(Guid id, Guid medicalRecordDiagnosisId, CancellationToken cancellationToken)
    {
        await sender.Send(new RemoveMedicalRecordDiagnosisCommand(id, medicalRecordDiagnosisId, RequireAuthorId()), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/prescriptions")]
    [ProducesResponseType<PrescriptionDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<PrescriptionDto>> AddPrescriptionAsync(
        Guid id, SavePrescriptionRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new AddPrescriptionCommand(id, request.Text, RequireAuthorId()), cancellationToken);
        return CreatedAtAction("Get", new { id }, result);
    }

    [HttpPost("{id:guid}/prescriptions/{prescriptionId:guid}/cancel")]
    public async Task<IActionResult> CancelPrescriptionAsync(Guid id, Guid prescriptionId, CancellationToken cancellationToken)
    {
        await sender.Send(new CancelPrescriptionCommand(id, prescriptionId, RequireAuthorId()), cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/prescriptions/{prescriptionId:guid}")]
    public async Task<ActionResult<PrescriptionDto>> ReplacePrescriptionAsync(
        Guid id, Guid prescriptionId, SavePrescriptionRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new ReplacePrescriptionCommand(id, prescriptionId, request.Text, RequireAuthorId()), cancellationToken));

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<MedicalRecordHistoryEventDto>>> GetHistoryAsync(Guid id, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetMedicalRecordHistoryQuery(id), cancellationToken));

    [HttpGet("{id:guid}/snapshot")]
    public async Task<ActionResult<MedicalRecordSnapshotDto>> GetSnapshotAsync(Guid id, [FromQuery] string at, CancellationToken cancellationToken)
    {
        if (!OffsetTimestamp().IsMatch(at) ||
            !DateTimeOffset.TryParse(at, CultureInfo.InvariantCulture, DateTimeStyles.None, out var instant))
            throw new DomainValidationException("Параметр at должен быть временем ISO 8601 с Z или смещением, например 2026-10-04T12:00:00Z.");
        return Ok(await sender.Send(new GetMedicalRecordSnapshotQuery(id, instant), cancellationToken));
    }

    private Guid RequireAuthorId()
    {
        var values = Request.Headers["X-Medical-Worker-Id"];
        if (values.Count != 1 || !Guid.TryParse(values[0], out var id) || id == Guid.Empty)
            throw new DomainValidationException("Укажите один корректный X-Medical-Worker-Id.");
        return id;
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$", RegexOptions.IgnoreCase)]
    private static partial Regex OffsetTimestamp();
}
