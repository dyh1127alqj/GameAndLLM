namespace GameCore.Model;

/// <summary>
/// 纯数据驱动 Gambit 战术指令四元组
/// (ConditionType, P0, ActionType, A0)
/// 杜绝 C# Func/Action 委托，完美支持 JSON 序列化与离线存档校验
/// </summary>
public readonly record struct TacticsInstruction(
    ConditionType Condition,
    int ConditionParam, // P0: 阈值参数（如血量千分比 400 = 40%，敌人数 3 等）
    ActionType Action,
    int ActionParam     // A0: 动作参数（如 TargetMode 枚举强转）
)
{
    /// <summary>
    /// 空默认指令（无条件默认索敌攻击）
    /// </summary>
    public static readonly TacticsInstruction Default = new(
        ConditionType.Always,
        0,
        ActionType.DefaultAttack,
        0
    );
}
