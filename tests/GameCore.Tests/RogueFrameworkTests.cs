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
        var defA = new AffixDefinition("shushan_swords_01", "蜀山万剑诀", SlotType.Combat, new[] { AffixTag.Shushan });
        var defB = new AffixDefinition("cyber_blade_01", "赛博纳米刃", SlotType.Combat, new[] { AffixTag.Cyber });

        var matA = new AffixInstance(defA, AffixTier.Tier3);
        var matB = new AffixInstance(defB, AffixTier.Tier3);

        var ok = FusionEngine.TryFuse(matA, matB, out var superWeapon);
        Assert.True(ok);
        Assert.NotNull(superWeapon);
        Assert.Equal("super_nano_flying_sword", superWeapon!.Definition.Id);
        Assert.True(FusionEngine.IsUnlocked("recipe_super_01"));
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
        Assert.Equal(130, InjuryService.GetNextHealFee(runState));
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
}
