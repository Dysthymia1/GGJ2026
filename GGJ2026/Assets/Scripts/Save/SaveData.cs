using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 整体存档数据结构：版本号 + 各段（key → 已序列化为 JSON 的字符串），便于可插拔扩展，无需为每个新系统改根类型。
/// </summary>
[Serializable]
public class SaveData
{
    public int version = 1;
    public List<SegmentEntry> segments = new List<SegmentEntry>();
}

[Serializable]
public class SegmentEntry
{
    public string key;
    public string json;
}
