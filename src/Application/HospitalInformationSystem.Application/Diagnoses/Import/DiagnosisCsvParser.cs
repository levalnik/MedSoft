using FluentValidation;
using FluentValidation.Results;
using HospitalInformationSystem.Contracts.Diagnoses;
using Microsoft.VisualBasic.FileIO;

namespace HospitalInformationSystem.Application.Diagnoses.Import;

public static class DiagnosisCsvParser
{
    public static IReadOnlyList<DiagnosisCsvRow> Parse(string csv, CancellationToken cancellationToken = default)
    {
        var physicalLines = csv.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        using var reader = new StringReader(csv.TrimStart('\uFEFF'));
        using var parser = new TextFieldParser(reader)
        {
            TextFieldType = FieldType.Delimited,
            Delimiters = [","],
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = true
        };
        var header = ReadFields(parser);
        if (header is not ["Code", "Name"])
            throw Error(1, "Ожидается заголовок Code,Name и разделитель запятая.");

        var rows = new List<DiagnosisCsvRow>();
        while (!parser.EndOfData)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = parser.LineNumber;
            while (line > 0 && line <= physicalLines.Length && string.IsNullOrWhiteSpace(physicalLines[line - 1]))
                line++;
            var fields = ReadFields(parser);
            if (fields is not { Length: 2 })
                throw Error(line, "Требуются ровно два столбца: Code,Name.");
            if (rows.Count == DiagnosisImportLimits.MaxRows)
                throw Error(line, $"Допускается не более {DiagnosisImportLimits.MaxRows} записей.");
            if (fields.Any(x => x.Contains('\0')))
                throw Error(line, "Нулевой символ недопустим в коде и названии.");
            rows.Add(new DiagnosisCsvRow(line, fields[0].Trim(), fields[1].Trim()));
        }
        if (rows.Count == 0)
            throw Error(1, "Добавьте хотя бы один диагноз после заголовка.");
        return rows;
    }

    private static string[]? ReadFields(TextFieldParser parser)
    {
        try { return parser.ReadFields(); }
        catch (MalformedLineException)
        {
            throw Error(parser.ErrorLineNumber, "Некорректный CSV: проверьте кавычки и разделители.");
        }
    }

    internal static ValidationException Error(long line, string message) =>
        new([new ValidationFailure($"file[{line}]", $"Строка {line}: {message}")]);
}
