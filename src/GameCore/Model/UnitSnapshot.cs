namespace GameCore.Model;

/// <summary>
/// 战斗单位实体运行时状态快照 (UnitSnapshot)
/// 严格自包含战斗计算所需的所有动态数值、状态掩码与战术配置
/// </summary>
public sealed class UnitSnapshot
{
    // 基础身份
    public required int UnitId { get; init; }
    public required string VesselId { get; init; }
    public required string Name { get; init; }
    public required Faction Faction { get; init; }
    public required Profession Profession { get; init; }
    public required Rank Rank { get; init; }
    public int Level { get; init; } = 1;

    // 棋盘布局位置 (0~9，共 2x5 格子；前排 0~4，后排 5~9)
    public int BoardPosition { get; set; }
    public bool IsBackline => BoardPosition >= 5;

    // 静态基准数值 (不可变基础底盘)
    public int MaxHp { get; init; }
    public int BaseAtk { get; init; }
    public int BaseArmor { get; init; }
    public int BaseSpeed { get; init; } // 行动条充能速度 (基准 100~200)
    public int PenPermille { get; init; } = 0; // 穿透率千分比 (1000 = 100%)

    // 动态资源条
    public int CurrentHp { get; set; }
    public int CurrentMana { get; set; } = 0;   // 法力 [0, 100]
    public int CurrentRage { get; set; } = 0;   // 怒气 [0, RageCap]
    public int RageCap { get; set; } = 100;     // 动态怒气上限 (Gambit 留大招时可升为 150)
    public int ActionGauge { get; set; } = 0;   // 行动条进度 [0, 1000]
    public int CurrentGauge { get => ActionGauge; set => ActionGauge = value; }

    // 治疗双独立乘区 (千分比，基准 1000 = 100%)
    public int HealingDonePermille { get; set; } = 1000;
    public int HealingReceivedPermille { get; set; } = 1000;

    // 扩展机制状态 (第二十一节)
    public int ShieldHp { get; set; } = 0;      // 护盾当前吸收量
    public bool IsInvincible { get; set; } = false; // 无敌状态
    public bool IsStealthed { get; set; } = false;  // 隐匿状态
    public int? TauntedByUnitId { get; set; } = null; // 被谁嘲讽
    public int StunTicksRemaining { get; set; } = 0; // 眩晕剩余帧数

    // 控制递减跟踪器 (5s 窗口期内控制次数)
    public int CcCountInWindow { get; set; } = 0;
    public int CcWindowCooldownTicks { get; set; } = 0; // 5s (300 ticks) 衰减计时器

    // 死亡标记
    public bool IsDead => CurrentHp <= 0;

    // 技能槽
    public SkillDefinition? ActiveSkill { get; set; }   // 插槽战技（法力驱动）
    public SkillDefinition? UltimateSkill { get; set; } // 本命大招（怒气驱动，不可更换）

    // 战术指令槽 (Gambit 2 槽)
    public TacticsInstruction TacticsSlot1 { get; set; } = TacticsInstruction.Default;
    public TacticsInstruction TacticsSlot2 { get; set; } = TacticsInstruction.Default;

    /// <summary>
    /// 便捷克隆方法（供无头蒙特卡洛与多局复用）
    /// </summary>
    public UnitSnapshot Clone()
    {
        return new UnitSnapshot
        {
            UnitId = this.UnitId,
            VesselId = this.VesselId,
            Name = this.Name,
            Faction = this.Faction,
            Profession = this.Profession,
            Rank = this.Rank,
            Level = this.Level,
            BoardPosition = this.BoardPosition,
            MaxHp = this.MaxHp,
            BaseAtk = this.BaseAtk,
            BaseArmor = this.BaseArmor,
            BaseSpeed = this.BaseSpeed,
            PenPermille = this.PenPermille,
            CurrentHp = this.MaxHp,
            CurrentMana = 0,
            CurrentRage = 0,
            RageCap = 100,
            ActionGauge = 0,
            ShieldHp = 0,
            HealingDonePermille = this.HealingDonePermille,
            HealingReceivedPermille = this.HealingReceivedPermille,
            IsInvincible = false,
            IsStealthed = false,
            TauntedByUnitId = null,
            StunTicksRemaining = 0,
            CcCountInWindow = 0,
            CcWindowCooldownTicks = 0,
            ActiveSkill = this.ActiveSkill,
            UltimateSkill = this.UltimateSkill,
            TacticsSlot1 = this.TacticsSlot1,
            TacticsSlot2 = this.TacticsSlot2
        };
    }

    public static UnitSnapshot Create(
        int unitId,
        string name,
        Faction faction,
        Profession profession,
        int maxHp,
        int baseAtk,
        int baseArmor,
        int baseSpeed,
        int boardPosition = 0,
        Rank rank = Rank.Normal,
        int level = 1,
        SkillDefinition? activeSkill = null,
        SkillDefinition? ultimateSkill = null)
    {
        return new UnitSnapshot
        {
            UnitId = unitId,
            VesselId = $"vessel_{unitId}",
            Name = name,
            Faction = faction,
            Profession = profession,
            Rank = rank,
            Level = level,
            BoardPosition = boardPosition,
            MaxHp = maxHp,
            CurrentHp = maxHp,
            BaseAtk = baseAtk,
            BaseArmor = baseArmor,
            BaseSpeed = baseSpeed,
            ActiveSkill = activeSkill,
            UltimateSkill = ultimateSkill
        };
    }
}
