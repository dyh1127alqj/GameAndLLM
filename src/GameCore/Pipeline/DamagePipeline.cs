using System;
using GameCore.Math;
using GameCore.Model;

namespace GameCore.Pipeline;

public readonly record struct DamageResult(
    int FinalDamage,
    int ShieldAbsorbed,
    int TargetRemainingHp,
    DamageFlags Flags,
    int VampireHeal,
    int ReflectDamage
);

public static class DamagePipeline
{
    public static DamageResult CalculateAndApply(
        UnitSnapshot attacker,
        UnitSnapshot target,
        int skillDamagePermille,
        DamageType damageType,
        DamageFlags baseFlags = DamageFlags.Direct,
        int vampirePermille = 0,
        int reflectPermille = 0,
        bool hasExecutePower = false)
    {
        if (target.IsDead)
        {
            return new DamageResult(0, 0, 0, DamageFlags.None, 0, 0);
        }

        // Step 1: 无敌检查
        if (target.IsInvincible)
        {
            return new DamageResult(0, 0, target.CurrentHp, baseFlags | DamageFlags.Blocked, 0, 0);
        }

        DamageFlags flags = baseFlags;

        // Step 2: 基础攻击力换算
        int raw = CombatMath.Mul(attacker.BaseAtk, skillDamagePermille);

        // Step 3: 护甲与穿透率减免
        int effectiveArmor = CombatMath.Mul(target.BaseArmor, Math.Max(0, 1000 - attacker.PenPermille));
        int armorK = CombatMath.ComputeArmorK(target.Level);
        int armorRatio = CombatMath.ArmorDamageRatio(effectiveArmor, armorK);
        int postArmorDmg = CombatMath.Mul(raw, armorRatio);

        // Step 4: 斩杀判定 (Execution: 直接伤害且血量比例 <= 12%)
        bool canExecute = hasExecutePower &&
                          flags.HasFlag(DamageFlags.Direct) &&
                          target.Rank != Rank.Boss &&
                          (target.CurrentHp * 1000 / Math.Max(1, target.MaxHp)) <= 120;

        int finalPreDamage = postArmorDmg;
        if (canExecute)
        {
            flags |= DamageFlags.Executed;
            finalPreDamage = target.CurrentHp + target.ShieldHp;
        }

        // Step 5: 护盾吸收 (ShieldHp)
        int shieldAbsorbed = 0;
        int hpToDeduct = finalPreDamage;
        if (target.ShieldHp > 0)
        {
            if (target.ShieldHp >= hpToDeduct)
            {
                shieldAbsorbed = hpToDeduct;
                target.ShieldHp -= hpToDeduct;
                hpToDeduct = 0;
            }
            else
            {
                shieldAbsorbed = target.ShieldHp;
                hpToDeduct -= target.ShieldHp;
                target.ShieldHp = 0;
            }
        }

        // Step 6: 真实扣血与受击回怒 (+5)
        target.CurrentHp = Math.Max(0, target.CurrentHp - hpToDeduct);
        if (target.CurrentHp > 0)
        {
            target.CurrentRage = Math.Min(100, target.CurrentRage + 5);
        }

        // Step 7: 吸血计算 (非反伤触发)
        int vampireHeal = 0;
        if (vampirePermille > 0 && !flags.HasFlag(DamageFlags.Reflected) && finalPreDamage > 0)
        {
            vampireHeal = CombatMath.Mul(finalPreDamage, vampirePermille);
            attacker.CurrentHp = Math.Min(attacker.MaxHp, attacker.CurrentHp + vampireHeal);
        }

        // Step 8: 反伤计算 (受击方反伤，非反伤触发)
        int reflectDmg = 0;
        if (reflectPermille > 0 && !flags.HasFlag(DamageFlags.Reflected) && finalPreDamage > 0)
        {
            reflectDmg = CombatMath.Mul(finalPreDamage, reflectPermille);
            // 对攻击方直接扣血
            attacker.CurrentHp = Math.Max(0, attacker.CurrentHp - reflectDmg);
        }

        return new DamageResult(finalPreDamage, shieldAbsorbed, target.CurrentHp, flags, vampireHeal, reflectDmg);
    }

    /// <summary>
    /// 兼容别名方法
    /// </summary>
    public static DamageResult ProcessDamage(
        UnitSnapshot attacker,
        UnitSnapshot target,
        int skillDamagePermille,
        DamageType damageType,
        DamageFlags baseFlags = DamageFlags.Direct,
        int vampirePermille = 0,
        int reflectPermille = 0,
        bool hasExecutePower = false)
    {
        return CalculateAndApply(attacker, target, skillDamagePermille, damageType, baseFlags, vampirePermille, reflectPermille, hasExecutePower);
    }
}
