using STOKIO.Application.Abstractions;

namespace STOKIO.Api.Security;

public sealed class HttpIdempotencyKeyAccessor(IHttpContextAccessor httpContextAccessor) : IIdempotencyKeyAccessor
{
    public string? IdempotencyKey
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.Request.Headers["Idempotency-Key"].FirstOrDefault();
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
