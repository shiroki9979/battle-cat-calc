using System.Collections.Frozen;
using BattleCatCalc.Core.Models;

namespace BattleCatCalc.Application.Simulation;

/// <summary>
/// Immutable search node for A*/Dijkstra path finding.
/// Path is reconstructed lazily via parent chain to reduce memory usage.
/// </summary>
internal readonly record struct SearchState(
    float Priority,
    float Cost,
    int PosId,
    string PosTrack,
    FrozenSet<string> Collected,
    int StateId,
    int? ParentId,
    GachaStep? LastStep,
    string? LastRolledRare);
