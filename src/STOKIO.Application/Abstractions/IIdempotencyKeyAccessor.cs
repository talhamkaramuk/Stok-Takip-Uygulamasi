namespace STOKIO.Application.Abstractions;

public interface IIdempotencyKeyAccessor
{
    string? IdempotencyKey { get; }
}
