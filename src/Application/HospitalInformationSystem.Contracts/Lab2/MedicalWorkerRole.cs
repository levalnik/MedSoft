using System.Text.Json;
using System.Text.Json.Serialization;

namespace HospitalInformationSystem.Contracts.Lab2;

[JsonConverter(typeof(MedicalWorkerRoleJsonConverter))]
public enum MedicalWorkerRole
{
    Doctor,
    LaboratoryTechnician,
    Nurse,
    Other
}

public sealed class MedicalWorkerRoleJsonConverter : JsonConverter<MedicalWorkerRole>
{
    public override MedicalWorkerRole Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options) => reader.TokenType == JsonTokenType.String
        ? reader.GetString() switch
        {
            "doctor" => MedicalWorkerRole.Doctor,
            "laboratory-technician" => MedicalWorkerRole.LaboratoryTechnician,
            "nurse" => MedicalWorkerRole.Nurse,
            "other" => MedicalWorkerRole.Other,
            _ => throw new JsonException("Неизвестная медицинская роль.")
        }
        : throw new JsonException("Медицинская роль должна быть строкой.");

    public override void Write(Utf8JsonWriter writer, MedicalWorkerRole value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value switch
        {
            MedicalWorkerRole.Doctor => "doctor",
            MedicalWorkerRole.LaboratoryTechnician => "laboratory-technician",
            MedicalWorkerRole.Nurse => "nurse",
            MedicalWorkerRole.Other => "other",
            _ => throw new JsonException("Неизвестная медицинская роль.")
        });
}
