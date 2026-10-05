using HospitalInformationSystem.Application.Diagnoses.Commands.CreateDiagnosis;
using HospitalInformationSystem.Application.Diagnoses.Commands.UpdateDiagnosis;
using HospitalInformationSystem.Application.Diagnoses.Commands.DeleteDiagnosis;
using HospitalInformationSystem.Application.Diagnoses.Queries.GetDiagnosis;
using HospitalInformationSystem.Application.Diagnoses.Queries.GetDiagnoses;
using HospitalInformationSystem.Contracts.Diagnoses;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using HospitalInformationSystem.Application.Diagnoses.Commands.ImportDiagnoses;

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

    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(DiagnosisImportLimits.MaxFileBytes + 64 * 1024)]
    [ProducesResponseType<DiagnosisImportResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DiagnosisImportResultDto>> ImportAsync(
        [FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        if (Request.Form.Files.Count != 1 || file is null)
            return Problem(statusCode: 400, detail: "Передайте ровно один CSV-файл в поле file.");
        if (file.Length == 0)
            return Problem(statusCode: 400, detail: "Выберите непустой CSV-файл в поле file.");
        if (file.Length > DiagnosisImportLimits.MaxFileBytes)
            return Problem(statusCode: 413, detail: "Размер CSV-файла не должен превышать 1 МиБ.");

        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false);
        string csv;
        try { csv = await reader.ReadToEndAsync(cancellationToken); }
        catch (DecoderFallbackException)
        {
            return Problem(statusCode: 400, detail: "Сохраните CSV-файл в кодировке UTF-8.");
        }
        return Ok(await sender.Send(new ImportDiagnosesCommand(csv), cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteDiagnosisCommand(id), cancellationToken);
        return NoContent();
    }
}
