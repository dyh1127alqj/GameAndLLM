using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.Model;

namespace GameCore.Rogue;

public enum AffixTag
{
    Guardian, Striker, Mystic, Ranger, Shadow, Support, Shushan, Cyber
}

public enum AffixTier
{
    Tier1 = 1, Tier2 = 2, Tier3 = 3
}

public sealed class AffixDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public SlotType AllowedSlots { get; init; } = SlotType.Core;
    public List<AffixTag> Tags { get; init; } = new();
    public Profession? Affinity { get; init; }
    public string Description { get; init; } = "";

    public AffixDefinition() { }

    public AffixDefinition(string id, string name, SlotType allowedSlots, IEnumerable<AffixTag> tags, string description = "")
    {
        Id = id;
        Name = name;
        AllowedSlots = allowedSlots;
        Tags = tags.ToList();
        Description = description;
    }
}

public sealed class AffixInstance
{
    public string InstanceId { get; } = Guid.NewGuid().ToString("N");
    public AffixDefinition Definition { get; }
    public AffixTier CurrentTier { get; set; } = AffixTier.Tier1;

    public AffixInstance(AffixDefinition definition, AffixTier tier = AffixTier.Tier1)
    {
        Definition = definition;
        CurrentTier = tier;
    }

    public AffixInstance(string instanceId, AffixDefinition definition, AffixTier tier = AffixTier.Tier1)
    {
        InstanceId = instanceId;
        Definition = definition;
        CurrentTier = tier;
    }
}
