using STOKIO.Application.Abstractions;
using STOKIO.Domain.Enums;

namespace STOKIO.Tests.Common;

public sealed class TestCurrentUser(Guid? userId = null, UserRole role = UserRole.Owner) : ICurrentUser
{
    public Guid? UserId { get; } = userId ?? Guid.CreateVersion7();
    public string? Email => "owner@test.local";
    public string? Role => role.ToString();
}
