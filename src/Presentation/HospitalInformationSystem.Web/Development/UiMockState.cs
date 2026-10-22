using System.Net;
using HospitalInformationSystem.Contracts.Auth;
using HospitalInformationSystem.Contracts.Diagnoses;
using HospitalInformationSystem.Contracts.Lab2;
using HospitalInformationSystem.Contracts.MedicalRecords;
using HospitalInformationSystem.Contracts.MedicalWorkers;
using HospitalInformationSystem.Contracts.Patients;
using HospitalInformationSystem.Web.Authentication;

namespace HospitalInformationSystem.Web.Development;

public sealed class UiMockState
{
    public const string DemonstrationPassword = "UiMock-only-password!2026";
    public const string DemonstrationTotpCode = "012345";
    public const string SetupToken = "ui-mock-setup-token";
    public static readonly Guid DoctorId = Guid.Parse("10000000-0000-4000-8000-000000000001");
    public static readonly Guid NurseId = Guid.Parse("10000000-0000-4000-8000-000000000002");
    public static readonly Guid LaboratoryTechnicianId = Guid.Parse("10000000-0000-4000-8000-000000000003");
    public static readonly Guid OtherWorkerId = Guid.Parse("10000000-0000-4000-8000-000000000004");
    public static readonly Guid PatientWithRecordId = Guid.Parse("20000000-0000-4000-8000-000000000001");
    public static readonly Guid PatientWithoutRecordId = Guid.Parse("20000000-0000-4000-8000-000000000002");
    public static readonly Guid MedicalRecordId = Guid.Parse("30000000-0000-4000-8000-000000000001");
    public static readonly Guid DiagnosisId = Guid.Parse("40000000-0000-4000-8000-000000000001");

    private readonly SessionState _session;

    public UiMockState(SessionState session)
    {
        _session = session;
        Seed();
    }

    public UiMockPersona Persona { get; private set; }
    public bool SetupRequired { get; set; }
    public HttpStatusCode? NextStatusCode { get; set; }
    public TimeSpan ResponseDelay { get; set; }
    public bool EnableLiveAudit { get; set; } = true;
    public int RequestCount { get; internal set; }
    public event Action? Changed;

    internal object SyncRoot { get; } = new();
    internal Dictionary<Guid, MedicalWorkerDto> Workers { get; } = [];
    internal Dictionary<Guid, string> WorkerRoles { get; } = [];
    internal Dictionary<Guid, PatientDto> Patients { get; } = [];
    internal Dictionary<Guid, DiagnosisDefinitionDto> Diagnoses { get; } = [];
    internal Dictionary<Guid, MockMedicalRecord> Records { get; } = [];
    internal Dictionary<Guid, MockAccount> Accounts { get; } = [];
    internal Dictionary<Guid, string> Passwords { get; } = [];
    internal List<MockAuditEvent> Audit { get; } = [];
    internal Guid? ActiveAccountId { get; private set; }
    internal Guid? SetupChallengeId { get; set; }
    internal DateTimeOffset? SetupChallengeExpiresAt { get; set; }
    internal string? SetupPassword { get; set; }
    internal DateTimeOffset LastAuditTick { get; set; } = DateTimeOffset.UtcNow;
    internal SessionDto? CurrentSession => _session.Current;

    public void SwitchPersona(UiMockPersona persona)
    {
        lock (SyncRoot)
        {
            if (persona == UiMockPersona.Anonymous)
            {
                SignOut();
                return;
            }

            var userName = persona switch
            {
                UiMockPersona.Administrator => "admin.demo",
                UiMockPersona.Doctor => "doctor.demo",
                UiMockPersona.Nurse => "nurse.demo",
                UiMockPersona.LaboratoryTechnician => "lab.demo",
                UiMockPersona.Other => "other.demo",
                _ => throw new ArgumentOutOfRangeException(nameof(persona))
            };
            SignIn(Accounts.Values.Single(x => x.UserName == userName));
        }
    }

