using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mask
{
    /// <summary>
    /// 面具模块核心：凝视状态机、能量消耗、输入协调；驱动 View / 音效，与 Dialog 通过事件与查询解耦。
    /// 进入凝视：仅通过 Input System 的 SpecialHold Performed（长按 0.4s）。退出凝视：优先用 SpecialHold Canceled；
    /// 若 Canceled 未触发（例如 UI 获得焦点吃掉松开事件），则在 Update 中检测 Space 松开作为兜底。
    /// </summary>
    public class MaskSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DialogManager dialogManager;
        [SerializeField] private DialogSpecialModeView specialModeView;
        [SerializeField] private PlayerController2D playerController;
        [SerializeField] private GameAudioManager audioManager;

        [Header("Gaze Audio（按下空格瞬间起播，松开即停）")]
        [SerializeField] private AudioClip gazeHoldClip;
        [SerializeField] [Range(0f, 1f)] private float gazeHoldVolume = 1f;

        [Header("Debug：排查 Canceled 未触发时勾选")]
        [SerializeField] private bool logGazeInput;

        [Header("Mask Fail Dialogs")]
        [SerializeField] private DialogScript maskFailFirstScript;
        [SerializeField] private DialogScript maskFailNormalScript;

        [SerializeField] private bool _isGazeActive;
        private int _remainingEnergy;
        private int _totalEnergy;
        private DialogScript _currentScript;
        private int _currentLineIndex = -1;
        private DialogScript _lastConsumedScript;
        private int _lastConsumedLineIndex = -1;

        public bool IsGazeActive => _isGazeActive;
        // public bool CanUseGaze => dialogManager != null && dialogManager.IsOpen && _remainingEnergy > 0 &&  GlobalVariables.Instance.GetBool("g_CanUseGaze", true)  &&  !IsDialogInChoiceState();
        public bool CanUseGaze => dialogManager != null && dialogManager.IsOpen && _remainingEnergy > 0 &&  GlobalVariables.Instance.GetBool("g_CanUseGaze", true);
        public int RemainingEnergy
        {
            get => _remainingEnergy;
            set => _remainingEnergy = value;
        }

        public int TotalEnergy
        {
            get => _totalEnergy;
            set => _totalEnergy = value;
        }

        public event Action<bool> OnGazeStateChanged;
        public event Action<int, int> OnEnergyChanged;

        private void OnEnable()
        {
            InitEnergyFromGlobals();

            if (playerController != null)
            {
                playerController.OnSpecialHoldStarted += HandleSpecialHoldStarted;
                playerController.OnSpecialHoldPerformed += HandleSpecialHoldPerformed;
                playerController.OnSpecialHoldCanceled += HandleSpecialHoldCanceled;
            }
        }

        private void OnDisable()
        {
            if (playerController != null)
            {
                playerController.OnSpecialHoldStarted -= HandleSpecialHoldStarted;
                playerController.OnSpecialHoldPerformed -= HandleSpecialHoldPerformed;
                playerController.OnSpecialHoldCanceled -= HandleSpecialHoldCanceled;
            }
            StopGazeSound();
        }

        private bool IsDialogInChoiceState()
        {
            // 在选项状态下禁止进入凝视。
            return dialogManager != null && dialogManager.IsInChoiceState;
        }

        private void Update()
        {
            if (!_isGazeActive) return;

            if (dialogManager != null && !dialogManager.IsOpen)
            {
                ExitGaze();
                return;
            }

            // 兜底：Canceled 未触发时用 Space 松开检测退出。排查法：勾选 PlayerController2D.logSpecialHold 与
            // MaskSystem.logGazeInput，松开时若无 "[SpecialHold] isPressed=False" 则问题在 Input/UI 抢焦点；
            // 对话打开时已用 EventSystem.SetSelectedGameObject(null) 尝试从根源避免。
            if (Keyboard.current != null && !Keyboard.current.spaceKey.isPressed)
                ExitGaze();
        }

        private void HandleSpecialHoldStarted()
        {
            if (!CanUseGaze) return;
            // if (dialogManager == null || !dialogManager.IsOpen) return;
            // 这里只希望在一次按下过程中播放一次音效，不要循环，
            // 因此改为使用一次性 SFX，而不是持续型 SFX。
            if (audioManager != null && gazeHoldClip != null)
                audioManager.PlaySFX(gazeHoldClip, gazeHoldVolume, false);
        }

        private void HandleSpecialHoldPerformed()
        {
            // 能量>0：按原逻辑尝试进入凝视；能量=0：触发面具失败对话。
            if (_remainingEnergy > 0)
            {
                TryEnterGaze();
            }
            else
            {
                TryPlayMaskFailDialog();
            }
        }

        private void HandleSpecialHoldCanceled()
        {
            ExitGaze();
        }

        private void StopGazeSound()
        {
            if (audioManager != null)
                audioManager.StopContinuousSFX();
        }
        

        /// <summary> 一局开始：设置总能量并重置剩余。由 DialogSequencePlayer 或关卡入口调用。 </summary>
        public void InitSession(int totalEnergy)
        {
            _totalEnergy = Mathf.Max(0, totalEnergy);
            _remainingEnergy = _totalEnergy;
            _lastConsumedScript = null;
            _lastConsumedLineIndex = -1;
            SyncEnergyToGlobals();
            OnEnergyChanged?.Invoke(_remainingEnergy, _totalEnergy);
        }

        /// <summary> 供外部（如剧情脚本）直接设置剩余能量，并同步 UI / 全局变量。 </summary>
        public void SetRemainingEnergyExternal(int value)
        {
            _remainingEnergy = Mathf.Clamp(value, 0, _totalEnergy);
            SyncEnergyToGlobals();
            OnEnergyChanged?.Invoke(_remainingEnergy, _totalEnergy);
        }

        /// <summary> 对话打开或换行时由 DialogManager 调用，用于“本行是否已消耗能量”判定。 </summary>
        public void OnDialogLineChanged(DialogScript script, int lineIndex)
        {
            _currentScript = script;
            _currentLineIndex = lineIndex;
        }

        public void TryEnterGaze()
        {
            if (!CanUseGaze) return;
            if (_currentScript == null || _currentLineIndex < 0) return;

            bool sameLine = _lastConsumedScript == _currentScript && _lastConsumedLineIndex == _currentLineIndex;
            if (!sameLine)
            {
                _remainingEnergy--;
                _lastConsumedScript = _currentScript;
                _lastConsumedLineIndex = _currentLineIndex;
                SyncEnergyToGlobals();
                OnEnergyChanged?.Invoke(_remainingEnergy, _totalEnergy);
            }

            if (_isGazeActive) return;
            _isGazeActive = true;
            if (specialModeView != null) specialModeView.SetMode(true);
            OnGazeStateChanged?.Invoke(true);

            var gv = GlobalVariables.Instance;
            if (!gv.GetBool("g_GazeTutorialActive")) return;
            gv.SetBool("g_AllowAdvance", true);
            gv.SetBool("g_GazeTutorialActive", false);
        }

        public void ExitGaze()
        {
            if (!_isGazeActive)
            {
                StopGazeSound();
                return;
            }
            _isGazeActive = false;
            StopGazeSound();
            if (specialModeView) specialModeView.SetMode(false);
            OnGazeStateChanged?.Invoke(false);
        }

        /// <summary>
        /// 在能量为 0 时，由长按空格尝试进入凝视触发面具失败对话。
        /// 首次失败播放 MaskFailFirst，此后播放 MaskFailNormal。
        /// </summary>
        private void TryPlayMaskFailDialog()
        {
            if (dialogManager == null) return;
            if (!dialogManager.IsOpen) return;
            if (IsDialogInChoiceState()) return;

            var gv = GlobalVariables.Instance;
            bool firstPlayed = gv.GetBool("g_MaskFailFirstPlayed", false);

            DialogScript targetScript = firstPlayed ? maskFailNormalScript : maskFailFirstScript;
            if (targetScript == null) return;

            dialogManager.PlayMaskFail(targetScript);

            if (!firstPlayed)
                gv.SetBool("g_MaskFailFirstPlayed", true);
        }

        /// <summary> 从 GlobalVariables 初始化能量值。 </summary>
        private void InitEnergyFromGlobals()
        {
            var gv = GlobalVariables.Instance;
            int total = gv.GetInt("g_totalEnergy", _totalEnergy > 0 ? _totalEnergy : 0);
            int remaining = gv.GetInt("g_remainingEnergy", total);

            _totalEnergy = Mathf.Max(0, total);
            _remainingEnergy = Mathf.Clamp(remaining, 0, _totalEnergy);

            SyncEnergyToGlobals();
            OnEnergyChanged?.Invoke(_remainingEnergy, _totalEnergy);
        }

        /// <summary> 将当前能量同步回 GlobalVariables，供外部修改后影响 UI。 </summary>
        private void SyncEnergyToGlobals()
        {
            var gv = GlobalVariables.Instance;
            gv.SetInt("g_totalEnergy", _totalEnergy);
            gv.SetInt("g_remainingEnergy", _remainingEnergy);
        }
    }
}
