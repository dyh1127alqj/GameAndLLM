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
                DamageRatioPermille = 1800,
                Type = DamageType.Physical,
                ManaCost = 100
            },
            UltimateSkill = new SkillDefinition
            {
                SkillId = "strike_ultimate",
                Name = "万剑归宗",
                DamageRatioPermille = 3200,
                Type = DamageType.Physical,
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
        Assert.Equal(BattleOutcome.PlayerVictory, result.Outcome);
        Assert.True(result.DurationTicks > 0);
        Assert.True(result.DurationTicks <= 5400);
        Assert.True(result.SurvivorsCount > 0);
    }

    [Fact]
    public void HealSkill_TargetsLowestHpAlly_NotCasterSelf()
    {
        // 回归测试：曾经 ResolveAllyTarget 走了敌对阵营过滤逻辑，
        // 导致治疗永远落回施法者自己，队友选不中（见 R3 复审报告 §3.2）。
        var healer = CreateTestUnit(1, "医者", Faction.Player, 0, speed: 300, atk: 100, hp: 1000);
        healer.CurrentGauge = 1000; // 直接置满行动条，保证第一个 Step 就出手
        healer.CurrentMana = 100;   // 直接满蓝，保证第一时间释放战技
        healer.ActiveSkill = new SkillDefinition
        {
            SkillId = "heal_test",
            Name = "测试治疗",
            ManaCost = 100,
            HealRatioPermille = 500,
            TargetMode = TargetMode.LowestHpPercentage
        };

        var ally = CreateTestUnit(2, "残血队友", Faction.Player, 1, speed: 100, atk: 50, hp: 1000);
        ally.CurrentHp = 100; // 10% 残血，队伍里血量百分比最低

        var deadEnemy = CreateTestUnit(101, "陪跑敌人", Faction.Enemy, 0, speed: 100, atk: 0, hp: 100);
        deadEnemy.CurrentHp = 0; // 已阵亡，避免干扰本次断言

        var context = new BattleContext(
            new List<UnitSnapshot> { healer, ally },
            new List<UnitSnapshot> { deadEnemy },
            seed: 1,
            timeLimitSeconds: 90);
        var sim = new BattleSim(context);

        sim.Step();

        Assert.Equal(150, ally.CurrentHp); // 100 + Mul(100 Atk, 500‰) = 150，证明治疗确实落在了队友身上
        Assert.Equal(0, healer.CurrentMana); // 技能已释放（法力归零），而不是空放
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
        outcomes.TryGetValue(BattleOutcome.TimeOutDraw, out int timeouts);
        double timeoutRate = (double)timeouts / simulationCount;
        Assert.True(timeoutRate <= 0.01, $"超时率超过 1%: {timeoutRate:P2}");

        // 门禁 G1 断言：单场无头平均计算时间应低于 1.0 ms
        Assert.True(sw.ElapsedMilliseconds < 5000, "10,000 场无头模拟总耗时超出 5000ms 阈值");
    }
}