namespace GameCore.Model;

/// <summary>
/// 技能静态规则定义
/// </summary>
public sealed class SkillDefinition
{
    public required string SkillId { get; init; }
    public required string Name { get; init; }
    public bool IsUltimate { get; init; } // true 为怒气驱动本命大招，false 为法力驱动战技

    // 资源消耗
    public int ManaCost { get; init; } = 100; // 战技默认 100 法力
    public int RageCost { get; init; } = 100; // 大招默认 100 怒气

    // 技能数值
    public DamageType Type { get; init; } = DamageType.Physical;
    public DamageType DamageType => Type; // 兼容别名
    public int DamageRatioPermille { get; init; } = 1000; // 伤害倍率（千分比，1000 = 100% Atk）
    public int DamagePermille => DamageRatioPermille;    // 兼容别名
    public int BaseDamage { get; init; } = 0;             // 基础固伤
    public int HealRatioPermille { get; init; } = 0;      // 治疗倍率（千分比）
    public int BaseValue { get => HealRatioPermille; init => HealRatioPermille = value; } // 兼容别名
    public bool IsHeal => HealRatioPermille > 0;          // 是否为治疗技能
    public int ShieldAmount { get; init; } = 0;           // 护盾数值

    // 索敌与作用
    public TargetMode TargetMode { get; init; } = TargetMode.Nearest;
    public bool IsAoe { get; init; } = false;             // 是否为群体技能

    // 附带状态
    public StatusType? ApplyStatus { get; init; }
    public int StatusDurationTicks { get; init; } = 0;    // 状态持续帧数 (60Hz)

    public static SkillDefinition CreateDamageSkill(
        string id, string name, int damagePermille, DamageType type = DamageType.Physical,
        TargetMode targetMode = TargetMode.Nearest, bool isAoe = false, int manaCost = 100)
    {
        return new SkillDefinition
        {
            SkillId = id,
            Name = name,
            IsUltimate = false,
            ManaCost = manaCost,
            DamageRatioPermille = damagePermille,
            Type = type,
            TargetMode = targetMode,
            IsAoe = isAoe
        };
    }

    public static SkillDefinition CreateUltimate(
        string id, string name, int damagePermille, DamageType type = DamageType.Physical,
        TargetMode targetMode = TargetMode.Nearest, bool isAoe = false, int rageCost = 100)
    {
        return new SkillDefinition
        {
            SkillId = id,
            Name = name,
            IsUltimate = true,
            RageCost = rageCost,
            DamageRatioPermille = damagePermille,
            Type = type,
            TargetMode = targetMode,
            IsAoe = isAoe
        };
    }
}
