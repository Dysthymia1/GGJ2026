using System;
using UnityEngine;

/// <summary>
/// 高内聚音效系统：BGM 与 SFX 独立音轨，与剧本/游戏模式解耦。
/// 供剧本播放、按键触发、UI 按钮等按需调用。
/// 可挂场景单例，或通过引用注入；若需全局访问可勾选 Use Singleton。
/// </summary>
public class GameAudioManager : MonoBehaviour
{
    public static GameAudioManager Instance { get; private set; }

    [Header("Singleton（勾选后 UI/其他脚本可用 GameAudioManager.Instance?.PlaySFX(...) 调用）")]
    [SerializeField] private bool useSingleton = true;

    [Header("BGM - 独立音轨")]
    [Tooltip("BGM 主音源（旧字段，仍兼容）。若需要交叉淡化，会自动创建/使用第二条 BGM 音轨。")]
    [SerializeField] private AudioSource bgmSource;
    [Tooltip("可选：手动指定第二条 BGM 音轨用于交叉淡化；不指定则运行时自动创建。")]
    [SerializeField] private AudioSource bgmSourceSecondary;

    [Header("SFX 音源池 - 可同时播放多条")]
    [SerializeField] private AudioSource[] sfxPool = Array.Empty<AudioSource>();
    [Tooltip("池为空时使用此音源（多段 SFX 会叠加）")]
    [SerializeField] private AudioSource sfxFallback;

    [Header("持续型 SFX（如按住按键音，可中途停止）")]
    [SerializeField] private AudioSource continuousSfxSource;

    private AudioSource _bgmA;
    private AudioSource _bgmB;
    private bool _bgmAIsActive = true;
    private Coroutine _bgmFadeRoutine;

