using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 在玩家获得移动权限时显示的提示 UI：支持淡入显示、淡出后停用。
/// </summary>
public class MovementHintUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text hintText;

    [Header("Timing")]
    [Min(0f)][SerializeField] private float fadeInDuration = 0.1f;
    [Min(0f)][SerializeField] private float fadeOutDuration = 0.3f;

    private Coroutine _co;

    private void Awake()
    {
        HideImmediate();
    }

    /// <summary>
    /// 立即显示（无动画），可选地更新提示文本。
    /// </summary>
    public void ShowImmediate(string text = null)
    {
        if (!string.IsNullOrEmpty(text) && hintText != null)
            hintText.text = text;

        if (canvasGroup == null)
            return;

        if (_co != null) StopCoroutine(_co);

        gameObject.SetActive(true);
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    /// <summary>
    /// 从隐藏状态淡入显示。
    /// </summary>
    public void ShowWithFade(string text = null)
    {
        if (!gameObject.activeInHierarchy) {
            gameObject.SetActive(true);
        }

        if (!string.IsNullOrEmpty(text) && hintText)
            hintText.text = text;

        if (!canvasGroup)
            return;

        if (_co != null) StopCoroutine(_co);
        gameObject.SetActive(true);
        _co = StartCoroutine(FadeAlpha(canvasGroup.alpha, 1f, fadeInDuration));
    }

    /// <summary>
    /// 淡出并在结束时停用 GameObject。
    /// </summary>
public void HideWithFadeAndDeactivate()
{
    // 如果自己都已经不激活/不启用，就不要再做任何事
    if (!isActiveAndEnabled)
        return;
    if (canvasGroup == null)
    {
        gameObject.SetActive(false);
        return;
    }
    if (_co != null) StopCoroutine(_co);
    _co = StartCoroutine(FadeAndDeactivate());
}

    private IEnumerator FadeAndDeactivate()
    {
        yield return FadeAlpha(canvasGroup.alpha, 0f, fadeOutDuration);
        _co = null;
        gameObject.SetActive(false);
    }

    private IEnumerator FadeAlpha(float from, float to, float duration)
    {
        if (!canvasGroup)
            yield break;

        if (duration <= 0f)
        {
            canvasGroup.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }
        canvasGroup.alpha = to;
    }

    private void HideImmediate()
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }
}

