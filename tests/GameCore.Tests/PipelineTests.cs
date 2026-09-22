using GameCore.Model;
using GameCore.Pipeline;
using Xunit;

namespace GameCore.Tests;

public class PipelineTests
{
    private static UnitSnapshot CreateTestUnit(int id, int hp, int armor, int atk = 100)
    {
        return UnitSnapshot.Create(
            unitId: id,
            name: $"Unit_{id}",
            faction: id <= 5 ? Faction.Player : Faction.Enemy,
            prof: Profession.Striker,
            maxHp: hp,
            atk: atk,
            armor: armor,
            speed: 100,
            pos: id
        );
    }

    [Fact]
    public void Shield_Absorbs_Damage_Fully()
    {
        var attacker = CreateTestUnit(1, 1000, 100, 100);
        var target = CreateTestUnit(6, 1000, 0);
        target.ShieldHp = 300;

        var result = DamagePipeline.CalculateAndApply(attacker, target, skillDamagePermille: 1000, DamageType.Physical);

        Assert.Equal(100, result.FinalDamage);
        Assert.Equal(100, result.ShieldAbsorbed);
        Assert.Equal(200, target.ShieldHp);
        Assert.Equal(1000, target.CurrentHp);
    }

    [Fact]
    public void Shield_Partially_Absorbs_And_Overflows()
    {
        var attacker = CreateTestUnit(1, 1000, 100, 200);
        var target = CreateTestUnit(6, 1000, 0);
        target.ShieldHp = 50;

        var result = DamagePipeline.CalculateAndApply(attacker, target, skillDamagePermille: 1000, DamageType.Physical);

        Assert.Equal(200, result.FinalDamage);
        Assert.Equal(50, result.ShieldAbsorbed);
        Assert.Equal(0, target.ShieldHp);
        Assert.Equal(850, target.CurrentHp);
    }

    [Fact]
    public void Hit_Increases_Rage_By_5()
    {
        var attacker = CreateTestUnit(1, 1000, 100, 50);
        var target = CreateTestUnit(6, 1000, 0);
        target.CurrentRage = 10;

        DamagePipeline.CalculateAndApply(attacker, target, skillDamagePermille: 1000, DamageType.Physical);

        Assert.Equal(15, target.CurrentRage);
    }

    [Fact]
    public void Execution_Triggers_Under_12_Percent_Hp()
    {
        var attacker = CreateTestUnit(1, 1000, 100, 50);
        var target = CreateTestUnit(6, 1000, 0);
        target.CurrentHp = 100; // 10%

        var result = DamagePipeline.CalculateAndApply(
            attacker, target, skillDamagePermille: 1000, DamageType.Physical,
            baseFlags: DamageFlags.Direct, hasExecutePower: true
        );

        Assert.True(result.Flags.HasFlag(DamageFlags.Executed));
        Assert.Equal(0, target.CurrentHp);
    }

    [Fact]
    public void Invincible_Blocks_All_Damage()
    {
        var attacker = CreateTestUnit(1, 1000, 100, 500);
        var target = CreateTestUnit(6, 1000, 0);
        target.IsInvincible = true;

        var result = DamagePipeline.CalculateAndApply(attacker, target, skillDamagePermille: 1000, DamageType.Physical);

        Assert.True(result.Flags.HasFlag(DamageFlags.Blocked));
        Assert.Equal(0, result.FinalDamage);
        Assert.Equal(1000, target.CurrentHp);
    }

    [Fact]
    public void HealPipeline_Applies_Dual_Multipliers()
    {
        var caster = CreateTestUnit(1, 1000, 100, 100);
        caster.HealingDonePermille = 1200; // +20%
        var target = CreateTestUnit(2, 1000, 100);
        target.CurrentHp = 500;
        target.HealingReceivedPermille = 1500; // +50%

        var result = HealPipeline.CalculateAndApply(caster, target, skillHealPermille: 1000);

        // raw = 100 * 1.0 = 100; withDone = 100 * 1.2 = 120; withRcv = 120 * 1.5 = 180
        Assert.Equal(180, result.FinalHeal);
        Assert.Equal(680, target.CurrentHp);
    }
}
