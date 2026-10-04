using HospitalInformationSystem.Domain.MedicalWorkers.Services;

namespace HospitalInformationSystem.Domain.MedicalWorkers.Factories;

public static class MedicalWorkerFactory
{
    public static MedicalWorker Create(string firstName, string lastName, string position, DateTimeOffset now)
    {
        var worker = new MedicalWorker
        {
            Id = Guid.NewGuid(), CreatedAtUtc = now.ToUniversalTime(),
            FirstName = string.Empty, LastName = string.Empty, Position = string.Empty
        };
        MedicalWorkerService.Update(worker, firstName, lastName, position, now);
        return worker;
    }
}
