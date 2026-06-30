using STOKIO.Domain.Enums;

namespace STOKIO.Application.Dtos.Exports;

public sealed record ExportFile(
    string FileName,
    string ContentType,
    byte[] Content);

public sealed record CreateExportJobRequest(
    ExportJobType Type,
    Guid? CountId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null);

public sealed record ExportJobDto(
    Guid Id,
    ExportJobType Type,
    ExportJobStatus Status,
    string FileName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? NextAttemptAt,
    string? FailedReasonCode,
    string? ErrorMessage);
