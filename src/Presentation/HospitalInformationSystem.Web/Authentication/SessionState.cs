using HospitalInformationSystem.Contracts.Auth;

namespace HospitalInformationSystem.Web.Authentication;

public sealed class SessionState
{
    public SessionDto? Current { get; private set; }
    public bool Initialized { get; private set; }
    public long Version { get; private set; }
    public event Action? Changed;

    public void SetSession(SessionDto? session)
    {
        Current = session;
        Initialized = true;
        Version++;
        Changed?.Invoke();
    }
}
