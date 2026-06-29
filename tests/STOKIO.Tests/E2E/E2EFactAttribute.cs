namespace STOKIO.Tests.E2E;

[AttributeUsage(AttributeTargets.Method)]
public sealed class E2EFactAttribute : FactAttribute
{
    public E2EFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(E2ETestSettings.BaseUrl))
        {
            Skip = "Set STOKIO_E2E_BASE_URL to run E2E smoke tests against a running API.";
        }
    }
}
