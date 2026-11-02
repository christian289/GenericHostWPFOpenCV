using System.Net;

namespace OhMyRudolph.Core.Containers;

public sealed class ProcessingHttpException(HttpStatusCode statusCode, string code)
    : IOException($"Processing host returned HTTP {(int)statusCode} ({code}).")
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}
