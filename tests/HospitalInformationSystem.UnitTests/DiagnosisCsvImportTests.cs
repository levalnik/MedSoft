using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using HospitalInformationSystem.Application.Abstractions;
using HospitalInformationSystem.Application.Diagnoses.Commands.ImportDiagnoses;
using HospitalInformationSystem.Application.Diagnoses.Import;
using HospitalInformationSystem.Contracts.Diagnoses;
using Moq;
using Xunit;

namespace HospitalInformationSystem.UnitTests;

public sealed class DiagnosisCsvImportTests
{
    [Fact]
    public void Parser_handles_bom_crlf_commas_escaped_quotes_and_multiline_fields()
    {
        var rows = DiagnosisCsvParser.Parse("\uFEFFCode,Name\r\n A00.00 , \"Боль, \"\"острая\"\"\"\r\nB00.00,\"Первая\r\nвторая\"\r\n");
        rows.Should().HaveCount(2);
        rows[0].Should().Be(new DiagnosisCsvRow(2, "A00.00", "Боль, \"острая\""));
        rows[1].Name.Should().Be("Первая\r\nвторая");
        rows[1].Line.Should().Be(3);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Code,Name\nA00.00,Боль\0")]
    [InlineData("Code,Name")]
    [InlineData("Name,Code\nБоль,A00.00")]
    [InlineData("Code;Name\nA00.00;Боль")]
    [InlineData("Code,Name,Extra\nA00.00,Боль,extra")]
    [InlineData("Code,Name\nA00.00")]
    [InlineData("Code,Name\nA00.00,Боль,extra")]
    [InlineData("Code,Name\nA00.00,\"Открытые кавычки")]
    public void Invalid_csv_is_rejected(string csv)
    {
        var action = () => DiagnosisCsvParser.Parse(csv);
        action.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Row_limit_is_enforced()
    {
        var action = () => DiagnosisCsvParser.Parse("Code,Name\n" + string.Concat(
            Enumerable.Repeat("A00.00,Боль\n", DiagnosisImportLimits.MaxRows + 1)));
        action.Should().Throw<ValidationException>().WithMessage("*10000*");
    }

    [Fact]
    public void Blank_lines_preserve_physical_line_numbers()
    {
        var rows = DiagnosisCsvParser.Parse("Code,Name\n\n \t\nA00.00,Первый\n\nB00.00,Второй\n");
        rows.Select(x => x.Line).Should().Equal(4L, 6L);
    }

    [Theory]
    [InlineData("A00.00,Боль\n A00.00 ,Другое", "повтор кода")]
    [InlineData("A00.00,Боль\nB00.00, Боль ", "повтор названия")]
    [InlineData("A00.00,Боль\nSHORT,Другое", "ровно 6")]
    [InlineData("A00.00,Боль\nB00.00,Аб", "от 3 до 24")]
    public async Task Invalid_rows_do_not_reach_repository(string data, string expected)
    {
        var repository = new Mock<IDiagnosisRepository>(MockBehavior.Strict);
        var unitOfWork = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var handler = new ImportDiagnosesCommandHandler(repository.Object, unitOfWork.Object, TimeProvider.System);
        var action = () => handler.Handle(new ImportDiagnosesCommand("Code,Name\n" + data), default);

        var error = await action.Should().ThrowAsync<ValidationException>();
        error.Which.Errors.Should().Contain(x => x.ErrorMessage.Contains(expected) && x.ErrorMessage.Contains("Строка 3"));
        repository.VerifyNoOtherCalls();
        unitOfWork.VerifyNoOtherCalls();
    }
}
