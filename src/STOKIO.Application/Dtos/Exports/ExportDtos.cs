namespace STOKIO.Application.Dtos.Exports;

public sealed record ExportFile(
    string FileName,
    string ContentType,
    byte[] Content);

