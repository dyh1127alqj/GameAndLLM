using GameCore.Model;

namespace GameCore.Events;

/// <summary>
/// 零 GC 堆分配事件队列（固定栈/预分配定长环形双缓冲区）
/// 专供内核 60Hz 产生事件，表现层每帧通过 ReadOnlySpan<T> 批量消费
/// </summary>
public sealed class BattleEventQueue
{
    private const int MaxCapacity = 2048;

    private readonly DamageEvent[] _damageBuffer = new DamageEvent[MaxCapacity];
    private int _damageCount;

    private readonly HealEvent[] _healBuffer = new HealEvent[MaxCapacity];
    private int _healCount;

    private readonly SkillCastEvent[] _skillCastBuffer = new SkillCastEvent[MaxCapacity];
    private int _skillCastCount;

    private readonly UnitDiedEvent[] _diedBuffer = new UnitDiedEvent[MaxCapacity];
    private int _diedCount;

    public void PushDamage(in DamageEvent evt)
    {
        if (_damageCount < MaxCapacity)
        {
            _damageBuffer[_damageCount++] = evt;
        }
    }

    public void PushHeal(in HealEvent evt)
    {
        if (_healCount < MaxCapacity)
        {
            _healBuffer[_healCount++] = evt;
        }
    }

    public void PushSkillCast(in SkillCastEvent evt)
    {
        if (_skillCastCount < MaxCapacity)
        {
            _skillCastBuffer[_skillCastCount++] = evt;
        }
    }

    public void PushUnitDied(in UnitDiedEvent evt)
    {
        if (_diedCount < MaxCapacity)
        {
            _diedBuffer[_diedCount++] = evt;
        }
    }

    public ReadOnlySpan<DamageEvent> ConsumeDamageEvents()
    {
        var span = new ReadOnlySpan<DamageEvent>(_damageBuffer, 0, _damageCount);
        _damageCount = 0;
        return span;
    }

    public ReadOnlySpan<HealEvent> ConsumeHealEvents()
    {
        var span = new ReadOnlySpan<HealEvent>(_healBuffer, 0, _healCount);
        _healCount = 0;
        return span;
    }

    public ReadOnlySpan<SkillCastEvent> ConsumeSkillCastEvents()
    {
        var span = new ReadOnlySpan<SkillCastEvent>(_skillCastBuffer, 0, _skillCastCount);
        _skillCastCount = 0;
        return span;
    }

    public ReadOnlySpan<UnitDiedEvent> ConsumeUnitDiedEvents()
    {
        var span = new ReadOnlySpan<UnitDiedEvent>(_diedBuffer, 0, _diedCount);
        _diedCount = 0;
        return span;
    }

    public void ClearAll()
    {
        _damageCount = 0;
        _healCount = 0;
        _skillCastCount = 0;
        _diedCount = 0;
    }
}
