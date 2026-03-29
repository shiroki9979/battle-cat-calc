using System.Collections;

namespace BattleCatCalc.Core.Models;

/// <summary>
/// Container for gacha slots on a single track, with O(1) lookup by slot ID.
/// Corresponds to Python's <c>Track(UserList)</c> using composition over inheritance.
/// </summary>
public sealed class Track : IReadOnlyList<GachaSlot>
{
    private readonly IReadOnlyList<GachaSlot> _slots;
    private readonly Dictionary<int, GachaSlot> _index;

    public Track(string name, IEnumerable<GachaSlot> slots)
    {
        Name = name;
        var list = slots.ToList();
        _slots = list.AsReadOnly();
        _index = new Dictionary<int, GachaSlot>(list.Count);
        foreach (var slot in list)
        {
            _index[slot.Id.Id] = slot;
        }
    }

    /// <summary>Track identifier (e.g. "A" or "B").</summary>
    public string Name { get; }

    /// <summary>Number of slots on this track.</summary>
    public int Count => _slots.Count;

    public GachaSlot this[int index] => _slots[index];

    /// <summary>
    /// Returns the slot with the given 1-based ID, or <c>null</c> if not found. O(1).
    /// </summary>
    public GachaSlot? GetSlot(int id) => _index.GetValueOrDefault(id);

    public IEnumerator<GachaSlot> GetEnumerator() => _slots.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => $"Track(name='{Name}', slots={Count})";
}
