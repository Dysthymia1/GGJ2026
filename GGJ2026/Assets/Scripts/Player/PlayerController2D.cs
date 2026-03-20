using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController2D : MonoBehaviour
{
    public enum InputMode { Gameplay, VN }

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4.0f;
    
    private Rigidbody2D rb;
    private Vector2 moveInput;
    [SerializeField] private SpriteRenderer spriteRenderer;
    private float lastFacingX = -1f; // 默认朝左
    private Animator animator; // 动画控制器
    private bool movementEnabled = true;
    private bool inputEnabled = true;

    public InputMode CurrentMode { get; private set; } = InputMode.Gameplay;

    // 让别的系统订阅（交互系统/菜单系统）
    public event Action OnInteractPressed;
    public event Action OnCancelPressed;
    public event Action OnPausePressed;
    
    public event Action OnSpecialHoldStarted;   // 按下瞬间（Hold 的 started）
    public event Action OnSpecialHoldPerformed;
    public event Action OnSpecialHoldCanceled;

    [Header("SpecialHold 输入（可选）")]
    [Tooltip("若绑定则直接订阅 performed/canceled，避免 SendMessages 不转发 Canceled；不绑定时会 GetComponent<PlayerInput>()")]
    [SerializeField] private PlayerInput playerInput;

    private InputAction _specialHoldAction;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        if (playerInput == null) playerInput = GetComponent<PlayerInput>();
    }

    private void OnEnable()
    {
        if (playerInput != null && playerInput.actions != null)
        {
            var map = playerInput.actions.FindActionMap("Gameplay");
            if (map != null)
            {
                _specialHoldAction = map.FindAction("SpecialHold");
                if (_specialHoldAction != null)
                {
                    _specialHoldAction.started += OnSpecialHoldActionStarted;
                    _specialHoldAction.performed += OnSpecialHoldActionPerformed;
                    _specialHoldAction.canceled += OnSpecialHoldActionCanceled;
                }
            }
        }
    }

    private void OnDisable()
    {
        if (_specialHoldAction != null)
        {
            _specialHoldAction.started -= OnSpecialHoldActionStarted;
            _specialHoldAction.performed -= OnSpecialHoldActionPerformed;
            _specialHoldAction.canceled -= OnSpecialHoldActionCanceled;
            _specialHoldAction = null;
        }
    }

    private void OnSpecialHoldActionStarted(InputAction.CallbackContext _)
    {
        if (!inputEnabled) return;
        OnSpecialHoldStarted?.Invoke();
    }

    private void OnSpecialHoldActionPerformed(InputAction.CallbackContext _)
    {
        if (!inputEnabled) return;
        OnSpecialHoldPerformed?.Invoke();
    }

    private void OnSpecialHoldActionCanceled(InputAction.CallbackContext _)
    {
        if (!inputEnabled) return;
        OnSpecialHoldCanceled?.Invoke();
    }

    private void FixedUpdate()
    {
        if (!inputEnabled || !movementEnabled || CurrentMode != InputMode.Gameplay)
        {
            // 保持 y 方向速度（如果你有移动平台/外力），只停止水平
            rb.velocity = new Vector2(0f, rb.velocity.y);
            return;
        }

        // 只用 x 做横版移动
        float x = moveInput.x;
        rb.velocity = new Vector2(x * moveSpeed, rb.velocity.y);
        // 根据水平输入切换动画
        if (Mathf.Abs(moveInput.x) > 0.01f)
        {
            animator.SetBool("IsWalking", true);  // 启动走路动画
        }
        else
        {
            animator.SetBool("IsWalking", false); // 进入 idle 状态
        }
    }

    // --- Input System 回调（PlayerInput 绑定到这些方法） ---

    public void OnMove(InputValue value)
    {
        if (!inputEnabled) return;
        moveInput = value.Get<Vector2>();
        
        // 只在有明确输入的方向时才更新朝向
        if (Mathf.Abs(moveInput.x) > 0.01f)
        {
            lastFacingX = Mathf.Sign(moveInput.x);
            spriteRenderer.flipX = lastFacingX < 0;
        }
    }

    public void OnInteract(InputValue value)
    {
        if (!inputEnabled) return;
        if (value.isPressed) OnInteractPressed?.Invoke();
    }

    public void OnCancel(InputValue value)
    {
        if (!inputEnabled) return;
        if (value.isPressed) OnCancelPressed?.Invoke();
    }

    public void OnPause(InputValue value)
    {
        if (!inputEnabled) return;
        if (value.isPressed) OnPausePressed?.Invoke();
    }
    
    [Header("Debug：排查 SpecialHold Canceled 未触发时勾选")]
    [SerializeField] private bool logSpecialHold;

    /// <summary> SendMessages 回调。若已用 PlayerInput 直接订阅 performed/canceled，则此处不再发事件，避免重复且 Canceled 由订阅处理。 </summary>
    public void OnSpecialHold(InputValue value)
    {
        // if (logSpecialHold)
        //     Debug.Log($"[SpecialHold] isPressed={value.isPressed}, inputEnabled={inputEnabled}, useDirectSub={_specialHoldAction != null}");

        if (_specialHoldAction != null) return;

        if (!inputEnabled) return;

        // 重点：你在 Input Actions 里给 SpecialHold 加了 Hold Interaction
        // SendMessages 触发 OnSpecialHold() 的时机：
        // - performed（按住超过 HoldTime）时：value.isPressed == true
        // - canceled（松开）时：value.isPressed == false
        if (value.isPressed) OnSpecialHoldPerformed?.Invoke();
        else OnSpecialHoldCanceled?.Invoke();
    }

    // --- 给外部系统调用的控制接口 ---

    public void SetMode(InputMode mode)
    {
        CurrentMode = mode;

        if (mode == InputMode.VN)
        {
            // 进入 VN 时清空移动输入，避免回到 Gameplay 时“黏住”
            moveInput = Vector2.zero;
            rb.velocity = new Vector2(0f, rb.velocity.y);
        }
    }

    public void EnableInput(bool isEnabled)
    {
        inputEnabled = isEnabled;
        if (isEnabled) return;
        moveInput = Vector2.zero;
        rb.velocity = new Vector2(0f, rb.velocity.y);
    }

    public void EnableMovement(bool isEnabled)
    {
        movementEnabled = isEnabled;
        if (isEnabled) return;
        moveInput = Vector2.zero;
        rb.velocity = new Vector2(0f, rb.velocity.y);
    }
}
