using STOKIO.Application.Abstractions;

namespace STOKIO.Api.Security;

public sealed class CorrelationContext : ICorrelationContext
{
    public string? CorrelationId { get; private set; }

    public void SetCorrelationId(string correlationId)
    {
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            CorrelationId = correlationId;
        }
    }
}
