namespace BattleCatCalc.Core.Exceptions;

/// <summary>
/// Thrown when HTML content cannot be parsed into expected gacha structures.
/// </summary>
public sealed class ParseException : BattleCatCalcException
{
    public ParseException(string message, string? html = null, string? selector = null)
        : base(message)
    {
        Html = html;
        Selector = selector;
    }

    public ParseException(string message, Exception innerException, string? html = null, string? selector = null)
        : base(message, innerException)
    {
        Html = html;
        Selector = selector;
    }

    /// <summary>The HTML fragment that caused the error (truncated for logging).</summary>
    public string? Html { get; }

    /// <summary>The CSS selector that failed to match, if applicable.</summary>
    public string? Selector { get; }
}
