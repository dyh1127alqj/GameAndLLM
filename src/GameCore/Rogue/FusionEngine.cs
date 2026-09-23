using System.Collections.Generic;
using System.Linq;

namespace GameCore.Rogue;

public sealed record FusionRecipe(
    string RecipeId,
    string MaterialAId,
    string MaterialBId,
    AffixDefinition ResultDefinition
);

public static class FusionEngine
{
    private static readonly List<FusionRecipe> _recipes = new();
    private static readonly HashSet<string> _unlockedRecipeIds = new();

    static FusionEngine()
    {
        RegisterDefaultRecipes();
    }

    private static void RegisterDefaultRecipes()
    {
        _recipes.Add(new FusionRecipe(
            "recipe_super_01",
            "shushan_swords_01",
            "cyber_blade_01",
            new AffixDefinition(
                "super_nano_flying_sword",
                "高频纳米飞剑",
                SlotType.Core | SlotType.Combat,
                new[] { AffixTag.Shushan, AffixTag.Cyber, AffixTag.Striker },
                "概念级超武：贯穿全场，直接伤害附加 35% 破甲与撕裂"
            )
        ));

        _recipes.Add(new FusionRecipe(
            "recipe_super_02",
            "shushan_dan_01",
            "cyber_fusion_core_01",
            new AffixDefinition(
                "super_golden_core_reactor",
                "赛博金丹热能炉",
                SlotType.Core | SlotType.Combat,
                new[] { AffixTag.Shushan, AffixTag.Cyber, AffixTag.Guardian },
                "概念级超武：开局全队获得 30% 护盾，受击反弹 25% 真实伤害"
            )
        ));

        _recipes.Add(new FusionRecipe(
            "recipe_super_03",
            "shushan_mind_sword_01",
            "cyber_railgun_01",
            new AffixDefinition(
                "super_causal_sniper",
                "概念级因果律狙击",
                SlotType.Core | SlotType.Combat,
                new[] { AffixTag.Shushan, AffixTag.Cyber, AffixTag.Ranger },
                "概念级超武：首击必定暴击，对敌方后排造成 300% 贯穿打击"
            )
        ));
    }

    public static bool CanFuse(AffixInstance a, AffixInstance b)
    {
        if (a.CurrentTier != AffixTier.Tier3 || b.CurrentTier != AffixTier.Tier3) return false;
        return _recipes.Any(r =>
            (r.MaterialAId == a.Definition.Id && r.MaterialBId == b.Definition.Id) ||
            (r.MaterialAId == b.Definition.Id && r.MaterialBId == a.Definition.Id));
    }

    public static AffixInstance? TryFuse(AffixInstance a, AffixInstance b)
    {
        if (!CanFuse(a, b)) return null;

        var recipe = _recipes.First(r =>
            (r.MaterialAId == a.Definition.Id && r.MaterialBId == b.Definition.Id) ||
            (r.MaterialAId == b.Definition.Id && r.MaterialBId == a.Definition.Id));

        _unlockedRecipeIds.Add(recipe.RecipeId);
        return new AffixInstance(recipe.ResultDefinition, AffixTier.Tier3);
    }

    public static bool TryFuse(AffixInstance a, AffixInstance b, out AffixInstance? result)
    {
        result = TryFuse(a, b);
        return result != null;
    }

    public static bool IsRecipeUnlocked(string recipeId) => _unlockedRecipeIds.Contains(recipeId);
    public static bool IsUnlocked(string recipeId) => IsRecipeUnlocked(recipeId);
    public static IReadOnlyList<FusionRecipe> AllRecipes => _recipes;
}