    public void Reset()
    {
        lock (SyncRoot)
        {
            SignOut();
            Workers.Clear();
            WorkerRoles.Clear();
            Patients.Clear();
            Diagnoses.Clear();
            Records.Clear();
            Accounts.Clear();
            Passwords.Clear();
            Audit.Clear();
            SetupRequired = false;
            SetupChallengeId = null;
            SetupChallengeExpiresAt = null;
            SetupPassword = null;
            NextStatusCode = null;
            ResponseDelay = TimeSpan.Zero;
            EnableLiveAudit = true;
            LastAuditTick = DateTimeOffset.UtcNow;
            RequestCount = 0;
            Seed();
        }
    }

    internal void SignIn(MockAccount account)
    {
        ActiveAccountId = account.Id;
        var role = account.MedicalWorkerId is { } id ? WorkerRoles.GetValueOrDefault(id, "other") : null;
        Persona = account.Role == ApplicationRoles.Administrator ? UiMockPersona.Administrator : role switch
        {
            "doctor" => UiMockPersona.Doctor,
            "nurse" => UiMockPersona.Nurse,
            "laboratory-technician" => UiMockPersona.LaboratoryTechnician,
            _ => UiMockPersona.Other
        };
        _session.SetSession(new SessionDto(account.Id, account.UserName, account.Role,
            account.MedicalWorkerId, ToRole(role)));
        Changed?.Invoke();
    }

    internal void SignOut()
    {
        ActiveAccountId = null;
        Persona = UiMockPersona.Anonymous;
        _session.SetSession(null);
        Changed?.Invoke();
    }

    internal bool HasActiveSession
    {
        get
        {
            if (ActiveAccountId is not { } id || !Accounts.TryGetValue(id, out var account)
                || !account.IsActive || account.IsLockedOut || CurrentSession is not { } session
                || session.Id != id || session.Role != account.Role || session.MedicalWorkerId != account.MedicalWorkerId)
                return false;
            if (account.Role == ApplicationRoles.Administrator) return account.MedicalWorkerId is null;
            return account.Role == ApplicationRoles.MedicalWorker && account.MedicalWorkerId is { } workerId
                && Workers.TryGetValue(workerId, out var worker) && !worker.IsDeleted
                && session.MedicalWorkerRole == ToRole(WorkerRoles.GetValueOrDefault(workerId));
        }
    }

    internal bool IsAdministrator => HasActiveSession && CurrentSession?.Role == ApplicationRoles.Administrator;
    internal string? CurrentMedicalRole => HasActiveSession && CurrentSession?.MedicalWorkerId is { } id
        ? WorkerRoles.GetValueOrDefault(id, "other") : null;

