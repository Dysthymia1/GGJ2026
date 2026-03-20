using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CharacterEntry
{
    public string id;              // 剧本用的字符ID，如 "hero", "villain_01"
    public string displayName;     // UI显示的人名
    public Sprite defaultPortrait; // 默认立绘
    public Sprite[] poses;         // 可选：不同表情/姿势
    public Vector2 defaultOffset;  // 可选：在该槽位上的偏移
}
