namespace STOKIO.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

