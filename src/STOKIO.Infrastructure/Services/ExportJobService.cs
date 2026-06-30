using Microsoft.EntityFrameworkCore;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Common;
using STOKIO.Application.Dtos.Exports;
using STOKIO.Domain.Entities;
using STOKIO.Domain.Enums;
using STOKIO.Infrastructure.Persistence;

namespace STOKIO.Infrastructure.Services;

public sealed class ExportJobService(
    StokioDbContext dbContext,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    ExportJobFileStore fileStore,
    IClock clock) : IExportJobService
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public async Task<ExportJobDto> CreateAsync(CreateExportJobRequest request, CancellationToken cancellationToken)
    {
        EnsureTenant();
        await ValidateAsync(request, cancellationToken);

        var job = new ExportJob
        {
            TenantId = currentTenant.TenantId,
            RequestedByUserId = currentUser.UserId,
            Type = request.Type,
            Status = ExportJobStatus.Queued,
            CountId = request.Type == ExportJobType.CountDifferences ? request.CountId : null,
            From = request.Type == ExportJobType.StockMovements ? request.From : null,
            To = request.Type == ExportJobType.StockMovements ? request.To : null,
            FileName = FileNameFor(request.Type),
            ContentType = XlsxContentType,
            ExpiresAt = clock.UtcNow.AddHours(24)
        };

        dbContext.ExportJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(job);
    }

    public async Task<ExportJobDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var job = await FindJobAsync(id, cancellationToken);
        return ToDto(job);
    }

    public async Task<ExportFile> DownloadAsync(Guid id, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var job = await FindJobAsync(id, cancellationToken);

        if (job.ExpiresAt <= clock.UtcNow)
        {
            throw new AppProblemException(410, "export_expired", "Dışa aktarma dosyasının süresi doldu.");
        }

        if (job.Status == ExportJobStatus.Failed)
        {
            throw new AppProblemException(409, "export_failed", job.ErrorMessage ?? "Dışa aktarma işlemi başarısız oldu.");
        }

        if (job.Status != ExportJobStatus.Ready || string.IsNullOrWhiteSpace(job.StorageKey))
        {
            throw new AppProblemException(409, "export_not_ready", "Dışa aktarma dosyası henüz hazır değil.");
        }

        var content = await fileStore.ReadAsync(job.StorageKey, cancellationToken);
        return new ExportFile(job.FileName, job.ContentType, content);
    }

    private async Task ValidateAsync(CreateExportJobRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Type))
        {
            throw new AppProblemException(400, "invalid_export_type", "Dışa aktarma tipi geçersiz.");
        }

        if (request.From.HasValue && request.To.HasValue && request.From > request.To)
        {
            throw new AppProblemException(400, "invalid_export_period", "Başlangıç tarihi bitiş tarihinden büyük olamaz.");
        }

        if (request.Type != ExportJobType.StockMovements && (request.From.HasValue || request.To.HasValue))
        {
            throw new AppProblemException(400, "invalid_export_period", "Tarih aralığı yalnızca hareket dışa aktarımı için kullanılabilir.");
        }

        if (request.Type != ExportJobType.CountDifferences && request.CountId.HasValue)
        {
            throw new AppProblemException(400, "invalid_export_count", "Sayım bilgisi yalnızca sayım farkı dışa aktarımı için kullanılabilir.");
        }

        if (request.Type == ExportJobType.CountDifferences)
        {
            if (!request.CountId.HasValue)
            {
                throw new AppProblemException(400, "count_required", "Sayım farkı dışa aktarımı için sayım seçilmelidir.");
            }

            var countExists = await dbContext.InventoryCounts
                .AsNoTracking()
                .AnyAsync(x => x.Id == request.CountId.Value, cancellationToken);

            if (!countExists)
            {
                throw new AppProblemException(404, "count_not_found", "Sayım bulunamadı.");
            }
        }
    }

    private async Task<ExportJob> FindJobAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await dbContext.ExportJobs
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (job is null)
        {
            throw new AppProblemException(404, "export_job_not_found", "Dışa aktarma işi bulunamadı.");
        }

        return job;
    }

    private void EnsureTenant()
    {
        if (!currentTenant.HasTenant)
        {
            throw new AppProblemException(401, "tenant_required", "Tenant bağlamı gerekli.");
        }
    }

    private static ExportJobDto ToDto(ExportJob job)
    {
        return new ExportJobDto(
            job.Id,
            job.Type,
            job.Status,
            job.FileName,
            job.CreatedAt,
            job.CompletedAt,
            job.ExpiresAt,
            job.NextAttemptAt,
            job.FailedReasonCode,
            job.ErrorMessage);
    }

    private static string FileNameFor(ExportJobType type)
    {
        return type switch
        {
            ExportJobType.CurrentStock => "stokio-current-stock.xlsx",
            ExportJobType.CriticalStock => "stokio-critical-stock.xlsx",
            ExportJobType.StockMovements => "stokio-stock-movements.xlsx",
            ExportJobType.CountDifferences => "stokio-count-differences.xlsx",
            _ => "stokio-export.xlsx"
        };
    }
}
