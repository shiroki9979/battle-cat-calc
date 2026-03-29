using BattleCatCalc.Core.Enums;

namespace BattleCatCalc.Core.Models;

/// <summary>
/// Discriminated union for slot parsing results: either a cat name or a parse error status.
/// Corresponds to Python's <c>Union[str, ParseStatus]</c>.
/// </summary>
public abstract record SlotValue
{
    // 禁止外部繼承
    private SlotValue() { }

    /// <summary>A successfully parsed cat name.</summary>
    public sealed record CatName(string Value) : SlotValue
    {
        public override string ToString() => Value;
    }

    /// <summary>A parse error sentinel value.</summary>
    public sealed record Status(ParseStatus Value) : SlotValue
    {
        public override string ToString() => Value.ToString();
    }

    /// <summary>
    /// Returns the cat name if this is a <see cref="CatName"/>, otherwise <c>null</c>.
    /// </summary>
    public string? AsCatName() => this is CatName c ? c.Value : null;

    /// <summary>
    /// Returns <c>true</c> if this represents a valid cat name (not a parse error).
    /// </summary>
    public bool IsValid => this is CatName;
}
