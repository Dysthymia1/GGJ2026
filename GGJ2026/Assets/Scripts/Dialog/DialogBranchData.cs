using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary> 与全局变量比较的类型：用于分支条件与行前置条件。 </summary>
public enum VariableConditionType
{
    BoolTrue,
    BoolFalse,
    IntEquals,
    IntNotEquals,
    IntGreaterThan,
    IntGreaterOrEqual,
    IntLessThan,
    IntLessOrEqual,
    StringEquals,
    StringNotEmpty
}

/// <summary>
/// 跳转目标：某剧本的某行或带标签的行。
/// 空剧本表示当前剧本；特殊行号 -1=下一行，-2=结束对话。
/// 若 targetTag 非空，则优先通过 tag 查找目标行。
/// </summary>
[Serializable]
public class DialogBranchTarget
{
    [Tooltip("空 = 当前剧本")]
    public DialogScript targetScript;
    [Tooltip("行索引（0 起）；-1 = 下一行，-2 = 结束对话")]
    public int targetLineIndex;

    [Tooltip("若非空，优先按 tag 查找目标行；否则使用 targetLineIndex。")]
    public string targetTag;

    [Header("失败黑屏")]
    [Tooltip("勾选后在跳转前先播放一次失败黑屏，再执行跳转。")]
    public bool useFailBlackout;
}

/// <summary> 单条分支：条件（读全局变量）+ 满足时跳转目标。 </summary>
[Serializable]
public class DialogBranchCase
{
    public VariableConditionType conditionType = VariableConditionType.BoolTrue;

    [Tooltip("全局变量键名")]
    public string conditionVariableKey = "";

    [Header("比较值（按 conditionType 使用其一）")]
    public bool conditionBoolValue = true;
    public int conditionIntValue;
    public string conditionStringValue = "";

    [Header("满足时跳转")]
    public DialogBranchTarget target = new DialogBranchTarget();
}

/// <summary>
/// 行前置条件：决定“这一行是否可播放”。
/// 使用与 DialogBranchCase 相同的全局变量比较方式，但不携带跳转目标。
/// </summary>
[Serializable]
public class DialogLineCondition
{
    public VariableConditionType conditionType = VariableConditionType.BoolTrue;

    [Tooltip("全局变量键名")]
    public string conditionVariableKey = "";

    [Header("比较值（按 conditionType 使用其一）")]
    public bool conditionBoolValue = true;
    public int conditionIntValue;
    public string conditionStringValue = "";
}

/// <summary>
/// 单个可视化选项：显示文本 + 跳转目标 + 选中后写入的变量。
/// </summary>
[Serializable]
public class DialogChoiceOption
{
    [TextArea(1, 3)]
    [Tooltip("选项在 UI 上显示的文本")]
    public string text;

    [Header("跳转目标")]
    public DialogBranchTarget target = new DialogBranchTarget();

    [Header("选择后写入的全局变量")]
    public List<VariableAssignment> setOnChoose = new List<VariableAssignment>();
}
