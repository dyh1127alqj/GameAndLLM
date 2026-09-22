namespace GameCore.Targeting;

/// <summary>
/// 索敌黏性追踪状态（避免每帧因血量微弱波动而晃动切换目标）
/// </summary>
public sealed class TargetLockTracker
{
    private readonly Dictionary<int, LockEntry> _locks = new();

    private sealed class LockEntry
    {
        public int TargetUnitId { get; set; }
        public int RemainingTicks { get; set; }
    }

    /// <summary>
    /// 每 Tick 推进锁定时钟
    /// </summary>
    public void Tick()
    {
        foreach (var entry in _locks.Values)
        {
            if (entry.RemainingTicks > 0)
            {
                entry.RemainingTicks--;
            }
        }
    }

    /// <summary>
    /// 尝试获取当前仍处于黏性锁定期的有效目标 ID
    /// </summary>
    public bool TryGetLockedTarget(int attackerId, out int targetId)
    {
        if (_locks.TryGetValue(attackerId, out var entry) && entry.RemainingTicks > 0)
        {
            targetId = entry.TargetUnitId;
            return true;
        }

        targetId = 0;
        return false;
    }

    /// <summary>
    /// 锁定新目标，刷新 1.0 秒 (60 帧) 黏性保持期
    /// </summary>
    public void LockTarget(int attackerId, int targetId, int durationTicks = 60)
    {
        if (!_locks.TryGetValue(attackerId, out var entry))
        {
            entry = new LockEntry();
            _locks[attackerId] = entry;
        }

        entry.TargetUnitId = targetId;
        entry.RemainingTicks = durationTicks;
    }

    /// <summary>
    /// 强制清除锁定（如目标死亡、目标隐匿或自身被嘲讽）
    /// </summary>
    public void ClearLock(int attackerId)
    {
        _locks.Remove(attackerId);
    }
}
