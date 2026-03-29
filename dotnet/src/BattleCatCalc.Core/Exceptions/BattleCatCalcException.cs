namespace BattleCatCalc.Core.Exceptions;

/// <summary>
/// Base exception for all BattleCatCalc-specific errors.
/// </summary>
public class BattleCatCalcException : Exception
{
    public BattleCatCalcException(string message) : base(message) { }
    public BattleCatCalcException(string message, Exception innerException)
        : base(message, innerException) { }
}
