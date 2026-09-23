using System;

namespace GameCore.Rogue;

/// <summary>
/// D-25 定案：素体插槽类型位掩码
/// </summary>
[Flags]
public enum SlotType
{
    None        = 0,
    Core        = 1 << 0, // 核心槽（每人恒为 2，承载职业/血脉词条）
    Combat      = 1 << 1, // 作战槽（承载武器/伤害机制/触发技）
    General     = 1 << 2, // 通用槽（承载属性/生存/经济词条）
    Gold        = 1 << 3, // 黄金槽（预留）
    Singularity = 1 << 4  // 奇点槽（预留超武独占）
}
