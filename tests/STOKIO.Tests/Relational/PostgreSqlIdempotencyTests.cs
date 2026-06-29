using STOKIO.Application.Common;
using STOKIO.Domain.Enums;
using STOKIO.Infrastructure.Services;
using STOKIO.Tests.Common;

namespace STOKIO.Tests.Relational;

[Collection(PostgreSqlCollection.Name)]
public sealed class PostgreSqlIdempotencyTests(PostgreSqlDatabaseFixture fixture)
{
    [PostgreSqlFact]
    [Trait("Layer", "RelationalIntegration")]
    public async Task TryReserveAsync_AllowsOnlyOneStartedReservation_ForSameTenantOperationAndKey()
    {
        var tenantId = Guid.CreateVersion7();
        await fixture.ResetAsync();
        await fixture.SeedTenantAsync(tenantId);
        var fingerprint = new { ProductId = Guid.CreateVersion7(), Quantity = 5 };

        await using var firstContext = fixture.CreateDbContext(new TestCurrentTenant(tenantId));
        await using var secondContext = fixture.CreateDbContext(new TestCurrentTenant(tenantId));
        var firstService = new IdempotencyService(
            firstContext,
            new TestCurrentTenant(tenantId),
            new TestIdempotencyKeyAccessor("same-request-key"));
        var secondService = new IdempotencyService(
            secondContext,
            new TestCurrentTenant(tenantId),
            new TestIdempotencyKeyAccessor("same-request-key"));

        var attempts = await Task.WhenAll(
            TryReserveAsync(firstService, fingerprint),
            TryReserveAsync(secondService, fingerprint));

        Assert.Contains(attempts, x => x == "reserved");
        Assert.Contains(attempts, x => x == "idempotency_key_in_progress");

        await using var assertionContext = fixture.CreateDbContext(new TestCurrentTenant(tenantId));
        var record = Assert.Single(assertionContext.IdempotencyRecords);
        Assert.Equal(IdempotencyRecordStatus.Started, record.Status);
    }

    private static async Task<string> TryReserveAsync(IdempotencyService service, object fingerprint)
    {
        try
        {
            await service.TryReserveAsync("stock.movement", fingerprint, CancellationToken.None);
            return "reserved";
        }
        catch (AppProblemException exception)
        {
            return exception.Code;
        }
    }
}
