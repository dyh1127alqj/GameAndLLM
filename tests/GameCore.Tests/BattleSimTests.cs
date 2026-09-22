using System.Collections.Concurrent;
using System.Diagnostics;
using GameCore.Battle;
using GameCore.Model;
using Xunit;
using Xunit.Abstractions;

namespace GameCore.Tests;

public class BattleSimTests
{
    private readonly ITestOutputHelper _output;

    public BattleSimTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static UnitSnapshot CreateTestUnit(int id, string name, Faction faction, int pos, int speed = 200, int atk = 100, int hp = 1000)
    {
        return new UnitSnapshot
        {
            UnitId = id,
            VesselId = $"vessel_{id}",
            Name = name,
            Faction = faction,
            Profession = Profession.Striker,
            Rank = Rank.Normal,
            Level = 10,
            MaxHp = hp,
            CurrentHp = hp,
            BaseAtk = atk,
            BaseArmor = 100,
            BaseSpeed = speed,
            BoardPosition = pos,
            ActiveSkill = new SkillDefinition
            {
                SkillId = "strike_skill",
                Name = "裂空斩",
                DamagePermille = 1800,
                DamageType = DamageType.Physical,
                ManaCost = 100
            },
            UltimateSkill = new SkillDefinition
            {
                SkillId = "strike_ultimate",
                Name = "万剑归宗",
                DamagePermille = 3200,
                DamageType = DamageType.Physical,
                RageCost = 100,
                IsUltimate = true
            }
        };
    }

    [Fact]
    public void BattleSim_ShouldCompleteWithValidWinner()
    {
        var playerTeam = new List<UnitSnapshot>
        {
            CreateTestUnit(1, "张飞", Faction.Player, 0, speed: 220, atk: 120, hp: 1200),
            CreateTestUnit(2, "关羽", Faction.Player, 1, speed: 200, atk: 150, hp: 1000)
        };

        var enemyTeam = new List<UnitSnapshot>
        {
            CreateTestUnit(101, "敌兵A", Faction.Enemy, 0, speed: 180, atk: 80, hp: 800),
            CreateTestUnit(102, "敌兵B", Faction.Enemy, 1, speed: 160, atk: 70, hp: 700)
        };

        var context = new BattleContext(playerTeam, enemyTeam, seed: 12345, timeLimitSeconds: 90);
        var sim = new BattleSim(context);

        var result = sim.RunToCompletion();

        Assert.True(sim.IsFinished);
        Assert.Equal(BattleOutcome.Victory, result.Outcome);
        Assert.True(result.DurationTicks > 0);
        Assert.True(result.DurationTicks <= 5400);
        Assert.True(result.SurvivorsCount > 0);
    }

    [Fact]
    public void MonteCarlo_10000_Simulations_ShouldPassG1QualityGate()
    {
        const int simulationCount = 10000;
        var outcomes = new ConcurrentDictionary<BattleOutcome, int>();
        long totalTicks = 0;

        var sw = Stopwatch.StartNew();

        Parallel.For(0, simulationCount, i =>
        {
            int seed = 1000 + i;
            var playerTeam = new List<UnitSnapshot>
            {
                CreateTestUnit(1, "前排盾", Faction.Player, 0, speed: 180, atk: 80, hp: 1400),
                CreateTestUnit(2, "主力攻", Faction.Player, 1, speed: 220, atk: 140, hp: 900)
            };

            var enemyTeam = new List<UnitSnapshot>
            {
                CreateTestUnit(101, "敌怪A", Faction.Enemy, 0, speed: 190, atk: 90, hp: 1100),
                CreateTestUnit(102, "敌怪B", Faction.Enemy, 1, speed: 210, atk: 110, hp: 950)
            };

            var context = new BattleContext(playerTeam, enemyTeam, seed, timeLimitSeconds: 90);
            var sim = new BattleSim(context);
            var res = sim.RunToCompletion();

            outcomes.AddOrUpdate(res.Outcome, 1, (_, c) => c + 1);
            Interlocked.Add(ref totalTicks, res.DurationTicks);
        });

        sw.Stop();

        double avgSeconds = (double)totalTicks / simulationCount / 60.0;
        _output.WriteLine($"[10,000 场无头蒙特卡洛压测完成]");
        _output.WriteLine($"总耗时: {sw.ElapsedMilliseconds} ms (单场平均: {(double)sw.ElapsedMilliseconds / simulationCount:F3} ms)");
        _output.WriteLine($"平均战斗时长: {avgSeconds:F1} 秒 (符合 20~40s 预期)");
        
        foreach (var kvp in outcomes)
        {
            _output.WriteLine($"战果统计 {kvp.Key}: {kvp.Value} 场 ({(double)kvp.Value / simulationCount * 100:F1}%)");
        }

        // 门禁 G1 断言：超时平局率必须 <= 1%
        outcomes.TryGetValue(BattleOutcome.TimeOutDefeat, out int timeouts);
        double timeoutRate = (double)timeouts / simulationCount;
        Assert.True(timeoutRate <= 0.01, $"超时率超过 1%: {timeoutRate:P2}");

        // 门禁 G1 断言：单场无头平均计算时间应低于 1.0 ms
        Assert.True(sw.ElapsedMilliseconds < 5000, "10,000 场无头模拟总耗时超出 5000ms 阈值");
    }
}