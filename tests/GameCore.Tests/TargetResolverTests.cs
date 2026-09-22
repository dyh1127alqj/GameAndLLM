using GameCore.Model;
using GameCore.Targeting;
using Xunit;

namespace GameCore.Tests;

public sealed class TargetResolverTests
{
    private static UnitSnapshot CreateTestUnit(int id, Faction faction, int pos, int hp, int maxHp, int atk = 100, int armor = 50)
    {
        return new UnitSnapshot
        {
            UnitId = id,
            VesselId = $"v_{id}",
            Name = $"Unit_{id}",
            Faction = faction,
            Profession = Profession.Striker,
            Rank = Rank.Normal,
            BoardPosition = pos,
            MaxHp = maxHp,
            CurrentHp = hp,
            BaseAtk = atk,
            BaseArmor = armor,
            BaseSpeed = 100
        };
    }

    [Fact]
    public void NearestTarget_PrefersFrontline_AndSameColumn()
    {
        var tracker = new TargetLockTracker();
        var attacker = CreateTestUnit(1, Faction.Player, 2, 1000, 1000); // 玩家前排中路 (col 2)

        var enemyFrontCol0 = CreateTestUnit(10, Faction.Enemy, 0, 1000, 1000); // 前排 col 0
        var enemyFrontCol2 = CreateTestUnit(11, Faction.Enemy, 2, 1000, 1000); // 前排 col 2 (正对面)
        var enemyBackCol2  = CreateTestUnit(12, Faction.Enemy, 7, 1000, 1000); // 后排 col 2

        var all = new[] { attacker, enemyFrontCol0, enemyFrontCol2, enemyBackCol2 };

        var target = TargetResolver.ResolveSingleTarget(attacker, all, TargetMode.Nearest, tracker);
        Assert.NotNull(target);
        Assert.Equal(11, target.UnitId); // 应精确命中正对面同列前排
    }

    [Fact]
    public void TargetStickiness_PreventsSwitchingWhenHpFluctuates()
    {
        var tracker = new TargetLockTracker();
        var attacker = CreateTestUnit(1, Faction.Player, 2, 1000, 1000);

        var enemyA = CreateTestUnit(10, Faction.Enemy, 0, 500, 1000); // 50%
        var enemyB = CreateTestUnit(11, Faction.Enemy, 1, 490, 1000); // 49%

        var all = new[] { attacker, enemyA, enemyB };

        // 第一次根据最低血量索敌，应选 B (49%)
        var first = TargetResolver.ResolveSingleTarget(attacker, all, TargetMode.LowestHpPercentage, tracker);
        Assert.Equal(11, first!.UnitId);

        // 此时 enemyA 受到额外伤害掉到 30%，但只要未死亡且在黏性期内，attacker 不应震荡切走
        enemyA.CurrentHp = 300;
        var second = TargetResolver.ResolveSingleTarget(attacker, all, TargetMode.LowestHpPercentage, tracker);
        Assert.Equal(11, second!.UnitId); // 锁定依然黏在 11 上！
    }

    [Fact]
    public void StealthTarget_CannotBeSelected_UnlessAllAreStealthed()
    {
        var tracker = new TargetLockTracker();
        var attacker = CreateTestUnit(1, Faction.Player, 2, 1000, 1000);

        var enemyNormal = CreateTestUnit(10, Faction.Enemy, 0, 1000, 1000);
        var enemyStealth = CreateTestUnit(11, Faction.Enemy, 2, 200, 1000); // 残血但隐匿
        enemyStealth.IsStealthed = true;

        var all = new[] { attacker, enemyNormal, enemyStealth };

        // 尽管 enemyStealth 残血在正对面，但由于隐匿，不可被单体锁定
        var target = TargetResolver.ResolveSingleTarget(attacker, all, TargetMode.LowestHpPercentage, tracker);
        Assert.Equal(10, target!.UnitId); // 正常选择未隐匿的敌人
    }
}
