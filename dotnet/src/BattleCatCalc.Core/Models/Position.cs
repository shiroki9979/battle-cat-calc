namespace BattleCatCalc.Core.Models;

/// <summary>
/// Represents a position on a gacha track (1-based ID + track identifier).
/// </summary>
public readonly record struct Position(int Id, string Track)
{
    public override string ToString() => $"{Id}{Track}";
}
