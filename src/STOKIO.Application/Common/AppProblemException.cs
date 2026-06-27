namespace STOKIO.Application.Common;

public sealed class AppProblemException : Exception
{
    public AppProblemException(int statusCode, string code, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public int StatusCode { get; }
    public string Code { get; }
}

