namespace BattleCatCalc.Core.Models;

/// <summary>
/// Describes a single movement step from one position to another during simulation.
/// </summary>
/// <param name="Source">Starting position of this step.</param>
/// <param name="Target">Ending position after this step.</param>
/// <param name="Unit">Name of the cat obtained.</param>
/// <param name="PoolId">Which pool was rolled (empty for single-pool mode).</param>
/// <param name="IsGuaranteed">Whether this was a guaranteed (十連) roll.</param>
/// <param name="IsTrackSwitch">Whether the step crosses from track A to B or vice versa.</param>
/// <param name="IsDuplicateRare">Whether triggered by a consecutive rare duplicate.</param>
public record class GachaStep(
    Position Source,
    Position Target,
    string Unit,
    string PoolId = "",
    bool IsGuaranteed = false,
    bool IsTrackSwitch = false,
    bool IsDuplicateRare = false);
