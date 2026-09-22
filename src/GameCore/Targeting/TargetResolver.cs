using GameCore.Model;

namespace GameCore.Targeting;

/// <summary>
/// 纯函数目标解析器
/// 严格遵守就近原则、嘲讽拦截、隐匿豁免与目标锁定黏性
/// </summary>
public static class TargetResolver
{
    /// <summary>
    /// 解析单体目标（兼容调用接口，用于敌对索敌——按黏性锁定 + 阵营过滤）
    /// </summary>
    public static UnitSnapshot? ResolveTarget(
        UnitSnapshot attacker,
        TargetMode mode,
        IEnumerable<UnitSnapshot> targetPool,
        TargetLockTracker tracker)
    {
        var list = targetPool as IReadOnlyList<UnitSnapshot> ?? targetPool.ToList();
        return ResolveSingleTarget(attacker, list, mode, tracker);
    }

    /// <summary>
    /// 解析友方目标（治疗/增益专用）：不做阵营过滤——调用方已传入同阵营花名册；
    /// 不复用敌对索敌的黏性锁（治疗应实时跟随最低血量者，不应该"黏"在旧目标上）。
    /// </summary>
    public static UnitSnapshot? ResolveAllyTarget(
        UnitSnapshot caster,
        IReadOnlyList<UnitSnapshot> allies,
        TargetMode mode)
    {
        var candidates = allies.Where(u => !u.IsDead).ToList();
        if (candidates.Count == 0) return null;

        return mode switch
        {
            TargetMode.LowestHpAbsolute => candidates
                .OrderBy(u => u.CurrentHp)
                .ThenBy(u => u.UnitId)
                .FirstOrDefault(),
            TargetMode.HighestAtk => candidates
                .OrderByDescending(u => u.BaseAtk)
                .ThenBy(u => u.UnitId)
                .FirstOrDefault(),
            TargetMode.LowestDef => candidates
                .OrderBy(u => u.BaseArmor)
                .ThenBy(u => u.UnitId)
                .FirstOrDefault(),
            // LowestHpPercentage 及其余模式（含 Nearest/Backline 等对友方无意义的枚举值）
            // 统一回退为"血量百分比最低者优先"，这是治疗/增益场景下唯一有意义的默认口径
            _ => candidates
                .OrderBy(u => (long)u.CurrentHp * 1000 / u.MaxHp)
                .ThenBy(u => u.UnitId)
                .FirstOrDefault()
        };
    }

    /// <summary>
    /// 解析单体目标
    /// </summary>
    public static UnitSnapshot? ResolveSingleTarget(
        UnitSnapshot attacker,
        IReadOnlyList<UnitSnapshot> allUnits,
        TargetMode mode,
        TargetLockTracker tracker)
    {
        // 1. 优先检查嘲讽强制锁定
        if (attacker.TauntedByUnitId.HasValue)
        {
            var taunter = allUnits.FirstOrDefault(u => u.UnitId == attacker.TauntedByUnitId.Value && !u.IsDead);
            if (taunter != null)
            {
                tracker.LockTarget(attacker.UnitId, taunter.UnitId);
                return taunter;
            }
            attacker.TauntedByUnitId = null; // 施法者已死，解除嘲讽
        }

        // 2. 检查现存的黏性锁定是否仍有效
        if (tracker.TryGetLockedTarget(attacker.UnitId, out int lockedId))
        {
            var lockedTarget = allUnits.FirstOrDefault(u => u.UnitId == lockedId);
            if (lockedTarget != null && !lockedTarget.IsDead && !lockedTarget.IsStealthed)
            {
                return lockedTarget; // 锁定仍在有效期内且合法，直接沿用
            }
            tracker.ClearLock(attacker.UnitId); // 目标失效，清除
        }

        // 3. 筛选所有合法的敌对候选人 (非己方、未阵亡)
        var candidates = allUnits
            .Where(u => u.Faction != attacker.Faction && !u.IsDead)
            .ToList();

        if (candidates.Count == 0) return null;

        // 4. 过滤隐匿目标（非 AOE 单体锁定不可选取隐匿单位）
        var nonStealthCandidates = candidates.Where(u => !u.IsStealthed).ToList();
        var targetPool = nonStealthCandidates.Count > 0 ? nonStealthCandidates : candidates; // 全员隐匿时保底降级

        // 5. 按照指定的 TargetMode 索敌规则排序
        UnitSnapshot? selected = mode switch
        {
            TargetMode.Nearest => ResolveNearest(attacker, targetPool),
            TargetMode.LowestHpPercentage => targetPool
                .OrderBy(u => (long)u.CurrentHp * 1000 / u.MaxHp)
                .ThenBy(u => u.UnitId)
                .FirstOrDefault(),
            TargetMode.LowestHpAbsolute => targetPool
                .OrderBy(u => u.CurrentHp)
                .ThenBy(u => u.UnitId)
                .FirstOrDefault(),
            TargetMode.HighestAtk => targetPool
                .OrderByDescending(u => u.BaseAtk)
                .ThenBy(u => u.UnitId)
                .FirstOrDefault(),
            TargetMode.Backline => targetPool
                .OrderByDescending(u => u.IsBackline) // 后排优先
                .ThenBy(u => global::System.Math.Abs((u.BoardPosition % 5) - (attacker.BoardPosition % 5))) // 对位优先
                .FirstOrDefault(),
            TargetMode.LowestDef => targetPool
                .OrderBy(u => u.BaseArmor)
                .ThenBy(u => u.UnitId)
                .FirstOrDefault(),
            _ => ResolveNearest(attacker, targetPool)
        };

        if (selected != null)
        {
            // 刷新锁定黏性 (60 帧 / 1 秒)
            tracker.LockTarget(attacker.UnitId, selected.UnitId, 60);
        }

        return selected;
    }

    /// <summary>
    /// 标准就近索敌规则：优先同列，次选同行相邻，再次选前排
    /// 棋盘格子 0~4 为前排，5~9 为后排；列号 = BoardPosition % 5
    /// </summary>
    private static UnitSnapshot? ResolveNearest(UnitSnapshot attacker, IReadOnlyList<UnitSnapshot> pool)
    {
        int attackerCol = attacker.BoardPosition % 5;

        return pool
            .OrderBy(u => u.IsBackline) // 前排优先于后排
            .ThenBy(u => global::System.Math.Abs((u.BoardPosition % 5) - attackerCol)) // 同列或列距最近优先
            .ThenBy(u => u.UnitId)
            .FirstOrDefault();
    }
}
