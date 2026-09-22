using GameCore.Math;
using Xunit;

namespace GameCore.Tests;

public class CombatMathTests
{
    [Fact]
    public void Mul_Div_ShouldPreservePermillePrecision()
    {
        // 1000 * 1000 = 1000 (1.0 * 1.0 = 1.0)
        Assert.Equal(1000, CombatMath.Mul(1000, 1000));
        
        // 500 * 500 = 250 (0.5 * 0.5 = 0.25)
        Assert.Equal(250, CombatMath.Mul(500, 500));

        // 1000 / 2 = 500
        Assert.Equal(500, CombatMath.Div(500, 1000));
        
        // 1.67 元素增伤 (1670) * 1000 = 1670
        Assert.Equal(1670, CombatMath.Mul(1000, 1670));
    }

    [Fact]
    public void SoftCap_BelowSoft_ShouldBeExactIdentity()
    {
        // 减免率上限 750 (75%)，软阈值 600 (60%)
        // 当 raw = 500 (50%) 时，属于 <= soft，必须 100% 恒等保真
        int raw = 500;
        int soft = 600;
        int cap = 750;

        int finalVal = CombatMath.SoftCap(raw, soft, cap);
        Assert.Equal(500, finalVal);
    }

    [Fact]
    public void SoftCap_AboveSoft_ShouldAsymptoticallyApproachCap()
    {
        int soft = 600;
        int cap = 750;

        // raw = 750 时，超越 soft 150，按双曲计算不应超过 cap
        int v1 = CombatMath.SoftCap(750, soft, cap);
        Assert.True(v1 > 600 && v1 < 750);
        Assert.Equal(675, v1); // soft + (150 * 150)/(150 + 150) = 600 + 75 = 675

        // raw = 2000 (极其膨胀的数值)
        int v2 = CombatMath.SoftCap(2000, soft, cap);
        Assert.True(v2 < cap);
        Assert.True(v2 > 730);
    }

    [Fact]
    public void ComputeArmorK_ShouldScaleLinearlyWithLevel()
    {
        // Level 1: 800 + 20 * 1 = 820
        Assert.Equal(820, CombatMath.ComputeArmorK(1));

        // Level 35: 800 + 20 * 35 = 1500 (基准对齐)
        Assert.Equal(1500, CombatMath.ComputeArmorK(35));

        // Level 60: 800 + 20 * 60 = 2000
        Assert.Equal(2000, CombatMath.ComputeArmorK(60));
    }

    [Fact]
    public void CrowdControlDiminishingReturns_ShouldDecayCorrectly()
    {
        int baseTicks = 120; // 2.0s @ 60Hz

        Assert.Equal(120, CombatMath.ComputeCrowdControlDuration(baseTicks, 0)); // 100%
        Assert.Equal(60, CombatMath.ComputeCrowdControlDuration(baseTicks, 1));  // 50%
        Assert.Equal(30, CombatMath.ComputeCrowdControlDuration(baseTicks, 2));  // 25%
        Assert.Equal(0, CombatMath.ComputeCrowdControlDuration(baseTicks, 3));   // 免疫
        Assert.Equal(0, CombatMath.ComputeCrowdControlDuration(baseTicks, 5));   // 免疫
    }
}
