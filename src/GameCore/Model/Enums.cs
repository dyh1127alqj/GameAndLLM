namespace GameCore.Model;

/// <summary>
/// 六大标准职业（D-26 定案）
/// </summary>
public enum Profession
{
    Guardian = 0, // 守卫（前排承伤、反伤、退火抗性）
    Striker  = 1, // 强袭（近战爆发、吸血、斩杀破甲）
    Mystic   = 2, // 秘术（远程法术、减CD、施法回怒）
    Ranger   = 3, // 游猎（物理远程、急所破甲、弹射穿透）
    Shadow   = 4, // 暗影（刺客突击、孤立背刺、直接斩杀）
    Support  = 5  // 支援（后排救治、护盾屏障、全队免退火）
}

/// <summary>
/// 阵营归属
/// </summary>
public enum Faction
{
    Player = 0,
    Enemy  = 1
}

/// <summary>
/// 敌我单位阶级（D-27 定案）
/// </summary>
public enum Rank
{
    Normal = 0,
    Elite  = 1,
    Boss   = 2
}

/// <summary>
/// 伤害类型
/// </summary>
public enum DamageType
{
    Physical = 0, // 物理伤害（受护甲计算抵扣）
    Element  = 1  // 元素伤害（受抗性/五行摆幅计算）
}

/// <summary>
/// 伤害计算复合标记 (位掩码)
/// </summary>
[Flags]
public enum DamageFlags
{
    None        = 0,
    Direct      = 1 << 0, // 直接伤害（普攻/直接技能）
    Dot         = 1 << 1, // 持续伤害（毒/灼烧/流血）
    Reflected   = 1 << 2, // 反弹伤害（防循环吸血/反伤）
    Crit        = 1 << 3, // 暴击
    Executed    = 1 << 4, // 触发斩杀（<12% 直接判定）
    ShieldAbsorb= 1 << 5  // 护盾抵扣
}

/// <summary>
/// 战术索敌与技能目标模式
/// </summary>
public enum TargetMode
{
    Nearest            = 0, // 最近目标（优先同行/列）
    LowestHpPercentage = 1, // 当前血量百分比最低
    LowestHpAbsolute   = 2, // 绝对生命值最低
    HighestAtk         = 3, // 攻击力最高
    Backline           = 4, // 敌方后排优先
    LowestDef          = 5  // 防御/护甲最低
}

/// <summary>
/// Gambit 战术指令触发条件（数据驱动强类型）
/// </summary>
public enum ConditionType
{
    Always               = 0, // 无条件匹配
    SelfHpBelow          = 1, // 自身血量百分比低于 P0（千分比，例如 400 表示 40%）
    AllyHpBelow          = 2, // 任意友军血量低于 P0
    TargetIsEliteOrBoss  = 3, // 目标为精英或首领
    TargetHpBelow        = 4, // 目标血量百分比低于 P0
    EnemyCountAbove      = 5  // 存活敌方人数大于 P0
}

/// <summary>
/// Gambit 战术指令动作
/// </summary>
public enum ActionType
{
    DefaultAttack    = 0, // 按当前常规索敌普攻
    FocusTarget      = 1, // 锁定特定目标（按 A0 指定 TargetMode）
    HoldUltimate     = 2, // 暂扣大招（提高 RageCap 至 150）
    CastSkill        = 3, // 立即释放战技/大招
    ProtectLowestAlly= 4  // 救援/护盾最低友军
}

/// <summary>
/// 状态效果类型（控制、持续伤害、增益）
/// </summary>
public enum StatusType
{
    Stun       = 0, // 眩晕（无法行动、打断充能）
    Freeze     = 1, // 冻结（无法行动）
    Burn       = 2, // 灼烧（元素 DOT）
    Poison     = 3, // 中毒（最大生命百分比混合 DOT）
    Bleed      = 4, // 流血（物理 DOT）
    Invincible = 5, // 无敌（免疫一切伤害与减益）
    Stealth    = 6, // 隐匿（无法被单体非 AOE 技能锁定）
    Taunt      = 7, // 嘲讽（强制普攻锁定施法者）
    Shield     = 8  // 护盾（吸收固定量伤害）
}
