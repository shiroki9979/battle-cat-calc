namespace BattleCatCalc.Core.Models;

/// <summary>
/// Represents a gacha event entry parsed from the seed tracking service.
/// </summary>
/// <param name="Value">Event identifier used in API queries (e.g. "2026-03-30_943").</param>
/// <param name="Label">Human-readable event description.</param>
/// <param name="Group">Category group: "Upcoming", "Custom", or "Past".</param>
public sealed record EventInfo(string Value, string Label, string Group);
