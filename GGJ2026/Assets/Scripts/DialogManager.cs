using System;
using System.Collections.Generic;
using Mask;
using UnityEngine;
using UnityEngine.EventSystems;


/// <summary>
/// 对话流程编排器：负责单次对话的播放、行切换、关闭与特殊模式切换。
/// 将 UI 展示（打字机、双 Printer、头像）委托给 DialogTypewriter、DialogSpecialModeView、DialogPortraitController；
/// 音效与背景由 GameAudioManager、BackgroundController 注入，在行切换/关闭时调用。
/// 多段对话连续播放请使用 DialogSequencePlayer。
/// </summary>
public class DialogManager : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("对话根节点，控制整体显隐")]
    public GameObject dialogRoot;

    [Header("Decoupled modules (assign in inspector)")]
    [SerializeField] private DialogSpecialModeView specialModeView;   // 双 Printer 切换、遮罩、当前行名称/正文
    [SerializeField] private DialogPortraitController portraitController;
    [SerializeField] private DialogTypewriter typewriter;
    [SerializeField] private MaskSystem maskSystem;                   // 面具凝视状态与能量，可选
    [SerializeField] private MaskEnergyUI maskEnergyUI;               // 面具能量 UI 根节点，可选
    [SerializeField] private DialogChoiceView choiceView;            // 选项按钮视图，可选

    private readonly DialogConditionEvaluator _branchEvaluator = new DialogConditionEvaluator();

    [Header("Typewriter")]
    [Tooltip("是否启用逐字打字效果")]
    public bool useTypewriter = true;
    [SerializeField] private float charInterval = 0.02f;

    [Header("Audio")]
    [SerializeField] private GameAudioManager audioManager;
    [Tooltip("关闭对话时是否停止 BGM。为支持跨剧本/序列保持，默认关闭。")]
    [SerializeField] private bool clearBgmOnClose = false;

    [Header("Background")]
    [SerializeField] private BackgroundManager bgManager;
    [SerializeField] private bool clearBackgroundOnClose = true;
    [SerializeField] private bool clearBackgroundFade = true;
    [SerializeField] private float clearBackgroundFadeDuration = 0.3f;

    [Header("Fail Blackout")]
    [SerializeField] private bool enableFailBlackout = true;
    [SerializeField] private float failBlackoutDuration = 1.5f;
    [Tooltip("全屏黑幕根节点：在失败黑屏期间启用")]
    [SerializeField] private GameObject failBlackoutRoot;

    [Header("Advance Gate")]
    [SerializeField] private string allowAdvanceKey = "g_AllowAdvance";
    [SerializeField] private bool allowAdvanceDefault = true;
    [SerializeField] private bool resetAllowAdvanceOnPlay = true;
    [SerializeField] private string allowAdvanceHintKey = "g_AllowAdvanceHint";
    [SerializeField][TextArea(1, 3)] private string allowAdvanceBlockedFallbackHint = "需要完成指定操作才能继续";

    public event Action<string> OnAdvanceBlocked;

    private enum DialogState
    {
        Playing,       // 正在打字或刷新当前行
        WaitingInput,  // 文本已补全，等待 Z 推进
        Choosing,      // 选项行，显示按钮中
        Blocking       // 阻塞状态（例如失败黑屏期间），禁止推进输入
    }

    private DialogState state = DialogState.Playing;

    private DialogScript current;   // 当前播放的剧本
    private int index;              // 当前行索引
    private Action onFinished;      // 播放结束回调
    private string fullLineText;    // 当前行完整文本（用于跳过打字时补全）

    /// <summary>
    /// 是否在播放“临时对话”（例如面具失败对话）期间抑制对 BGM/背景 的修改。
    /// 方案1：特殊剧本本身不影响环境，只输出文本/SFX。
    /// </summary>
    private bool suppressEnvironmentForTemporary;

    /// <summary>
    /// 对话状态栈：用于临时切换到其它剧本（例如面具失败对话）后再恢复当前对话。
    /// </summary>
    private readonly Stack<DialogStackFrame> dialogStack = new Stack<DialogStackFrame>();

    private struct DialogStackFrame
    {
        public DialogScript Script;
        public int LineIndex;
        public Action OnFinished;
    }

    private Coroutine failBlackoutCoroutine;

    private void Awake()
    {
        if (dialogRoot != null) dialogRoot.SetActive(false);
        if (portraitController != null) portraitController.HideAll();
        if (specialModeView != null) specialModeView.ResetToNormal();
        if (typewriter != null && charInterval > 0f) typewriter.SetCharInterval(charInterval);
        if (typewriter != null) typewriter.OnComplete += HandleTypingCompleted;
        if (maskSystem != null)
            maskSystem.OnGazeStateChanged += RefreshCurrentLineForGaze;
    }

    private void OnDestroy()
    {
        if (typewriter != null)
            typewriter.OnComplete -= HandleTypingCompleted;
        if (maskSystem != null)
            maskSystem.OnGazeStateChanged -= RefreshCurrentLineForGaze;
    }

    // private void Update()
    // {
    //     Debug.Log($"{EventSystem.current.currentSelectedGameObject}");
    //     if (IsOpen && EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
    //         EventSystem.current.SetSelectedGameObject(null);
    // }

    /// <summary> 对话面板是否已打开 </summary>
    public bool IsOpen => dialogRoot != null && dialogRoot.activeSelf;

    /// <summary> 当前是否处于选项选择状态（禁止 Z 推进和凝视）。 </summary>
    public bool IsInChoiceState => state == DialogState.Choosing;

    /// <summary>
    /// 面具失败专用入口：若当前没有对话，则直接播放；若有对话，则压栈后播放临时剧本。
    /// </summary>
    public void PlayMaskFail(DialogScript script)
    {
        if (!script) return;

        if (!IsOpen || current == null)
        {
            Play(script);
        }
        else
        {
            suppressEnvironmentForTemporary = true;
            // 临时剧本结束后先关闭环境抑制，再恢复原对话，这样恢复时环境按原行配置刷新。
            PushAndPlayTemporary(script, () => { suppressEnvironmentForTemporary = false; });
        }
    }

    /// <summary> 播放单段对话；结束时调用 finished 回调。 </summary>
    public void Play(DialogScript script, Action finished = null)
    {
        if (!script || script.lines == null || script.lines.Count == 0) return;

        if (resetAllowAdvanceOnPlay && !string.IsNullOrEmpty(allowAdvanceKey))
            GlobalVariables.Instance.SetBool(allowAdvanceKey, true);

        current = script;
        index = 0;
        StartPlayFromCurrent(finished);
    }

    /// <summary>
    /// 在已设置好 current / index 的前提下，从当前行开始播放对话。
    /// </summary>
    private void StartPlayFromCurrent(Action finishedOverride = null)
    {
        onFinished = finishedOverride;
        state = DialogState.Playing;

        if (dialogRoot != null)
            dialogRoot.SetActive(true);

        // 避免 UI 获得焦点后吃掉 Space 松开，导致 SpecialHold Canceled 不触发
        if (EventSystem.current)
            EventSystem.current.SetSelectedGameObject(null);

        ShowLine();
    }

    /// <summary>
    /// 将当前对话状态压栈，并从头播放一个临时剧本。临时剧本结束后自动恢复。
    /// </summary>
    public void PushAndPlayTemporary(DialogScript scriptToPlay, Action onTempFinished = null)
    {
        if (!scriptToPlay || scriptToPlay.lines == null || scriptToPlay.lines.Count == 0)
            return;

        // 保存当前对话上下文（如果有）
        if (current != null && IsOpen)
        {
            dialogStack.Push(new DialogStackFrame
            {
                Script = current,
                LineIndex = index,
                OnFinished = onFinished
            });
        }

        current = scriptToPlay;
        index = 0;

        // 临时剧本结束时，先执行其自定义回调，再恢复栈顶对话。
        void TempFinished()
        {
            onTempFinished?.Invoke();
            PopAndResume();
        }

        StartPlayFromCurrent(TempFinished);
    }

    /// <summary>
    /// 从栈中恢复之前的对话状态；若栈为空则关闭对话。
    /// </summary>
    private void PopAndResume()
    {
        if (dialogStack.Count == 0)
        {
            Close();
            return;
        }

        var frame = dialogStack.Pop();
        current = frame.Script;
        index = frame.LineIndex;
        onFinished = frame.OnFinished;
        state = DialogState.Playing;

        if (current == null || current.lines == null || current.lines.Count == 0)
        {
            Close();
            return;
        }

        if (dialogRoot != null)
            dialogRoot.SetActive(true);

        if (EventSystem.current)
            EventSystem.current.SetSelectedGameObject(null);

        ShowLine();
    }

    /// <summary>
    /// 播放一次失败黑屏：在指定时长内启用全屏黑幕并阻塞输入，结束后执行回调。
    /// </summary>
    private void PlayFailBlackoutAndThen(Action onFinished)
    {
        // 若未启用或未配置黑幕节点，则直接执行回调。
        if (!enableFailBlackout || failBlackoutDuration <= 0f || failBlackoutRoot == null)
        {
            onFinished?.Invoke();
            return;
        }

        // 若已有黑屏协程在运行，先停止并重置。
        if (failBlackoutCoroutine != null)
        {
            StopCoroutine(failBlackoutCoroutine);
            failBlackoutCoroutine = null;
        }

        failBlackoutCoroutine = StartCoroutine(FailBlackoutRoutine(onFinished));
    }

    private System.Collections.IEnumerator FailBlackoutRoutine(Action onFinished)
    {
        state = DialogState.Blocking;

        if (failBlackoutRoot != null && !failBlackoutRoot.activeSelf)
            failBlackoutRoot.SetActive(true);

        yield return new WaitForSeconds(failBlackoutDuration);

        if (failBlackoutRoot != null && failBlackoutRoot.activeSelf)
            failBlackoutRoot.SetActive(false);

        state = DialogState.Playing;

        failBlackoutCoroutine = null;
        onFinished?.Invoke();
    }

    /// <summary> 显示当前行：退出凝视、重置视图、头像、背景、音效、正文。 </summary>
    private void ShowLine()
    {
        if (!current 
            || current.lines == null 
            || current.lines.Count == 0 
            || !MoveToNextPlayableLine())
        {
            Close();
            return;
        }

        if (maskSystem) maskSystem.ExitGaze();
        if (specialModeView) specialModeView.ResetToNormal();
        if (choiceView) choiceView.Hide();
        state = DialogState.Playing;
        if (maskSystem) maskSystem.OnDialogLineChanged(current, index);

        // 根据全局变量控制 MaskEnergyUI 显隐
        bool showMaskEnergyUI = GlobalVariables.Instance.GetBool("showMaskEnergyUI");
        if (maskEnergyUI) maskEnergyUI.ToggleMaskEnergyUI(showMaskEnergyUI);

        var line = current.lines[index];

        ApplyLineVariables(line);

        if (portraitController) portraitController.SetPortrait(line);
        
        // 方案1：在“临时对话”（如 MaskFail）期间不改 BGM/背景，只允许播放 SFX。
        if (!suppressEnvironmentForTemporary)
        {
            if (bgManager)
                bgManager.SetBackground(line.background, line.fadeBackground, line.bgFadeDuration);

            if (audioManager && line?.media != null && line.media.applyBgm)
            {
                if (line.media.bgmAction == DialogLineMedia.BgmAction.Stop)
                {
                    if (line.media.bgmUseFade)
                        audioManager.StopBGMFade(line.media.bgmFadeOut);
                    else
                        audioManager.StopBGM();
                }
                else
                {
                    // Play / Switch
                    if (line.bgm)
                    {
                        if (line.media.bgmUseFade)
                            audioManager.PlayBGMFade(line.bgm, line.bgmVolume, line.bgmLoop, line.media.bgmFadeOut, line.media.bgmFadeIn);
                        else
                            audioManager.PlayBGM(line.bgm, line.bgmVolume, line.bgmLoop);
                    }
                }
            }
        }

        // 无论是否抑制环境，SFX 都可以播放，不影响 BGM。
        if (audioManager && line is not null && line.sfx)
            audioManager.PlaySFX(line.sfx, line.sfxVolume, line.stopSfxBeforePlay);

        bool gazeActive = maskSystem && maskSystem.IsGazeActive;
        fullLineText = ResolveLineText(line, gazeActive);
        if (specialModeView)
            specialModeView.SetLine(line.speaker, line.normalText ?? "", line.specialText ?? "", useTypewriter);
    }

    /// <summary> 将 index 推进到下一条可播放的行；若找不到则返回 false。 </summary>
    private bool MoveToNextPlayableLine()
    {
        if (!current || current.lines == null) return false;

        int safety = 0;
        int lineCount = current.lines.Count;
        while (index >= 0 && index < lineCount && safety <= lineCount)
        {
            var line = current.lines[index];
            if (ShouldPlayLine(line))
                return true;

            index++;
            safety++;
        }

        return false;
    }

    /// <summary> 凝视状态变化时刷新当前行正文（由 MaskSystem.OnGazeStateChanged 调用）。 </summary>
    private void RefreshCurrentLineForGaze(bool active)
    {
        if (!IsOpen || !current || index < 0 || index >= current.lines.Count) return;
        var line = current.lines[index];
        fullLineText = ResolveLineText(line, active);
        if (specialModeView)
            specialModeView.SetLine(line.speaker, line.normalText ?? "", line.specialText ?? "", useTypewriter);
    }

    /// <summary> 打字机完成当前行时，根据是否为选项行切换到等待输入或选择状态。 </summary>
    private void HandleTypingCompleted()
    {
        if (!IsOpen || !current || index < 0 || index >= current.lines.Count)
            return;

        var line = current.lines[index];
        bool hasChoices = line.choices is { isChoiceLine: true, options: { Count: > 0 } };

        if (hasChoices && choiceView)
        {
            state = DialogState.Choosing;
            choiceView.ShowOptions(line.choices.options, OnChoiceSelected);
        }
        else
        {
            if (state == DialogState.Playing)
                state = DialogState.WaitingInput;
        }
    }

    /// <summary> 本行播放时写入配置的全局变量。 </summary>
    private void ApplyLineVariables(DialogLine line)
    {
        if (line?.variables?.setOnLine == null) return;
        var gv = GlobalVariables.Instance;
        foreach (var a in line.variables.setOnLine)
        {
            if (string.IsNullOrEmpty(a.key)) continue;
            switch (a.valueType)
            {
                case VariableValueType.Bool:
                    gv.SetBool(a.key, a.boolValue);
                    break;
                case VariableValueType.Int:
                    gv.SetInt(a.key, a.intValue);
                    // 若剧本行中修改了面具能量相关变量，则同步到 MaskSystem，以驱动逻辑与 UI。
                    if (maskSystem != null)
                    {
                        if (a.key == "g_remainingEnergy")
                            maskSystem.SetRemainingEnergyExternal(a.intValue);
                        else if (a.key == "g_totalEnergy")
                            maskSystem.InitSession(a.intValue);
                    }
                    break;
                case VariableValueType.String: gv.SetString(a.key, a.stringValue ?? ""); break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

/// <summary> 行级前置条件：全部满足时才允许播放本行。未配置条件则总是可播放。 </summary>
private bool ShouldPlayLine(DialogLine line)
{
    if (line == null) return false;
    var pre = line.preconditions;
    if (pre?.conditions == null || pre.conditions.Count == 0)
        return true;
    var gv = GlobalVariables.Instance;
    foreach (var c in pre.conditions)
    {
        if (c == null || string.IsNullOrEmpty(c.conditionVariableKey))
            continue;
        bool ok = c.conditionType switch
        {
            VariableConditionType.BoolTrue => gv.GetBool(c.conditionVariableKey, false) == true,
            VariableConditionType.BoolFalse => gv.GetBool(c.conditionVariableKey, true) == false,
            VariableConditionType.IntEquals => gv.GetInt(c.conditionVariableKey, 0) == c.conditionIntValue,
            VariableConditionType.IntNotEquals => gv.GetInt(c.conditionVariableKey, 0) != c.conditionIntValue,
            VariableConditionType.IntGreaterThan => gv.GetInt(c.conditionVariableKey, 0) > c.conditionIntValue,
            VariableConditionType.IntGreaterOrEqual => gv.GetInt(c.conditionVariableKey, 0) >= c.conditionIntValue,
            VariableConditionType.IntLessThan => gv.GetInt(c.conditionVariableKey, 0) < c.conditionIntValue,
            VariableConditionType.IntLessOrEqual => gv.GetInt(c.conditionVariableKey, 0) <= c.conditionIntValue,
            VariableConditionType.StringEquals => (gv.GetString(c.conditionVariableKey, "") ?? "") ==
                                                  (c.conditionStringValue ?? ""),
            VariableConditionType.StringNotEmpty => !string.IsNullOrEmpty(gv.GetString(c.conditionVariableKey, "")),
            _ => false
        };
        if (!ok)
            return false;
    }
    
    return true;
}

    /// <summary> 根据是否特殊模式解析当前行应显示的文本（normalText / specialText）。 </summary>
    private string ResolveLineText(DialogLine line, bool special)
    {
        if (line == null) return "";

        if (special && !string.IsNullOrWhiteSpace(line.specialText))
            return line.specialText;

        return line.normalText ?? "";
    }

    /// <summary> Z 键按下：若正在打字则跳过并补全；若处于凝视状态则不推进；否则按分支或线性推进下一行/关闭。由 Interactor2D 在对话打开时调用。 </summary>
    public void OnInteractPressed()
    {
        if (!IsOpen || current == null) return;
        if (state == DialogState.Blocking) return;
        if (state == DialogState.Choosing) return;
        if (maskSystem != null && maskSystem.IsGazeActive) return;

        bool typing = specialModeView != null && specialModeView.IsTyping;
        if (typing)
        {
            if (specialModeView != null) specialModeView.StopTypingAndComplete(fullLineText);
            state = DialogState.WaitingInput;
            return;
        }

        if (!CanAdvance(out var blockedHint))
        {
            
            OnAdvanceBlocked?.Invoke(blockedHint);
            return;
        }

        var line = current.lines[index];
        var branchTarget = line?.branch != null && line.branch.isBranchPoint
            ? _branchEvaluator.EvaluateBranch(line, current)
            : null;

        if (branchTarget != null)
            ApplyBranchTarget(branchTarget);
        else
            AdvanceOrClose();
    }

    /// <summary> 应用分支跳转目标：跨剧本/行或下一行/结束。 </summary>
    private void ApplyBranchTarget(DialogBranchTarget target)
    {
        if (target == null)
        {
            AdvanceOrClose();
            return;
        }

        // 若标记为失败跳转且启用了黑屏，则先播放黑幕再执行真正的跳转。
        if (enableFailBlackout && target.useFailBlackout)
        {
            PlayFailBlackoutAndThen(() => JumpToBranchTarget(target));
            return;
        }

        JumpToBranchTarget(target);
    }

    /// <summary> 实际执行分支跳转：根据目标配置决定关闭、下一行或跳转到指定行。 </summary>
    private void JumpToBranchTarget(DialogBranchTarget target)
    {
        if (target == null)
        {
            AdvanceOrClose();
            return;
        }

        if (target.targetLineIndex == -2 && string.IsNullOrEmpty(target.targetTag))
        {
            Close();
            return;
        }

        if (target.targetLineIndex == -1 && string.IsNullOrEmpty(target.targetTag))
        {
            AdvanceOrClose();
            return;
        }

        var script = target.targetScript != null ? target.targetScript : current;
        if (script == null || script.lines == null || script.lines.Count == 0)
        {
            AdvanceOrClose();
            return;
        }

        int lineIndex = ResolveTargetLineIndex(target, script);
        if (lineIndex < 0 || lineIndex >= script.lines.Count)
        {
            AdvanceOrClose();
            return;
        }

        current = script;
        index = lineIndex;
        ShowLine();
    }

    /// <summary> 解析跳转目标行：若配置 targetTag 则优先按 tag 查找，否则使用 targetLineIndex。 </summary>
    private int ResolveTargetLineIndex(DialogBranchTarget target, DialogScript script)
    {
        if (target == null || !script || script.lines == null)
            return -1;

        if (string.IsNullOrEmpty(target.targetTag)) return target.targetLineIndex;
        for (int i = 0; i < script.lines.Count; i++)
        {
            var line = script.lines[i];
            if (line != null && line.tag != null &&
                !string.IsNullOrEmpty(line.tag.tag) &&
                line.tag.tag == target.targetTag)
            {
                return i;
            }
        }

        return -1;

    }

    /// <summary> 选中某个选项：写入选项变量并根据其跳转目标切换行。 </summary>
    private void OnChoiceSelected(int optionIndex)
    {
        if (!IsOpen || !current || index < 0 || index >= current.lines.Count)
            return;

        var line = current.lines[index];
        if (line.choices?.options == null ||
            optionIndex < 0 || optionIndex >= line.choices.options.Count)
            return;

        var option = line.choices.options[optionIndex];
        if (option == null)
            return;

        // 选择后写入变量
        if (option.setOnChoose != null && option.setOnChoose.Count > 0)
        {
            var gv = GlobalVariables.Instance;
            foreach (var a in option.setOnChoose)
            {
                if (a == null || string.IsNullOrEmpty(a.key)) continue;
                switch (a.valueType)
                {
                    case VariableValueType.Bool:
                        gv.SetBool(a.key, a.boolValue);
                        break;
                    case VariableValueType.Int:
                        gv.SetInt(a.key, a.intValue);
                        break;
                    case VariableValueType.String:
                        gv.SetString(a.key, a.stringValue ?? "");
                        break;
                }
            }
        }

        if (choiceView)
            choiceView.Hide();

        state = DialogState.Playing;

        if (option.target != null)
            ApplyBranchTarget(option.target);
        else
            AdvanceOrClose();
    }

    private void AdvanceOrClose()
    {
        if (index < current.lines.Count - 1)
        {
            index++;
            ShowLine();
        }
        else
        {
            Close();
        }
    }

    private bool CanAdvance(out string hint)
    {
        hint = allowAdvanceBlockedFallbackHint ?? "";

        if (string.IsNullOrEmpty(allowAdvanceKey))
            return true;

        var gv = GlobalVariables.Instance;
        if (gv.GetBool(allowAdvanceKey, allowAdvanceDefault))
            return true;

        if (!string.IsNullOrEmpty(allowAdvanceHintKey))
            hint = gv.GetString(allowAdvanceHintKey, hint) ?? hint;

        return false;
    }

    /// <summary> 关闭对话：停止打字、隐藏头像、清除 BGM/背景、执行 onFinished 回调。 </summary>
    public void Close()
    {
        // 若栈中仍有对话，说明当前关闭的是一个“临时对话”（例如 MaskFail），
        // 此时不应清理全局环境（BGM/背景），也不应真正关闭对话面板，由恢复逻辑接管。
        bool isClosingTemporary = dialogStack.Count > 0;

        // 终止黑屏协程并隐藏黑幕，避免残留。
        if (failBlackoutCoroutine != null)
        {
            StopCoroutine(failBlackoutCoroutine);
            failBlackoutCoroutine = null;
        }
        if (failBlackoutRoot != null && failBlackoutRoot.activeSelf)
            failBlackoutRoot.SetActive(false);
        if (state == DialogState.Blocking)
            state = DialogState.Playing;

        if (maskSystem) maskSystem.ExitGaze();
        if (typewriter) typewriter.Stop();
        if (portraitController) portraitController.HideAll();
        if (specialModeView) specialModeView.ResetToNormal();

        if (!isClosingTemporary)
        {
            if (clearBgmOnClose && audioManager)
                audioManager.StopBGM();
            if (clearBackgroundOnClose && bgManager)
                bgManager.Clear(clearBackgroundFade, clearBackgroundFadeDuration);

            if (dialogRoot != null)
                dialogRoot.SetActive(false);
        }

        var cb = onFinished;
        current = null;
        onFinished = null;
        cb?.Invoke();
    }

}