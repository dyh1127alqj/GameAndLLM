using System;
using System.Collections.Generic;
using System.Linq;

namespace GameCore.Rogue;

public sealed class VesselSockets
{
    public int UnitId { get; }
    public AffixInstance? CoreSlot1 { get; set; }
    public AffixInstance? CoreSlot2 { get; set; }
    public AffixInstance? CombatSlot1 { get; set; }
    public AffixInstance? CombatSlot2 { get; set; }
    public AffixInstance? GeneralSlot1 { get; set; }
    public AffixInstance? GeneralSlot2 { get; set; }

    public int UnlockedSlotCount { get; set; } = 3;

    public VesselSockets(int unitId)
    {
        UnitId = unitId;
    }

    public IEnumerable<AffixInstance> AllEquippedAffixes =>
        new[] { CoreSlot1, CoreSlot2, CombatSlot1, CombatSlot2, GeneralSlot1, GeneralSlot2 }
            .Where(a => a != null)!;

    public IEnumerable<AffixInstance> CoreAffixes =>
        new[] { CoreSlot1, CoreSlot2 }.Where(a => a != null)!;
}

public sealed class SocketManager
{
    private readonly Dictionary<int, VesselSockets> _vesselSockets = new();

    public VesselSockets GetOrCreateSockets(int unitId)
    {
        if (!_vesselSockets.TryGetValue(unitId, out var s))
        {
            s = new VesselSockets(unitId);
            _vesselSockets[unitId] = s;
        }
        return s;
    }

    public bool EquipAffix(int unitId, int slotIndex, AffixInstance affix, bool isSafeNode = true)
    {
        if (!isSafeNode) return false;
        var s = GetOrCreateSockets(unitId);

        return slotIndex switch
        {
            0 => TrySetCore(affix, a => s.CoreSlot1 = a),
            1 => TrySetCore(affix, a => s.CoreSlot2 = a),
            2 => TrySetCombat(affix, a => s.CombatSlot1 = a),
            3 when s.UnlockedSlotCount >= 4 => TrySetCombat(affix, a => s.CombatSlot2 = a),
            4 when s.UnlockedSlotCount >= 5 => TrySetGeneral(affix, a => s.GeneralSlot1 = a),
            5 when s.UnlockedSlotCount >= 6 => TrySetGeneral(affix, a => s.GeneralSlot2 = a),
            _ => false
        };
    }

    private static bool TrySetCore(AffixInstance a, Action<AffixInstance> setter)
    {
        if (!a.Definition.AllowedSlots.HasFlag(SlotType.Core)) return false;
        setter(a);
        return true;
    }

    private static bool TrySetCombat(AffixInstance a, Action<AffixInstance> setter)
    {
        if (!a.Definition.AllowedSlots.HasFlag(SlotType.Combat)) return false;
        setter(a);
        return true;
    }

    private static bool TrySetGeneral(AffixInstance a, Action<AffixInstance> setter)
    {
        if (!a.Definition.AllowedSlots.HasFlag(SlotType.General)) return false;
        setter(a);
        return true;
    }
}
