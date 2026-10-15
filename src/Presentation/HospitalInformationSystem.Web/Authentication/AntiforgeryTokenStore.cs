namespace HospitalInformationSystem.Web.Authentication;

public sealed class AntiforgeryTokenStore
{
    public string? RequestToken { get; set; }
    public long Version { get; private set; }

    public void Reset()
    {
        RequestToken = null;
        Version++;
    }
}
