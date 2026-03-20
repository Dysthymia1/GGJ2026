using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 存档管理器：单一 JSON 文件 + 可插拔存档段。负责路径、收集各段、序列化写盘与读盘分发。
/// 各子系统通过 ISaveSegmentProvider 注册，不直接接触文件。
/// 若场景中无 SaveManager，会在首帧前自动创建（见 EnsureRuntime）；也可在首场景中手动挂载以配置 fileName。
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [Header("Path")]
    [SerializeField] private string fileName = "save.json";

    private readonly List<ISaveSegmentProvider> _providers = new List<ISaveSegmentProvider>();
    private string _fullPath;

    // [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    // private static void EnsureRuntime()
    // {
    //     if (Instance != null) return;
    //     var go = new GameObject("SaveManager");
    //     go.AddComponent<SaveManager>();
    // }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _fullPath = Path.Combine(Application.persistentDataPath, fileName);

        RegisterDefaultProviders();
        Load();
    }

    private void OnApplicationQuit()
    {
        Save();
    }

    /// <summary> 注册存档段提供者；同一下键仅保留最后一次注册。 </summary>
    public void Register(ISaveSegmentProvider provider)
    {
        if (provider == null) return;
        for (int i = _providers.Count - 1; i >= 0; i--)
        {
            if (_providers[i].SegmentKey == provider.SegmentKey)
            {
                _providers.RemoveAt(i);
                break;
            }
        }
        _providers.Add(provider);
    }

    public void Unregister(string segmentKey)
    {
        for (int i = _providers.Count - 1; i >= 0; i--)
        {
            if (_providers[i].SegmentKey == segmentKey)
            {
                _providers.RemoveAt(i);
                break;
            }
        }
    }

    /// <summary> 向所有已注册提供者收集数据并写入 JSON 文件。 </summary>
    public void Save()
    {
        var data = new SaveData();
        foreach (var p in _providers)
        {
            object state = p.ExportState();
            string json = state != null ? JsonUtility.ToJson(state) : "{}";
            data.segments.Add(new SegmentEntry { key = p.SegmentKey, json = json });
        }

        string content = JsonUtility.ToJson(data, prettyPrint: true);
        try
        {
            File.WriteAllText(_fullPath, content);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SaveManager] Save failed: {ex.Message}");
        }
    }

    /// <summary> 从 JSON 文件读取并分发给各提供者；文件不存在或失败时对各段传 null。 </summary>
    public void Load()
    {
        if (!File.Exists(_fullPath))
        {
            foreach (var p in _providers)
                p.ImportState(null);
            return;
        }

        string content;
        try
        {
            content = File.ReadAllText(_fullPath);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SaveManager] Load read failed: {ex.Message}");
            foreach (var p in _providers)
                p.ImportState(null);
            return;
        }

        SaveData data;
        try
        {
            data = JsonUtility.FromJson<SaveData>(content);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SaveManager] Load parse failed: {ex.Message}");
            foreach (var p in _providers)
                p.ImportState(null);
            return;
        }

        if (data?.segments == null)
        {
            foreach (var p in _providers)
                p.ImportState(null);
            return;
        }

        foreach (var p in _providers)
        {
            SegmentEntry entry = null;
            for (int i = 0; i < data.segments.Count; i++)
            {
                if (data.segments[i].key == p.SegmentKey)
                {
                    entry = data.segments[i];
                    break;
                }
            }

            if (entry == null || string.IsNullOrEmpty(entry.json))
            {
                p.ImportState(null);
                continue;
            }

            try
            {
                Type type = p.GetStateType();
                object state = JsonUtility.FromJson(entry.json, type);
                p.ImportState(state);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveManager] Load segment '{p.SegmentKey}' failed: {ex.Message}");
                p.ImportState(null);
            }
        }
    }

    /// <summary> 启动时注册默认提供者（如全局变量）；可在此处扩展。 </summary>
    private void RegisterDefaultProviders()
    {
        Register(GlobalVariables.Instance);
    }
}
