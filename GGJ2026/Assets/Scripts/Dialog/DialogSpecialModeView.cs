using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 对话特殊模式视图：双 Printer 显隐切换、遮罩渐变、当前行名称/正文刷新。
/// </summary>
public class DialogSpecialModeView : MonoBehaviour
{
    [Header("Dual Printers")]
    [SerializeField] private GameObject normalPrinterRoot;
    [SerializeField] private GameObject specialPrinterRoot;
    [SerializeField] private TMP_Text normalNameText;
    [SerializeField] private TMP_Text normalContentText;
    [SerializeField] private TMP_Text specialNameText;
    [SerializeField] private TMP_Text specialContentText;

    [Header("Special Mask")]
    [SerializeField] private Image specialMaskImage;
    [SerializeField] private float maskAlphaWhenNormal = 0f;
    [SerializeField] private float maskAlphaWhenSpecial = 1f;
    [SerializeField] private float maskFadeDuration = 1f;

    [SerializeField] private DialogTypewriter typewriter;

    private Coroutine _maskFadeCo;
    private bool _specialActive;

    public bool UseDualPrinters => normalPrinterRoot != null && specialPrinterRoot != null;
    public TMP_Text CurrentContentText => _specialActive ? specialContentText : normalContentText;

    private void Awake()
    {
        if (normalContentText != null) normalContentText.richText = true;
        if (specialContentText != null) specialContentText.richText = true;
    }

    /// <summary> 进入普通模式：遮罩立即透明，显示 normal printer。 </summary>
    public void ResetToNormal()
    {
        _specialActive = false;
        if (_maskFadeCo != null) { StopCoroutine(_maskFadeCo); _maskFadeCo = null; }
        if (specialMaskImage != null)
        {
            var c = specialMaskImage.color;
            c.a = maskAlphaWhenNormal;
            specialMaskImage.color = c;
        }
        if (normalPrinterRoot != null) normalPrinterRoot.SetActive(true);
        if (specialPrinterRoot != null) specialPrinterRoot.SetActive(false);
    }

    /// <summary> 设置特殊模式开关；内部做遮罩渐变与双 Printer 切换。 </summary>
    public void SetMode(bool active)
    {
        if (_specialActive == active) return;
        _specialActive = active;

        if (specialMaskImage)
        {
            if (_maskFadeCo != null) StopCoroutine(_maskFadeCo);
            float targetAlpha = active ? maskAlphaWhenSpecial : maskAlphaWhenNormal;
            _maskFadeCo = StartCoroutine(FadeMaskAlpha(targetAlpha));
        }

        if (normalPrinterRoot) normalPrinterRoot.SetActive(!active);
        if (specialPrinterRoot) specialPrinterRoot.SetActive(active);
    }

    /// <summary> 刷新当前行：说话者、正文（普通/特殊），可选打字机。 </summary>
    public void SetLine(string speaker, string normalText, string specialText, bool useTypewriter)
    {
        if (UseDualPrinters)
        {
            if (normalNameText) normalNameText.text = speaker ?? "";
            if (specialNameText) specialNameText.text = speaker ?? "";
        }
        else
        {
            var nameText = _specialActive ? specialNameText : normalNameText;

            if (nameText) nameText.text = speaker ?? "";
        }
        
        string content = _specialActive && !string.IsNullOrWhiteSpace(specialText) ? specialText : (normalText ?? "");
        var contentTarget = CurrentContentText;

        if (typewriter && useTypewriter && contentTarget)
            typewriter.StartTyping(content, contentTarget, true);
        else if (contentTarget)
            contentTarget.text = content;
    }

    /// <summary> 立即停止打字并补全当前行正文。 </summary>
    public void StopTypingAndComplete(string fullText)
    {
        if (typewriter != null)
            typewriter.StopAndComplete(fullText, CurrentContentText);
        else if (CurrentContentText != null)
            CurrentContentText.text = fullText ?? "";
    }

    public bool IsTyping => typewriter != null && typewriter.IsTyping;

    private IEnumerator FadeMaskAlpha(float targetAlpha)
    {
        if (!specialMaskImage) { _maskFadeCo = null; yield break; }

        var c = specialMaskImage.color;
        float startAlpha = c.a;
        float elapsed = 0f;

        while (elapsed < maskFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / maskFadeDuration);
            c.a = Mathf.Lerp(startAlpha, targetAlpha, t);
            specialMaskImage.color = c;
            yield return null;
        }

        c.a = targetAlpha;
        specialMaskImage.color = c;
        _maskFadeCo = null;
    }
}
