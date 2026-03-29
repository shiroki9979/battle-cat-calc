namespace BattleCatCalc.Core.Models;

/// <summary>
/// Represents a single slot on a gacha track (A or B).
/// </summary>
/// <param name="Id">Position of this slot (1-based ID + track).</param>
/// <param name="Result">The cat obtained via single roll, or a parse error.</param>
/// <param name="Guaranteed">The cat obtained via guaranteed (十連) roll, or a parse error.</param>
/// <param name="NextPos">Next position after rolling this slot (may differ due to duplicate rare).</param>
/// <param name="OriginalResult">Pre-substitution result text (for event-specific display).</param>
/// <param name="OriginalGuaranteed">Pre-substitution guaranteed text.</param>
/// <param name="IsDuplicateRare">Whether this slot triggers a track switch due to consecutive rare duplicates.</param>
public record class GachaSlot(
    Position Id,
    SlotValue Result,
    SlotValue Guaranteed,
    Position? NextPos = null,
    string? OriginalResult = null,
    string? OriginalGuaranteed = null,
    bool IsDuplicateRare = false);
