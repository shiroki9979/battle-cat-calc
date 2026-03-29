using System.Net;

namespace BattleCatCalc.Core.Exceptions;

/// <summary>
/// Thrown when fetching gacha data from the remote service fails.
/// </summary>
public sealed class FetchException : BattleCatCalcException
{
    public FetchException(string message, string seed, string eventId, HttpStatusCode? statusCode = null)
        : base(message)
    {
        Seed = seed;
        EventId = eventId;
        StatusCode = statusCode;
    }

    public FetchException(string message, Exception innerException, string seed, string eventId, HttpStatusCode? statusCode = null)
        : base(message, innerException)
    {
        Seed = seed;
        EventId = eventId;
        StatusCode = statusCode;
    }

    public string Seed { get; }
    public string EventId { get; }
    public HttpStatusCode? StatusCode { get; }
}
