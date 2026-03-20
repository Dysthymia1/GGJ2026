using System;

/// <summary>
/// 存档段提供者：参与统一存读档的子系统实现此接口，只负责导出/导入自身状态，不关心文件路径或 JSON 结构。
/// </summary>
public interface ISaveSegmentProvider
{
    /// <summary> 在整体 JSON 中该段对应的键（如 "globalVariables"）。 </summary>
    string SegmentKey { get; }

    /// <summary> 返回当前需要持久化的可序列化状态；须与 GetStateType() 一致，且可被 JsonUtility 序列化。 </summary>
    object ExportState();

    /// <summary> 根据反序列化后的 state 恢复本系统状态；state 为 null 或缺失时做默认初始化。 </summary>
    void ImportState(object state);

    /// <summary> ExportState() 返回值的类型，用于 Load 时反序列化 JSON。 </summary>
    Type GetStateType();
}
