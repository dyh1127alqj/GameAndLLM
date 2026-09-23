using System;
using System.Collections.Generic;

namespace GameCore.Rogue;

public sealed class BackpackService
{
    public const int MaxCapacity = 15;
    private readonly List<AffixInstance> _items = new();

    public IReadOnlyList<AffixInstance> Items => _items;
    public int Capacity => MaxCapacity;
    public int Count => _items.Count;
    public int FreeSlots => MaxCapacity - _items.Count;
    public bool IsFull => _items.Count >= MaxCapacity;

    public bool AddItem(AffixInstance item)
    {
        if (IsFull) return false;
        _items.Add(item);
        return true;
    }

    public bool RemoveItem(string instanceId)
    {
        var match = _items.Find(i => i.InstanceId == instanceId);
        return match != null && _items.Remove(match);
    }

    public AffixInstance? Refine(AffixInstance a, AffixInstance b)
    {
        if (a.Definition.Id != b.Definition.Id) return null;
        if (a.CurrentTier != b.CurrentTier) return null;
        if (a.CurrentTier >= AffixTier.Tier3) return null;

        _items.Remove(a);
        _items.Remove(b);

        var upgradedTier = (AffixTier)((int)a.CurrentTier + 1);
        var upgraded = new AffixInstance(a.Definition, upgradedTier);
        _items.Add(upgraded);
        return upgraded;
    }

    public bool Refine(AffixInstance a, AffixInstance b, out AffixInstance? upgraded)
    {
        upgraded = Refine(a, b);
        return upgraded != null;
    }

    public int Salvage(AffixInstance item)
    {
        if (!_items.Remove(item)) return 0;
        return item.CurrentTier switch
        {
            AffixTier.Tier1 => 20,
            AffixTier.Tier2 => 50,
            AffixTier.Tier3 => 120,
            _ => 10
        };
    }
}
