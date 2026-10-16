using FluentValidation;
using FluentValidation.Results;
using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Application.Common;
using HospitalInformationSystem.Application.Diagnoses.Import;
using HospitalInformationSystem.Contracts.Diagnoses;
using HospitalInformationSystem.Domain.Common;
using HospitalInformationSystem.Domain.Diagnoses;
using HospitalInformationSystem.Domain.Diagnoses.Factories;
using HospitalInformationSystem.Domain.Diagnoses.Services;
using MediatR;

namespace HospitalInformationSystem.Application.Diagnoses.Commands.ImportDiagnoses;

public sealed class ImportDiagnosesCommandHandler(
    IDiagnosisRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider
) : IRequestHandler<ImportDiagnosesCommand, DiagnosisImportResultDto>
{
    public async Task<DiagnosisImportResultDto> Handle(ImportDiagnosesCommand request,
        CancellationToken cancellationToken)
    {
        var rows = DiagnosisCsvParser.Parse(request.Csv, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var candidates = new List<(DiagnosisCsvRow Row, DiagnosisDefinition Diagnosis)>();
        var errors = new List<ValidationFailure>();
        var codes = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!codes.Add(row.Code))
                errors.Add(new ValidationFailure($"file[{row.Line}].Code", $"Строка {row.Line}: повтор кода {row.Code}."));
            if (!names.Add(row.Name))
                errors.Add(new ValidationFailure($"file[{row.Line}].Name", $"Строка {row.Line}: повтор названия {row.Name}."));
            try
            {
                candidates.Add((row, DiagnosisDefinitionFactory.Create(row.Code, row.Name, now)));
            }
            catch (DomainValidationException e)
            {
                errors.Add(new($"file[{row.Line}]", $"Строка {row.Line}: {e.Message}"));
            }
        }

        if (errors.Count > 0) throw new ValidationException(errors);

        var existing = await repository.FindImportMatchesAsync(codes, names, cancellationToken);
        var byCode = existing.ToDictionary(x => x.Code, StringComparer.Ordinal);
        var byName = existing.ToDictionary(x => x.Name, StringComparer.Ordinal);
        foreach (var (row, diagnosis) in candidates)
            if (byName.TryGetValue(diagnosis.Name, out var owner) && owner.Code != diagnosis.Code)
                throw new BusinessConflictException(
                    $"Строка {row.Line}: название «{diagnosis.Name}» уже принадлежит коду {owner.Code}. Импорт отменён.");

        var created = 0;
        var updated = 0;
        var unchanged = 0;
        foreach (var (_, diagnosis) in candidates)
        {
            if (!byCode.TryGetValue(diagnosis.Code, out var current))
            {
                repository.Add(diagnosis);
                created++;
            }
            else if (current.Name == diagnosis.Name)
                unchanged++;
            else
            {
                DiagnosisDefinitionService.Update(current, diagnosis.Code, diagnosis.Name, now);
                await repository.UpdateAsync(current, cancellationToken);
                updated++;
            }
        }

        if (created + updated > 0)
            await unitOfWork.SaveChangesAsync(cancellationToken);
        return new DiagnosisImportResultDto(created, updated, unchanged);
    }
}