    internal void AddAudit(string eventType, string message, Guid? targetId = null)
    {
        Audit.Add(new MockAuditEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, eventType,
            CurrentSession?.Id, CurrentSession?.UserName ?? "Неавторизованный запрос", message, targetId));
    }

    internal void AppendEvent(MockMedicalRecord record, string eventType, Guid? entityId, string payload,
        DateTimeOffset? occurredAt = null)
    {
        var at = occurredAt ?? DateTimeOffset.UtcNow;
        var authorId = CurrentSession?.MedicalWorkerId ?? DoctorId;
        var author = Workers[authorId];
        record.Current = record.Current with { Version = record.Current.Version + 1 };
        record.History.Add(new MedicalRecordHistoryEventDto(record.Current.Version, at, authorId,
            $"{author.LastName} {author.FirstName}", author.Position, eventType, entityId, payload));
        record.States.Add((at, record.Current));
    }

    private void Seed()
    {
        AddWorker(DoctorId, "Алексей", "Учебный", "Врач-терапевт", "doctor");
        AddWorker(NurseId, "Мария", "Примерова", "Медицинская сестра", "nurse");
        AddWorker(LaboratoryTechnicianId, "Павел", "Тестовый", "Лаборант", "laboratory-technician");
        AddWorker(OtherWorkerId, "Анна", "Демонстрационная", "Регистратор", "other");
        AddWorker(Guid.Parse("10000000-0000-4000-8000-000000000005"), "Ирина", "Резервная", "Врач", "doctor");
        AddAccount("admin.demo", ApplicationRoles.Administrator, null, 1);
        AddAccount("doctor.demo", ApplicationRoles.MedicalWorker, DoctorId, 2);
        AddAccount("nurse.demo", ApplicationRoles.MedicalWorker, NurseId, 3);
        AddAccount("lab.demo", ApplicationRoles.MedicalWorker, LaboratoryTechnicianId, 4);
        AddAccount("other.demo", ApplicationRoles.MedicalWorker, OtherWorkerId, 5);
        var inactive = AddAccount("inactive.demo", ApplicationRoles.MedicalWorker,
            Guid.Parse("10000000-0000-4000-8000-000000000005"), 6);
        Accounts[inactive.Id] = inactive with { IsActive = false };
        Patients[PatientWithRecordId] = new PatientDto(PatientWithRecordId, "Елена", "Учебная", "Петровна",
            new DateOnly(1985, 3, 12), AdministrativeSex.Female, "Синтетические данные", true);
        Patients[PatientWithoutRecordId] = new PatientDto(PatientWithoutRecordId, "Иван", "Примеров", null,
            new DateOnly(2000, 1, 1), AdministrativeSex.Male, null);
        Diagnoses[DiagnosisId] = new DiagnosisDefinitionDto(DiagnosisId, "D00.01", "Учебный диагноз");
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var record = new MockMedicalRecord(new MedicalRecordDto(MedicalRecordId, PatientWithRecordId,
            createdAt, 0, [], []));
        Records[MedicalRecordId] = record;
        AppendEvent(record, "MedicalRecordCreated", null, "{}", createdAt);
        AddAudit("MedicalWorkerCreated", "Подготовлены синтетические медработники UiMock.", DoctorId);
        AddAudit("PatientCreated", "Подготовлен учебный пациент UiMock.", PatientWithRecordId);
        Audit.Add(new MockAuditEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, "AdministratorSetup", null,
            "UiMock", "Подготовлены демонстрационные данные; сервер не использовался.", null, true));
    }

    private void AddWorker(Guid id, string firstName, string lastName, string position, string role)
    {
        Workers[id] = new MedicalWorkerDto(id, firstName, lastName, position, false, ToRole(role));
        WorkerRoles[id] = role;
    }

    private MockAccount AddAccount(string userName, string role, Guid? workerId, int suffix)
    {
        var account = new MockAccount(Guid.Parse($"50000000-0000-4000-8000-{suffix:D12}"), userName,
            role, workerId, true, false, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(-1));
        Accounts[account.Id] = account;
        Passwords[account.Id] = DemonstrationPassword;
        return account;
    }

    internal sealed record MockAccount(Guid Id, string UserName, string Role, Guid? MedicalWorkerId,
        bool IsActive, bool IsLockedOut, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

    internal sealed record MockAuditEvent(Guid EventId, DateTimeOffset EventTime, string EventType,
        Guid? UserId, string DisplayName, string Message, Guid? TargetId, bool IsSystem = false);

    internal sealed class MockMedicalRecord(MedicalRecordDto current)
    {
        public MedicalRecordDto Current { get; set; } = current;
        public List<MedicalRecordHistoryEventDto> History { get; } = [];
        public List<(DateTimeOffset At, MedicalRecordDto State)> States { get; } = [];
    }

    internal static MedicalWorkerRole? ToRole(string? value) => value switch
    {
        "doctor" => MedicalWorkerRole.Doctor,
        "nurse" => MedicalWorkerRole.Nurse,
        "laboratory-technician" => MedicalWorkerRole.LaboratoryTechnician,
        "other" => MedicalWorkerRole.Other,
        _ => null
    };

    internal static string RoleValue(MedicalWorkerRole value) => value switch
    {
        MedicalWorkerRole.Doctor => "doctor",
        MedicalWorkerRole.Nurse => "nurse",
        MedicalWorkerRole.LaboratoryTechnician => "laboratory-technician",
        _ => "other"
    };
}
