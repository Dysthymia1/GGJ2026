using System.Collections.Generic;
using UnityEngine;

// ----- 分组结构：Inspector 中可折叠，减少单行平铺字段 -----

/// <summary> 台词与说话人、头像显隐。 </summary>
[System.Serializable]
public class DialogLineContent
{
    public string speaker;

    [Header("Portrait (Character Slots)")]
    [Tooltip("左侧立绘使用的角色ID（对应 CharacterDatabase 中的 id）；为空则沿用上一行")]
    public string leftCharacterId;
    [Tooltip("右侧立绘使用的角色ID（对应 CharacterDatabase 中的 id）；为空则沿用上一行")]
    public string rightCharacterId;

    [Tooltip("可选：左侧使用的姿势ID（映射到 CharacterEntry.poses）")]
    public string leftPoseId;
    [Tooltip("可选：右侧使用的姿势ID（映射到 CharacterEntry.poses）")]
    public string rightPoseId;

    [Tooltip("是否为本行单独指定左侧立绘偏移量")]
    public bool overrideLeftOffset;
    public Vector2 leftOffset;

    [Tooltip("是否为本行单独指定右侧立绘偏移量")]
    public bool overrideRightOffset;
    public Vector2 rightOffset;

    [Tooltip("支持 TextMeshPro 富文本：<b>粗体</b> <i>斜体</i> <color=#FF0000>颜色</color> <size=24>字号</size>")]
    [TextArea(2, 6)]
    public string normalText;

    [Tooltip("支持 TextMeshPro 富文本（长按空格等特殊模式下显示）")]
    [TextArea(2, 6)]
    public string specialText;

    public bool showLeftPortrait;
    public bool showRightPortrait;
}

/// <summary> 单行可选标签：用于通过 tag 跳转到该行。 </summary>
[System.Serializable]
public class DialogLineTag
{
    [Tooltip("可选标签：在分支/选项中可通过 targetTag 跳转到该行")]
    public string tag;
}

/// <summary> 本行播放时写入的全局变量；可配置多个。 </summary>
[System.Serializable]
public class VariableAssignment
{
    [Tooltip("全局变量键名")]
    public string key = "";
    public VariableValueType valueType = VariableValueType.Bool;
    public bool boolValue = true;
    public int intValue;
    public string stringValue = "";
}

public enum VariableValueType { Bool, Int, String }

/// <summary> BGM、SFX、背景等媒体与场景。 </summary>
[System.Serializable]
public class DialogLineMedia
{
    // [Header("SFX")]
    public AudioClip sfx;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    public bool stopSfxBeforePlay = false;

    // [Header("BGM")]
    [Tooltip("勾选后本行会修改 BGM；不勾选则保持当前 BGM（跨行/跨剧本保持）。")]
    public bool applyBgm = false;

    public enum BgmAction { Play, Stop }
    [Tooltip("Play：使用下方 bgm 参数播放/切换；Stop：显式停止当前 BGM（忽略 bgm）。")]
    public BgmAction bgmAction = BgmAction.Play;

    [Tooltip("是否对本次 BGM 动作使用淡入淡出（切换曲目时为交叉淡化）。")]
    public bool bgmUseFade = false;
    [Min(0f)] public float bgmFadeOut = 0.5f;
    [Min(0f)] public float bgmFadeIn = 0.5f;

    public AudioClip bgm;
    [Range(0f, 1f)] public float bgmVolume = 1f;
    public bool bgmLoop = true;

    [Header("Background")]
    public Sprite background;
    public bool fadeBackground = true;
    [Min(0f)] public float bgFadeDuration = 0.3f;
}

/// <summary> 本行播放时执行的变量写入；供分支条件等后续读取。 </summary>
[System.Serializable]
public class DialogLineVariables
{
    [Tooltip("进入本行时依次写入的全局变量")]
    public List<VariableAssignment> setOnLine = new List<VariableAssignment>();
}

/// <summary> 本行是否可播放的前置条件列表；全部满足时才会播放本行。 </summary>
[System.Serializable]
public class DialogLinePreconditions
{
    [Tooltip("若为空则总是可播放；所有条件都满足时才播放本行")]
    public List<DialogLineCondition> conditions = new List<DialogLineCondition>();
}

