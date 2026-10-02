using BattleCatCalc.Core.Enums;

namespace BattleCatCalc.Core.Models;

/// <summary>
/// Represents a single slot on a gacha track (A or B).
/// </summary>
/// <param name="Id">Position of this slot (1-based ID + track).</param>
/// <param name="Result">The cat obtained via single roll, or a parse error.</param>
/// <param name="Guaranteed">The cat obtained via guaranteed (十連) roll, or a parse error.</param>
/// <param name="NextPos">
/// Where a guaranteed (十連) roll started at this slot lands (the guaranteed column's arrow).
/// For duplicate rare slots this is the post-substitution version.
/// </param>
/// <param name="OriginalResult">Pre-substitution result text (for event-specific display).</param>
/// <param name="OriginalGuaranteed">Pre-substitution guaranteed text.</param>
/// <param name="IsDuplicateRare">Whether this slot is a duplicate rare slot (the table assumes the previous cat is the same cat).</param>
/// <param name="Rarity">Rarity of the single-roll cat, parsed from HTML CSS class.</param>
/// <param name="GuaranteedRarity">Rarity of the guaranteed cat from the HTML cat catalog, or null if unknown.</param>
/// <param name="SingleNextPos">
/// Where a single roll lands when the duplicate rare rule is triggered (the result column's arrow).
/// It is a different arrow from <paramref name="NextPos"/>; mixing them sends a single roll ten slots ahead.
/// Null for ordinary slots, or when the HTML gives no landing (e.g. <c>&lt;?&gt;</c> past the end of the table).
/// </param>
/// <param name="OriginalNextPos">
/// Guaranteed-roll landing when the duplicate rare rule is NOT triggered; pairs with <paramref name="OriginalGuaranteed"/>.
/// </param>
public record class GachaSlot(
    Position Id,
    SlotValue Result,
    SlotValue Guaranteed,
    Position? NextPos = null,
    string? OriginalResult = null,
    string? OriginalGuaranteed = null,
    bool IsDuplicateRare = false,
    CatRarity? Rarity = null,
    CatRarity? GuaranteedRarity = null,
    Position? SingleNextPos = null,
    Position? OriginalNextPos = null);
