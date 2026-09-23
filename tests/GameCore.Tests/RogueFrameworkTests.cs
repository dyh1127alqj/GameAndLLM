using System.Collections.Generic;
using System.Linq;
using GameCore.Model;
using GameCore.Rogue;
using Xunit;

namespace GameCore.Tests;

public class RogueFrameworkTests
{
    [Fact]
    public void Backpack_RefineAndCapacity_ShouldWork()
    {
        var bp = new BackpackService();
        Assert.Equal(15, bp.Capacity);

        var def = new AffixDefinition("test_sword", "铁剑", SlotType.Combat, new[] { AffixTag.Striker });
        var a = new AffixInstance("inst_1", def, AffixTier.Tier1);
        var b = new AffixInstance("inst_2", def, AffixTier.Tier1);

        bp.AddItem(a);
        bp.AddItem(b);
        Assert.Equal(2, bp.Count);

        var ok = bp.Refine(a, b, out var upgraded);
        Assert.True(ok);
        Assert.Equal(AffixTier.Tier2, upgraded!.CurrentTier);
        Assert.Equal(1, bp.Count);
    }

    [Fact]
    public void Synergy_6ClassAnd2Blood_ShouldActivate()
    {
        var teamSockets = new Dictionary<int, VesselSockets>();
        var guardianDef = new AffixDefinition("guard_core", "护卫核心", SlotType.Core, new[] { AffixTag.Guardian, AffixTag.Shushan });

        for (int i = 1; i <= 5; i++)
        {
            var s = new VesselSockets(i);
            s.CoreSlot1 = new AffixInstance(guardianDef);
            if (i == 1) s.CoreSlot2 = new AffixInstance(guardianDef);
            teamSockets[i] = s;
        }

        var result = SynergyEvaluator.Evaluate(teamSockets);
        Assert.Equal(5, result.ActiveSynergies.Count);
        Assert.True(result.IsSynergyActive(AffixTag.Guardian, 2));
        Assert.True(result.IsSynergyActive(AffixTag.Shushan, 2));
    }

    [Fact]
    public void Fusion_Tier3CrossWorldItems_ShouldProduceSuperWeapon()
    {
        var runState = new RogueRunState(42);
        var defA = new AffixDefinition("shushan_swords_01", "蜀山万剑诀", SlotType.Combat, new[] { AffixTag.Shushan });
        var defB = new AffixDefinition("cyber_blade_01", "赛博纳米刃", SlotType.Combat, new[] { AffixTag.Cyber });

        var matA = new AffixInstance(defA, AffixTier.Tier3);
        var matB = new AffixInstance(defB, AffixTier.Tier3);

        var ok = FusionEngine.TryFuse(runState, matA, matB, out var superWeapon);
        Assert.True(ok);
        Assert.NotNull(superWeapon);
        Assert.Equal("super_nano_flying_sword", superWeapon!.Definition.Id);
        Assert.True(FusionEngine.IsUnlocked(runState, "recipe_super_01"));
        Assert.Contains("recipe_super_01", runState.UnlockedRecipeIds);
    }

    [Fact]
    public void Fusion_TwoIndependentRunStates_ShouldHaveIsolatedUnlockedRecipes()
    {
        var stateA = new RogueRunState(1);
        var stateB = new RogueRunState(2);

        var defA = new AffixDefinition("shushan_swords_01", "万剑诀", SlotType.Combat, new[] { AffixTag.Shushan });
        var defB = new AffixDefinition("cyber_blade_01", "纳米刃", SlotType.Combat, new[] { AffixTag.Cyber });

        var matA = new AffixInstance(defA, AffixTier.Tier3);
        var matB = new AffixInstance(defB, AffixTier.Tier3);

        // 仅在 A 中合成解锁
        var ok = FusionEngine.TryFuse(stateA, matA, matB, out _);
        Assert.True(ok);

        // A 已经解锁
        Assert.True(FusionEngine.IsUnlocked(stateA, "recipe_super_01"));
        Assert.Contains("recipe_super_01", stateA.UnlockedRecipeIds);

        // B 绝对不受静态字段污染，保持未解锁状态
        Assert.False(FusionEngine.IsUnlocked(stateB, "recipe_super_01"));
        Assert.DoesNotContain("recipe_super_01", stateB.UnlockedRecipeIds);
    }

    [Fact]
    public void Injury_StepUpFeeAndHealing_ShouldWork()
    {
        var runState = new RogueRunState(42) { Credits = 300, IsInSafehouse = true };
        var injuredUnit = UnitSnapshot.Create(1, "重伤单位", Faction.Player, Profession.Guardian, 1000, 100, 100, 100);
        runState.ActiveTeam.Add(injuredUnit);

        injuredUnit.CurrentHp = 0;
        InjuryService.ProcessBattleCasualties(runState);

        Assert.DoesNotContain(injuredUnit, runState.ActiveTeam);
        Assert.Contains(injuredUnit, runState.BenchTeam);
        Assert.True(injuredUnit.IsInjured);

        Assert.Equal(50, InjuryService.GetNextHealFee(runState));
        var healed = InjuryService.TryTreatInjury(runState, 1);
        Assert.True(healed);
        Assert.False(injuredUnit.IsInjured);
        Assert.Equal(250, runState.Credits);
        Assert.Equal(100, InjuryService.GetNextHealFee(runState));
    }

    [Fact]
    public void RogueMap_SeedDeterminism_ShouldMatch()
    {
        var map1 = RogueMapGenerator.Generate(42, 1);
        var map2 = RogueMapGenerator.Generate(42, 1);

        Assert.Equal(map1.Nodes.Count, map2.Nodes.Count);
        foreach (var pair in map1.Nodes)
        {
            var node1 = pair.Value;
            var node2 = map2.Nodes[pair.Key];
            Assert.Equal(node1.Type, node2.Type);
            Assert.Equal(node1.NextNodeIds, node2.NextNodeIds);
        }

        var bossNode = map1.AllNodes.First(n => n.Step == 5);
        Assert.Equal(RogueNodeType.Boss, bossNode.Type);
    }

    [Fact]
    public void RogueMap_EveryFloor_ShouldContainAtLeastOneRift()
    {
        for (int seed = 1; seed <= 5; seed++)
        {
            var map = RogueMapGenerator.Generate(seed, 1);
            Assert.Contains(map.AllNodes, n => n.Type == RogueNodeType.Rift);
        }
    }

    [Fact]
    public void SocketManager_SingularitySlot_ShouldRequireSlotTypeAndUnlockedCount()
    {
        var mgr = new SocketManager();
        var superAffix = new AffixInstance(new AffixDefinition("super_test", "测试超武", SlotType.Singularity), AffixTier.Tier3);
        
        // 默认 3 槽，尝试装配到 Slot 5 应该失败
        Assert.False(mgr.EquipAffix(1, 5, superAffix, isSafeNode: true));

        // 解锁至 6 槽后装配应该成功
        mgr.GetSockets(1).UnlockedSlotCount = 6;
        Assert.True(mgr.EquipAffix(1, 5, superAffix, isSafeNode: true));
        Assert.NotNull(mgr.GetSockets(1).SingularitySlot);
    }
}
