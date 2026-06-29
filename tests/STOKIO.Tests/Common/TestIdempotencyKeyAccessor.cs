using STOKIO.Application.Abstractions;

namespace STOKIO.Tests.Common;

public sealed class TestIdempotencyKeyAccessor(string? key) : IIdempotencyKeyAccessor
{
    public string? IdempotencyKey => key;
}
