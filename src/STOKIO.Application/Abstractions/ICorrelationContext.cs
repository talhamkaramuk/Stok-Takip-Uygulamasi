namespace STOKIO.Application.Abstractions;

public interface ICorrelationContext
{
    string? CorrelationId { get; }
    void SetCorrelationId(string correlationId);
}
