using Microsoft.EntityFrameworkCore;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Common;
using STOKIO.Domain.Enums;
using STOKIO.Infrastructure.Persistence;
using STOKIO.Infrastructure.Services;

namespace STOKIO.Tests.Stock;

public sealed class IdempotencyServiceTests
{
    [Fact]
    public async Task TryReserveAsync_RejectsStartedReservation_WhenSameKeyIsInProgress()
    {
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = CreateDbContext(tenantId);
        var service = new IdempotencyService(dbContext, new TestCurrentTenant(tenantId), new TestIdempotencyKeyAccessor("in-progress-key"));
        var fingerprint = new { ProductId = Guid.CreateVersion7(), Quantity = 5 };

        var firstReservation = await service.TryReserveAsync("stock.movement", fingerprint, CancellationToken.None);
        var exception = await Assert.ThrowsAsync<AppProblemException>(() =>
            service.TryReserveAsync("stock.movement", fingerprint, CancellationToken.None));

        Assert.Null(firstReservation);
        Assert.Equal("idempotency_key_in_progress", exception.Code);
        var record = Assert.Single(dbContext.IdempotencyRecords);
        Assert.Equal(IdempotencyRecordStatus.Started, record.Status);
    }

    [Fact]
    public async Task TryReserveAsync_ReturnsCompletedReservationSnapshot_WhenSameRequestIsReplayed()
    {
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = CreateDbContext(tenantId);
        var service = new IdempotencyService(dbContext, new TestCurrentTenant(tenantId), new TestIdempotencyKeyAccessor("completed-key"));
        var fingerprint = new { ShipmentId = Guid.CreateVersion7() };
        var response = new TestResponse(Guid.CreateVersion7(), "completed");

        await service.TryReserveAsync("shipment.create", fingerprint, CancellationToken.None);
        Assert.True(await service.CompleteAsync("shipment.create", fingerprint, "Shipment", response.Id.ToString(), response, CancellationToken.None));
        await dbContext.SaveChangesAsync();

        var existing = await service.TryReserveAsync("shipment.create", fingerprint, CancellationToken.None);
        var snapshot = service.TryReadResponseSnapshot<TestResponse>(existing!);

        Assert.NotNull(existing);
        Assert.Equal(IdempotencyRecordStatus.Completed, existing!.Status);
        Assert.Equal(response, snapshot);
        Assert.Equal(response.Id.ToString(), existing.ResourceId);
    }

    [Fact]
    public async Task TryReserveAsync_RejectsSameKeyWithDifferentPayload()
    {
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = CreateDbContext(tenantId);
        var service = new IdempotencyService(dbContext, new TestCurrentTenant(tenantId), new TestIdempotencyKeyAccessor("conflict-key"));
        var originalFingerprint = new { ProductId = Guid.CreateVersion7(), Quantity = 5 };
        var conflictingFingerprint = new { originalFingerprint.ProductId, Quantity = 6 };

        await service.TryReserveAsync("stock.movement", originalFingerprint, CancellationToken.None);
        Assert.True(await service.CompleteAsync("stock.movement", originalFingerprint, "StockMovement", Guid.CreateVersion7().ToString(), new TestResponse(Guid.CreateVersion7(), "ok"), CancellationToken.None));
        await dbContext.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<AppProblemException>(() =>
            service.TryReserveAsync("stock.movement", conflictingFingerprint, CancellationToken.None));

        Assert.Equal("idempotency_key_conflict", exception.Code);
    }

    private static StokioDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<StokioDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new StokioDbContext(options, new TestCurrentTenant(tenantId));
    }

    private sealed record TestResponse(Guid Id, string Status);

    private sealed class TestCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public bool HasTenant => true;
        public Guid TenantId => tenantId;
        public string? TenantSlug => "test";
        public void SetTenant(Guid tenantId, string? slug)
        {
        }
    }

    private sealed class TestIdempotencyKeyAccessor(string key) : IIdempotencyKeyAccessor
    {
        public string? IdempotencyKey => key;
    }
}
