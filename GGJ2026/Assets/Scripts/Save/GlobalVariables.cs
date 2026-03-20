using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局变量系统：内存键值表（bool/int/string），供剧本条件分支等读取与写入。
/// 持久化通过实现 ISaveSegmentProvider 由 SaveManager 统一存读档，本类不直接接触文件。
/// </summary>
public class GlobalVariables : ISaveSegmentProvider
{
    public static GlobalVariables Instance { get; } = new GlobalVariables();

    private readonly Dictionary<string, bool> _bools = new Dictionary<string, bool>();
    private readonly Dictionary<string, int> _ints = new Dictionary<string, int>();
    private readonly Dictionary<string, string> _strings = new Dictionary<string, string>();

    public string SegmentKey => "globalVariables";

    public bool GetBool(string key, bool defaultValue = false)
    {
        return _bools.TryGetValue(key, out var v) ? v : defaultValue;
    }

    public void SetBool(string key, bool value)
    {
        _bools[key] = value;
    }

    public int GetInt(string key, int defaultValue = 0)
    {
        return _ints.TryGetValue(key, out var v) ? v : defaultValue;
    }

    public void SetInt(string key, int value)
    {
        _ints[key] = value;
    }

    public string GetString(string key, string defaultValue = "")
    {
        return _strings.TryGetValue(key, out var v) ? v : defaultValue;
    }

    public void SetString(string key, string value)
    {
        _strings[key] = value;
    }

    public object ExportState()
    {
        var state = new GlobalVariablesState();
        foreach (var kv in _bools)
            state.bools.Add(new StringBoolPair { key = kv.Key, value = kv.Value });
        foreach (var kv in _ints)
            state.ints.Add(new StringIntPair { key = kv.Key, value = kv.Value });
        foreach (var kv in _strings)
            state.strings.Add(new StringStringPair { key = kv.Key, value = kv.Value });
        return state;
    }

    public void ImportState(object state)
    {
        _bools.Clear();
        _ints.Clear();
        _strings.Clear();

        if (state is not GlobalVariablesState s) return;

        if (s.bools != null)
            foreach (var p in s.bools)
                if (!string.IsNullOrEmpty(p.key))
                    _bools[p.key] = p.value;
        if (s.ints != null)
            foreach (var p in s.ints)
                if (!string.IsNullOrEmpty(p.key))
                    _ints[p.key] = p.value;
        if (s.strings != null)
            foreach (var p in s.strings)
                if (!string.IsNullOrEmpty(p.key))
                    _strings[p.key] = p.value ?? "";
    }

    public Type GetStateType() => typeof(GlobalVariablesState);
}
