using System;
using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// 对话打字机效果：在目标 TMP_Text 上逐字显示，支持富文本（不把 TMP 标签当字符打出）。
/// </summary>
public class DialogTypewriter : MonoBehaviour
{
    [SerializeField] private float charInterval = 0.02f;

    private Coroutine _typingCo;
    private bool _isTyping;

    public bool IsTyping => _isTyping;
    public event Action OnComplete;

    /// <summary> 开始逐字显示；若 useTypewriter 为 false 则直接设置全文并返回。 </summary>
    public void StartTyping(string fullText, TMP_Text target, bool useTypewriter = true)
    {
        Stop();
        if (!target) return;

        target.richText = true;
        if (!useTypewriter)
        {
            target.text = fullText ?? "";
            OnComplete?.Invoke();
            return;
        }

        _typingCo = StartCoroutine(TypeLine(fullText ?? "", target));
    }

    /// <summary> 立即停止打字并补全当前全文到 target。 </summary>
    public void StopAndComplete(string fullText, TMP_Text target)
    {
        Stop();
        if (target != null)
            target.text = fullText ?? "";
        OnComplete?.Invoke();
    }

    public void Stop()
    {
        if (_typingCo != null)
        {
            StopCoroutine(_typingCo);
            _typingCo = null;
        }
        _isTyping = false;
    }

    private IEnumerator TypeLine(string text, TMP_Text targetText)
    {
        _isTyping = true;
        int visibleCount = TmpRichTextHelper.GetVisibleCharacterCount(text);

        for (int n = 1; n <= visibleCount; n++)
        {
            targetText.text = TmpRichTextHelper.GetVisiblePrefix(text, n);
            yield return new WaitForSeconds(charInterval);
        }

        targetText.text = text;
        _isTyping = false;
        _typingCo = null;
        OnComplete?.Invoke();
    }

    public void SetCharInterval(float interval) => charInterval = Mathf.Max(0f, interval);
}

/// <summary>
/// 用于打字机效果：只按“可见字符”推进，不把 TMP 标签当字符打出。
/// 支持 &lt;tag&gt; / &lt;tag=value&gt; 形式，不统计尖括号内的内容。
/// </summary>
public static class TmpRichTextHelper
{
    public static int GetVisibleCharacterCount(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        int count = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '<')
            {
                int close = text.IndexOf('>', i + 1);
                i = close > 0 ? close : text.Length - 1;
            }
            else
                count++;
        }
        return count;
    }

    public static string GetVisiblePrefix(string text, int n)
    {
        if (string.IsNullOrEmpty(text) || n <= 0) return "";
        int visible = 0;
        int lastEnd = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '<')
            {
                int close = text.IndexOf('>', i + 1);
                if (close < 0) { lastEnd = text.Length; break; }
                lastEnd = close + 1;
                i = close;
            }
            else
            {
                visible++;
                lastEnd = i + 1;
                if (visible >= n) break;
            }
        }
        return text.Substring(0, lastEnd);
    }
}