/// <summary> 条件分支（switch-case-default）：按 cases 顺序匹配全局变量，第一个满足则跳转该 case 的 target，否则走 defaultTarget。 </summary>
[System.Serializable]
public class DialogLineBranch
{
    public bool isBranchPoint;
    public List<DialogBranchCase> cases = new List<DialogBranchCase>();
    [Tooltip("无 case 匹配时使用；默认 -1 表示下一行")]
    public DialogBranchTarget defaultTarget = new DialogBranchTarget { targetLineIndex = -1 };
}

/// <summary> 可视化分支选项配置：本行为选项行时使用。 </summary>
[System.Serializable]
public class DialogLineChoices
{
    [Tooltip("勾选后本行在文本播放完毕时进入“选择状态”，显示下方选项按钮。")]
    public bool isChoiceLine;

    [Tooltip("从上到下显示的选项列表")]
    public List<DialogChoiceOption> options = new List<DialogChoiceOption>();
}

/// <summary>
/// 单行对话：内容、媒体、分支三块在 Inspector 中可折叠编辑。
/// 若已有旧版 DialogScript 资产（平铺字段），加载后每行的 content/media 会为空，需重新填写或运行迁移脚本。
/// </summary>
[System.Serializable]
public class DialogLine
{
    [Header("标签（可选，用于跳转）")]
    public DialogLineTag tag = new DialogLineTag();

    [Header("内容")]
    public DialogLineContent content = new DialogLineContent();

    [Header("媒体与场景")]
    public DialogLineMedia media = new DialogLineMedia();

    [Header("变量（本行播放时设置）")]
    public DialogLineVariables variables = new DialogLineVariables();

    [Header("前置条件（本行是否播放）")]
    public DialogLinePreconditions preconditions = new DialogLinePreconditions();

    [Header("分支（可选）")]
    public DialogLineBranch branch = new DialogLineBranch();

    [Header("选项（可选，可视化分支）")]
    public DialogLineChoices choices = new DialogLineChoices();

    // 兼容旧代码与外部引用：转发到 content / media
    public string speaker { get => content.speaker; set => content.speaker = value; }
    public string leftCharacterId { get => content.leftCharacterId; set => content.leftCharacterId = value; }
    public string rightCharacterId { get => content.rightCharacterId; set => content.rightCharacterId = value; }
    public string leftPoseId { get => content.leftPoseId; set => content.leftPoseId = value; }
    public string rightPoseId { get => content.rightPoseId; set => content.rightPoseId = value; }
    public bool overrideLeftOffset { get => content.overrideLeftOffset; set => content.overrideLeftOffset = value; }
    public Vector2 leftOffset { get => content.leftOffset; set => content.leftOffset = value; }
    public bool overrideRightOffset { get => content.overrideRightOffset; set => content.overrideRightOffset = value; }
    public Vector2 rightOffset { get => content.rightOffset; set => content.rightOffset = value; }
    public string normalText { get => content.normalText; set => content.normalText = value; }
    public string specialText { get => content.specialText; set => content.specialText = value; }
    public bool showLeftPortrait { get => content.showLeftPortrait; set => content.showLeftPortrait = value; }
    public bool showRightPortrait { get => content.showRightPortrait; set => content.showRightPortrait = value; }

    public AudioClip sfx { get => media.sfx; set => media.sfx = value; }
    public float sfxVolume { get => media.sfxVolume; set => media.sfxVolume = value; }
    public bool stopSfxBeforePlay { get => media.stopSfxBeforePlay; set => media.stopSfxBeforePlay = value; }
    public AudioClip bgm { get => media.bgm; set => media.bgm = value; }
    public float bgmVolume { get => media.bgmVolume; set => media.bgmVolume = value; }
    public bool bgmLoop { get => media.bgmLoop; set => media.bgmLoop = value; }
    public Sprite background { get => media.background; set => media.background = value; }
    public bool fadeBackground { get => media.fadeBackground; set => media.fadeBackground = value; }
    public float bgFadeDuration { get => media.bgFadeDuration; set => media.bgFadeDuration = value; }
}

[CreateAssetMenu(menuName = "Dialog/Dialog Script")]
public class DialogScript : ScriptableObject
{
    public List<DialogLine> lines = new List<DialogLine>();
}
