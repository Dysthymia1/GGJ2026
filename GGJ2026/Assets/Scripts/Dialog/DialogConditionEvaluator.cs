using UnityEngine;

/// <summary>
/// 根据 DialogLine 的分支配置与全局变量求值，返回应跳转的目标（第一个匹配的 case 或 defaultTarget）。
/// 仅读 GlobalVariables，不写；与剧本数据、DialogManager 解耦。
/// </summary>
public class DialogConditionEvaluator
{
    /// <summary>
    /// 若当前行为分支行，按 cases 顺序求值，返回第一个满足的 case 的 target，否则返回 defaultTarget。
    /// 非分支行返回 null（调用方按“下一行/结束”处理）。
    /// </summary>
    public DialogBranchTarget EvaluateBranch(DialogLine line, DialogScript currentScript)
    {
        if (line?.branch == null || !line.branch.isBranchPoint)
            return null;

        if (line.branch.cases != null)
        {
            foreach (var c in line.branch.cases)
            {
                if (c?.target == null) continue;
                if (EvaluateCondition(c))
                    return c.target;
            }
        }

        return line.branch.defaultTarget;
    }

    /// <summary> 根据全局变量判断单条条件是否成立。 </summary>
    public bool EvaluateCondition(DialogBranchCase c)
    {
        if (c == null || string.IsNullOrEmpty(c.conditionVariableKey))
            return false;

        var gv = GlobalVariables.Instance;
        switch (c.conditionType)
        {
            case VariableConditionType.BoolTrue:
                return gv.GetBool(c.conditionVariableKey, false) == true;
            case VariableConditionType.BoolFalse:
                return gv.GetBool(c.conditionVariableKey, true) == false;
            case VariableConditionType.IntEquals:
                return gv.GetInt(c.conditionVariableKey, 0) == c.conditionIntValue;
            case VariableConditionType.IntNotEquals:
                return gv.GetInt(c.conditionVariableKey, 0) != c.conditionIntValue;
            case VariableConditionType.IntGreaterThan:
                return gv.GetInt(c.conditionVariableKey, 0) > c.conditionIntValue;
            case VariableConditionType.IntGreaterOrEqual:
                return gv.GetInt(c.conditionVariableKey, 0) >= c.conditionIntValue;
            case VariableConditionType.IntLessThan:
                return gv.GetInt(c.conditionVariableKey, 0) < c.conditionIntValue;
            case VariableConditionType.IntLessOrEqual:
                return gv.GetInt(c.conditionVariableKey, 0) <= c.conditionIntValue;
            case VariableConditionType.StringEquals:
                return (gv.GetString(c.conditionVariableKey, "") ?? "") == (c.conditionStringValue ?? "");
            case VariableConditionType.StringNotEmpty:
                return !string.IsNullOrEmpty(gv.GetString(c.conditionVariableKey, ""));
            default:
                return false;
        }
    }
}
