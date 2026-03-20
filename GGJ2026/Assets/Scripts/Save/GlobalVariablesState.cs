using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局变量持久化状态：可被 JsonUtility 序列化，用于 SaveManager 的 Export/Import。
/// </summary>
[Serializable]
public class GlobalVariablesState
{
    public List<StringBoolPair> bools = new List<StringBoolPair>();
    public List<StringIntPair> ints = new List<StringIntPair>();
    public List<StringStringPair> strings = new List<StringStringPair>();
}

[Serializable]
public class StringBoolPair
{
    public string key;
    public bool value;
}

[Serializable]
public class StringIntPair
{
    public string key;
    public int value;
}

[Serializable]
public class StringStringPair
{
    public string key;
    public string value;
}
