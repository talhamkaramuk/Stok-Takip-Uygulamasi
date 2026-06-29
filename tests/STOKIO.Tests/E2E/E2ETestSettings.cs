namespace STOKIO.Tests.E2E;

public static class E2ETestSettings
{
    public static string? BaseUrl => Environment.GetEnvironmentVariable("STOKIO_E2E_BASE_URL");
}
