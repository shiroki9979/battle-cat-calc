using BattleCatCalc.Core.Models;

namespace BattleCatCalc.Core.Exceptions;

/// <summary>
/// Thrown when the gacha simulation encounters an unrecoverable state.
/// </summary>
public sealed class SimulationException : BattleCatCalcException
{
    public SimulationException(string message, Position startPos, int maxRolls)
        : base(message)
    {
        StartPos = startPos;
        MaxRolls = maxRolls;
    }

    public SimulationException(string message, Exception innerException, Position startPos, int maxRolls)
        : base(message, innerException)
    {
        StartPos = startPos;
        MaxRolls = maxRolls;
    }

    public Position StartPos { get; }
    public int MaxRolls { get; }
}
