namespace GameCore.Model;

/// <summary>
/// 零 GC 堆分配事件集（栈值类型 readonly record struct）
/// 专供内核环形缓冲区与 Godot 表现层 ReadOnlySpan<T> 零拷贝消费
/// </summary>
public readonly record struct DamageEvent(
    int Tick,
    int SourceId,
    int TargetId,
    int DamageAmount,
    DamageFlags Flags,
    DamageType Type
);

public readonly record struct HealEvent(
    int Tick,
    int SourceId,
    int TargetId,
    int HealAmount
);

public readonly record struct SkillCastEvent(
    int Tick,
    int CasterId,
    string SkillId,
    int TargetId,
    bool IsUltimate
);

public readonly record struct StatusAppliedEvent(
    int Tick,
    int TargetId,
    StatusType Status,
    int DurationTicks
);

public readonly record struct UnitDiedEvent(
    int Tick,
    int UnitId,
    int KillerId
);
