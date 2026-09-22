using System;
using GameCore.Numerics;
using GameCore.Model;

namespace GameCore.Pipeline;

public readonly record struct HealResult(
    int FinalHeal,
    int TargetRemainingHp
);

public static class HealPipeline
{
    public static HealResult CalculateAndApply(
        UnitSnapshot caster,
        UnitSnapshot target,
        int skillHealPermille)
    {
        if (target.IsDead)
        {
            return new HealResult(0, 0);
        }

        // 基础治疗威力
        int raw = CombatMath.Mul(caster.BaseAtk, skillHealPermille);

        // 施法方提升乘区 (默认 1000 = 100%)
        int withDone = CombatMath.Mul(raw, caster.HealingDonePermille);

        // 受击方接收乘区 (默认 1000 = 100%)
        int finalHeal = CombatMath.Mul(withDone, target.HealingReceivedPermille);

        // 加血并钳制
        target.CurrentHp = Math.Min(target.MaxHp, target.CurrentHp + finalHeal);

        return new HealResult(finalHeal, target.CurrentHp);
    }
}
