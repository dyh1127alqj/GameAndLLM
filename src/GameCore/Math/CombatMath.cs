namespace GameCore.Numerics;

/// <summary>
/// 纯静态确定性数学库（整数千分比 Integer Permille，FP_ONE = 1000）
/// 严格杜绝浮点数在跨平台（x86/ARM）环境下的 IEEE 754 精度漂移
/// </summary>
public static class CombatMath
{
    /// <summary>
    /// 定点数基准：1.000 = 1000 (100%)
    /// </summary>
    public const int FP_ONE = 1000;

    /// <summary>
    /// 定点数乘法（带四舍五入）
    /// </summary>
    public static int Mul(int a, int b)
    {
        long product = (long)a * b;
        return (int)((product >= 0 ? product + (FP_ONE / 2) : product - (FP_ONE / 2)) / FP_ONE);
    }

    /// <summary>
    /// 定点数除法（带四舍五入）
    /// </summary>
    public static int Div(int a, int b)
    {
        if (b == 0) throw new DivideByZeroException("CombatMath.Div: 除数不能为零");
        long numerator = (long)a * FP_ONE;
        return (int)((numerator >= 0 ? numerator + (b / 2) : numerator - (b / 2)) / b);
    }

    /// <summary>
    /// 分段双曲软钳制 (Soft-Cap)
    /// 1. 当 raw <= soft 时：保持恒等 (无任何精度损耗)
    /// 2. 当 raw > soft 时：使用双曲渐近逼近硬上限 cap
    /// </summary>
    /// <param name="raw">原始累加值（千分比）</param>
    /// <param name="soft">软阈值点（千分比）</param>
    /// <param name="cap">硬钳制上限（千分比）</param>
    public static int SoftCap(int raw, int soft, int cap)
    {
        if (raw <= soft) return raw;
        if (soft >= cap) return cap;

        long delta = raw - soft;
        long range = cap - soft;
        long bonus = (range * delta) / (delta + range);
        return (int)(soft + bonus);
    }

    /// <summary>
    /// 依据受击方等级计算护甲常数 K
    /// K = 800 + 20 * Level (Lv35 基线恰好对应 1500)
    /// </summary>
    public static int ComputeArmorK(int targetLevel)
    {
        return 800 + 20 * System.Math.Max(1, targetLevel);
    }

    /// <summary>
    /// 计算护甲最终伤害乘数（Damage Multiplier，千分比）
    /// multiplier = K / (K + effectiveArmor)
    /// </summary>
    /// <param name="effectiveArmor">经穿透计算后的有效护甲值</param>
    /// <param name="k">护甲等级常数</param>
    public static int ComputeArmorDamageMultiplier(int effectiveArmor, int k)
    {
        if (effectiveArmor <= 0) return FP_ONE;
        return Div(k, k + effectiveArmor);
    }

    /// <summary>
    /// 兼容别名
    /// </summary>
    public static int ArmorDamageRatio(int effectiveArmor, int k) => ComputeArmorDamageMultiplier(effectiveArmor, k);

    /// <summary>
    /// 计算穿甲后的有效护甲
    /// effectiveArmor = max(0, armor * (1 - penRatio))
    /// </summary>
    /// <param name="armor">受击者初始护甲</param>
    /// <param name="penPermille">攻击者穿透率（千分比，例如 300 表示 30% 穿透）</param>
    public static int ComputeEffectiveArmor(int armor, int penPermille)
    {
        if (armor <= 0) return 0;
        int clampedPen = System.Math.Clamp(penPermille, 0, FP_ONE);
        int remainingRatio = FP_ONE - clampedPen;
        return Mul(armor, remainingRatio);
    }

    /// <summary>
    /// 混合 DOT 伤害公式（每跳）
    /// TickDamage = TargetMaxHp * pct + AttackerAtk * 0.20
    /// </summary>
    public static int ComputeDotDamage(int targetMaxHp, int pctPermille, int attackerAtk)
    {
        int hpPart = Mul(targetMaxHp, pctPermille);
        int atkPart = Mul(attackerAtk, 200); // 20%
        return System.Math.Max(1, hpPart + atkPart);
    }

    /// <summary>
    /// 控制效果在 5s 窗口内的递减抗性计算
    /// 0次: 100% | 1次: 50% | 2次: 25% | >=3次: 0% (完全免疫)
    /// </summary>
    /// <param name="baseDurationTicks">基础持续帧数 (以 60Hz 帧为单位)</param>
    /// <param name="ccCountInWindow">5s 窗口期内已遭受同类硬控次数</param>
    public static int ComputeCrowdControlDuration(int baseDurationTicks, int ccCountInWindow)
    {
        return ccCountInWindow switch
        {
            0 => baseDurationTicks,
            1 => baseDurationTicks / 2,
            2 => baseDurationTicks / 4,
            _ => 0 // 免疫
        };
    }
}
