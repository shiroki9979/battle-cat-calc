using System.Collections.Frozen;
using BattleCatCalc.Core.Models;

namespace BattleCatCalc.Application.Simulation;

/// <summary>
/// Immutable search node for A*/Dijkstra path finding.
/// Path is reconstructed lazily via parent chain to reduce memory usage.
/// <para>
/// <c>LastRolledRare</c> is the cat actually obtained on the previous roll, used to detect duplicate rares across pools.
/// Only cats that could trigger a duplicate (cats replaced at some duplicate rare slot) are kept; others are null,
/// so the state space does not grow needlessly.
/// </para>
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