    private void Awake()
    {
        if (useSingleton)
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        InitBgmSourcesIfNeeded();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ---------- BGM ----------

    /// <summary> 播放 BGM，会替换当前 BGM。 </summary>
    public void PlayBGM(AudioClip clip, float volume = 1f, bool loop = true)
    {
        InitBgmSourcesIfNeeded();
        if (!_bgmA) return;

        StopBgmFadeIfRunning();

        // 即时切换：用活跃音轨播放，另一条直接停掉
        var active = GetActiveBgmSource();
        var inactive = GetInactiveBgmSource();
        if (inactive) inactive.Stop();

        active.clip = clip;
        active.volume = Mathf.Clamp01(volume);
        active.loop = loop;
        if (clip) active.Play();
    }

    /// <summary> 停止 BGM。 </summary>
    public void StopBGM()
    {
        InitBgmSourcesIfNeeded();
        StopBgmFadeIfRunning();

        var a = _bgmA;
        var b = _bgmB;
        if (a && a.isPlaying) a.Stop();
        if (b && b.isPlaying) b.Stop();
    }

    /// <summary>
    /// 交叉淡化切换 BGM：旧曲淡出同时新曲淡入。fadeOut/fadeIn 为 0 时退化为即时切换。
    /// </summary>
    public void PlayBGMFade(AudioClip clip, float volume = 1f, bool loop = true, float fadeOut = 0.5f, float fadeIn = 0.5f)
    {
        InitBgmSourcesIfNeeded();
        if (!_bgmA || !_bgmB)
        {
            PlayBGM(clip, volume, loop);
            return;
        }

        StopBgmFadeIfRunning();

        float targetVolume = Mathf.Clamp01(volume);
        float outDur = Mathf.Max(0f, fadeOut);
        float inDur = Mathf.Max(0f, fadeIn);

        // 目标曲为空：视为停止（但仍走淡出）
        if (!clip)
        {
            StopBGMFade(outDur);
            return;
        }

        var from = GetActiveBgmSource();
        var to = GetInactiveBgmSource();

        // 直接切换同一首：仅更新参数与音量（可选淡入淡出到目标音量）
        if (from && from.isPlaying && from.clip == clip)
        {
            from.loop = loop;
            if (outDur <= 0f && inDur <= 0f)
            {
                from.volume = targetVolume;
                return;
            }

            float startVol = from.volume;
            _bgmFadeRoutine = StartCoroutine(FadeVolume(from, startVol, targetVolume, Mathf.Max(outDur, inDur)));
            return;
        }

        to.clip = clip;
        to.loop = loop;
        to.volume = 0f;
        to.Play();

        if (outDur <= 0f && inDur <= 0f)
        {
            if (from) from.Stop();
            to.volume = targetVolume;
            SwapActiveBgmSource();
            return;
        }

        _bgmFadeRoutine = StartCoroutine(CrossFade(from, to, targetVolume, outDur, inDur));
    }

    /// <summary> 淡出并停止当前 BGM。fadeOut 为 0 时立即停止。 </summary>
    public void StopBGMFade(float fadeOut = 0.5f)
    {
        InitBgmSourcesIfNeeded();
        if (!_bgmA) return;

        StopBgmFadeIfRunning();

        float outDur = Mathf.Max(0f, fadeOut);
        if (outDur <= 0f)
        {
            StopBGM();
            return;
        }

        var active = GetActiveBgmSource();
        var inactive = GetInactiveBgmSource();
        if (inactive) inactive.Stop();
        if (!active || !active.isPlaying) return;

        float startVol = active.volume;
        _bgmFadeRoutine = StartCoroutine(FadeOutAndStop(active, startVol, outDur));
    }

    /// <summary> 当前是否有 BGM 在播。 </summary>
    public bool IsBGMPlaying
    {
        get
        {
            InitBgmSourcesIfNeeded();
            return (_bgmA != null && _bgmA.isPlaying) || (_bgmB != null && _bgmB.isPlaying);
        }
    }

    // ---------- SFX 一次性 ----------

    /// <summary> 播放一次 SFX（从池中取空闲音源或叠加），与 BGM 互不覆盖。 </summary>
    /// <param name="stopBeforePlay">为 true 时先停止该音源上当前播放再播（仅对池内音源有效）</param>
    public void PlaySFX(AudioClip clip, float volume = 1f, bool stopBeforePlay = false)
    {
        if (!clip) return;
        AudioSource src = GetAvailableSfxSource();
        if (!src) return;
        if (stopBeforePlay) src.Stop();
        src.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    // ---------- 持续型 SFX（按下播放、松开停止） ----------

    /// <summary> 开始播放持续型 SFX（如按住空格反馈音），会覆盖当前持续 SFX。 </summary>
    public void StartContinuousSFX(AudioClip clip, float volume = 1f)
    {
        if (continuousSfxSource == null || clip == null) return;
        continuousSfxSource.clip = clip;
        continuousSfxSource.volume = Mathf.Clamp01(volume);
        continuousSfxSource.loop = true;
        continuousSfxSource.Play();
    }

    /// <summary> 停止持续型 SFX。 </summary>
    public void StopContinuousSFX()
    {
        if (continuousSfxSource && continuousSfxSource.isPlaying)
            continuousSfxSource.Stop();
    }

    /// <summary> 持续型 SFX 是否在播。 </summary>
    public bool IsContinuousSFXPlaying => continuousSfxSource != null && continuousSfxSource.isPlaying;

    // ---------- 内部 ----------

    private void InitBgmSourcesIfNeeded()
    {
        if (_bgmA && _bgmB) return;

        _bgmA = bgmSource;
        if (!_bgmA) return;

        _bgmB = bgmSourceSecondary;
        if (!_bgmB)
        {
            _bgmB = gameObject.AddComponent<AudioSource>();
            _bgmB.playOnAwake = false;
            _bgmB.outputAudioMixerGroup = _bgmA.outputAudioMixerGroup;
            _bgmB.spatialBlend = _bgmA.spatialBlend;
            _bgmB.rolloffMode = _bgmA.rolloffMode;
            _bgmB.minDistance = _bgmA.minDistance;
            _bgmB.maxDistance = _bgmA.maxDistance;
            _bgmB.dopplerLevel = _bgmA.dopplerLevel;
            _bgmB.spread = _bgmA.spread;
            _bgmB.panStereo = _bgmA.panStereo;
            _bgmB.pitch = _bgmA.pitch;
            _bgmB.reverbZoneMix = _bgmA.reverbZoneMix;
            _bgmB.priority = _bgmA.priority;
            _bgmB.mute = _bgmA.mute;
            _bgmB.bypassEffects = _bgmA.bypassEffects;
            _bgmB.bypassListenerEffects = _bgmA.bypassListenerEffects;
            _bgmB.bypassReverbZones = _bgmA.bypassReverbZones;
        }
    }

    private AudioSource GetActiveBgmSource() => _bgmAIsActive ? _bgmA : _bgmB;
    private AudioSource GetInactiveBgmSource() => _bgmAIsActive ? _bgmB : _bgmA;

    private void SwapActiveBgmSource()
    {
        _bgmAIsActive = !_bgmAIsActive;
    }

    private void StopBgmFadeIfRunning()
    {
        if (_bgmFadeRoutine != null)
        {
            StopCoroutine(_bgmFadeRoutine);
            _bgmFadeRoutine = null;
        }
    }

    private System.Collections.IEnumerator CrossFade(AudioSource from, AudioSource to, float targetVolume, float fadeOut, float fadeIn)
    {
        float fromStart = from ? from.volume : 0f;
        float t = 0f;
        float dur = Mathf.Max(fadeOut, fadeIn, 0.0001f);

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / dur);

            if (from)
            {
                float outP = fadeOut <= 0f ? 1f : Mathf.Clamp01(t / fadeOut);
                from.volume = Mathf.Lerp(fromStart, 0f, outP);
            }

            if (to)
            {
                float inP = fadeIn <= 0f ? 1f : Mathf.Clamp01(t / fadeIn);
                to.volume = Mathf.Lerp(0f, targetVolume, inP);
            }

            yield return null;
        }

        if (from) from.Stop();
        if (to) to.volume = targetVolume;
        SwapActiveBgmSource();
        _bgmFadeRoutine = null;
    }

    private System.Collections.IEnumerator FadeOutAndStop(AudioSource src, float startVolume, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            if (src) src.volume = Mathf.Lerp(startVolume, 0f, p);
            yield return null;
        }
        if (src) src.Stop();
        _bgmFadeRoutine = null;
    }

    private System.Collections.IEnumerator FadeVolume(AudioSource src, float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            if (src) src.volume = to;
            _bgmFadeRoutine = null;
            yield break;
        }
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            if (src) src.volume = Mathf.Lerp(from, to, p);
            yield return null;
        }
        if (src) src.volume = to;
        _bgmFadeRoutine = null;
    }

    private AudioSource GetAvailableSfxSource()
    {
        if (sfxPool != null && sfxPool.Length > 0)
        {
            foreach (var t in sfxPool)
            {
                if (t && !t.isPlaying)
                    return t;
            }

            return sfxPool[0];
        }
        return sfxFallback;
    }
}
