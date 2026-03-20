using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 对话推进被阻止时的提示 UI：订阅 DialogManager.OnAdvanceBlocked，显示文本并自动淡出。
/// </summary>
public class DialogAdvanceBlockHintUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private DialogManager dialogManager;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text hintText;

    [Header("Timing")]
    [Min(0f)][SerializeField] private float fadeInDuration = 0.08f;
    [Min(0f)][SerializeField] private float holdDuration = 1.0f;
    [Min(0f)][SerializeField] private float fadeOutDuration = 0.25f;

    private Coroutine _co;

    private void Awake()
    {
        HideImmediate();
    }

    private void OnEnable()
    {
        if (dialogManager != null)
            dialogManager.OnAdvanceBlocked += OnAdvanceBlocked;
    }

    private void OnDisable()
    {
        if (dialogManager != null)
            dialogManager.OnAdvanceBlocked -= OnAdvanceBlocked;
    }

    private void OnAdvanceBlocked(string hint)
    {
        if (hintText != null)
            hintText.text = hint ?? "";
        var gv = GlobalVariables.Instance;
        if (!gv.GetBool("g_GazeTutorialActive"))
            return ;

        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(ShowThenFade());
    }

    private IEnumerator ShowThenFade()
    {
        if (canvasGroup == null)
            yield break;

        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        yield return FadeAlpha(canvasGroup.alpha, 1f, fadeInDuration);
        if (holdDuration > 0f) yield return new WaitForSeconds(holdDuration);
        yield return FadeAlpha(canvasGroup.alpha, 0f, fadeOutDuration);

        _co = null;
    }

    private IEnumerator FadeAlpha(float from, float to, float duration)
    {
        if (canvasGroup == null)
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
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }
}

