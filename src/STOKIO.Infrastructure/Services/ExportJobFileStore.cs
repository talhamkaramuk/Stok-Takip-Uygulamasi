using Microsoft.Extensions.Configuration;
using STOKIO.Application.Common;
using STOKIO.Application.Dtos.Exports;

namespace STOKIO.Infrastructure.Services;

public sealed class ExportJobFileStore(IConfiguration configuration)
{
    private readonly string _rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(
        configuration["Exports:StoragePath"]
            ?? Path.Combine(AppContext.BaseDirectory, "export-files")));

    public async Task<string> SaveAsync(Guid tenantId, Guid jobId, ExportFile file, CancellationToken cancellationToken)
    {
        var tenantFolder = tenantId.ToString("N");
        var storageKey = Path.Combine(tenantFolder, $"{jobId:N}.xlsx");
        var path = ResolvePath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? _rootPath);
        await File.WriteAllBytesAsync(path, file.Content, cancellationToken);
        return storageKey.Replace(Path.DirectorySeparatorChar, '/');
    }

    public async Task<byte[]> ReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = ResolvePath(storageKey);
        if (!File.Exists(path))
        {
            throw new AppProblemException(410, "export_file_missing", "Dışa aktarma dosyası artık erişilebilir değil.");
        }

        return await File.ReadAllBytesAsync(path, cancellationToken);
    }

    public async Task CheckWritableAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_rootPath);
        var probePath = Path.Combine(_rootPath, $".health-{Guid.CreateVersion7():N}.tmp");
        await File.WriteAllTextAsync(probePath, "ok", cancellationToken);
        File.Delete(probePath);
    }

    private string ResolvePath(string storageKey)
    {
        var normalizedKey = storageKey
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(_rootPath, normalizedKey));
        var rootPrefix = _rootPath + Path.DirectorySeparatorChar;
        if (!path.Equals(_rootPath, StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new AppProblemException(400, "invalid_export_storage_key", "Dışa aktarma dosya anahtarı geçersiz.");
        }

        return path;
    }
}
