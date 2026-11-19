using System.Net;

namespace OSSFuel.Api.Features.Discovery.Gemini;

public sealed class GeminiApiException : Exception
{
    public HttpStatusCode? StatusCode { get; }
    public string? ResponseBody { get; }

    public GeminiApiException(string message)
        : base(message)
    {
    }

    public GeminiApiException(string message, HttpStatusCode statusCode, string? responseBody)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public GeminiApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
