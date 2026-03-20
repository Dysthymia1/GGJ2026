using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class BackgroundManager : MonoBehaviour
{
    [SerializeField] private Image bgImage;
    [SerializeField] private Sprite defaultBackground; // 可选：默认背景
    [SerializeField] private bool hideWhenNoBackground = true;

    private Coroutine co;

    public void SetBackground(Sprite sprite, bool fade, float duration)
    {
        if (bgImage == null) return;

        if (co != null) StopCoroutine(co);

        if (sprite == null)
        {
            if (hideWhenNoBackground)
            {

                bgImage.gameObject.SetActive(false);
            }
                
            else if (defaultBackground != null)
                bgImage.sprite = defaultBackground;

            return;
        }

        bgImage.gameObject.SetActive(true);

        if (!fade || duration <= 0f)
        {
            bgImage.sprite = sprite;
            var c = bgImage.color; c.a = 1f; bgImage.color = c;
            return;
        }

        co = StartCoroutine(FadeSwap(sprite, duration));
    }

    public void Clear(bool fade, float duration)
    {
        if (bgImage == null) return;
        if (co != null) StopCoroutine(co);

        if (!fade || duration <= 0f)
        {
            if (hideWhenNoBackground)
                bgImage.gameObject.SetActive(false);
            else if (defaultBackground != null)
                bgImage.sprite = defaultBackground;

            return;
        }

        co = StartCoroutine(FadeOutAndDisable(duration));
    }

    private IEnumerator FadeOutAndDisable(float duration)
    {
        bgImage.gameObject.SetActive(true);

        float t = 0f;
        Color c = bgImage.color;

        while (t < duration)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(1f, 0f, t / duration);
            bgImage.color = c;
            yield return null;
        }

        c.a = 1f;
        bgImage.color = c;

        if (hideWhenNoBackground)
            bgImage.gameObject.SetActive(false);
        else if (defaultBackground != null)
            bgImage.sprite = defaultBackground;

        co = null;
    }

    private IEnumerator FadeSwap(Sprite next, float duration)
    {
        float t = 0f;
        Color c = bgImage.color;

        while (t < duration)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(1f, 0f, t / duration);
            bgImage.color = c;
            yield return null;
        }

        bgImage.sprite = next;

        t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(0f, 1f, t / duration);
            bgImage.color = c;
            yield return null;
        }

        c.a = 1f;
        bgImage.color = c;
        co = null;
    }
}
