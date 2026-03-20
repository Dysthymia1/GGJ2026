using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Interactor2D : MonoBehaviour
{
    [Header("Detect")]
    [SerializeField] private Transform origin;            // 一般是玩家中心或胸口位置
    [SerializeField] private Vector2 boxSize = new Vector2(1.2f, 1.0f);
    [SerializeField] private float forwardOffset = 0.8f;  // 前方偏移
    [SerializeField] private LayerMask interactableMask;

    [Header("References")]
    [SerializeField] private PlayerController2D playerController;
    [SerializeField] private SpriteRenderer spriteRenderer; // 用来判断朝向（flipX）
    // 也可以改成你自己的 FacingDirection 字段
    
    [SerializeField] private DialogManager dialogManager; // 拖拽场景里的对话管理器
    [SerializeField] private InteractionPromptUI interactionPromptUI; // 可选：用于在交互期间隐藏提示 UI

    public struct PromptData
    {
        public IInteractable target;
        public bool canInteract;
        public Vector3 worldPos;  // 提示锚点
    }

    public event System.Action<PromptData?> OnPromptChanged;

    // 缓存
    private readonly Collider2D[] hits = new Collider2D[16];
    private IInteractable current;
    private bool inInteraction; // 是否处于“交互占用状态”（例如已进入调查界面）

    // （可选）给提示 UI 用
    public IInteractable Current => current;

    private void Awake()
    {
        playerController = GetComponent<PlayerController2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }
    private void OnEnable()
    {
        if (playerController != null)
        {
            playerController.OnInteractPressed += HandleInteract;
            playerController.OnCancelPressed += HandleCancel;
        }
    }

    private void OnDisable()
    {
        if (playerController != null)
        {
            playerController.OnInteractPressed -= HandleInteract;
            playerController.OnCancelPressed -= HandleCancel;
        }
    }

    private void Update()
    {
        // VN模式/禁用输入时，不检测（根据你项目的模式控制）
        if (!playerController) return;
        if (playerController.CurrentMode != PlayerController2D.InputMode.Gameplay) 
        {
            SetCurrent(null);
            return;
        }

        // 如果你希望“交互中保持锁定对象”，就不更新 current
        if (inInteraction) return;

        FindBestInteractable();
    }

    private void FindBestInteractable()
    {
        Vector2 o = origin ? (Vector2)origin.position : (Vector2)transform.position;

        float dir = spriteRenderer && spriteRenderer.flipX ? -1f : 1f;
        Vector2 center = o + Vector2.right * (dir * forwardOffset);

        int count = Physics2D.OverlapBoxNonAlloc(center, boxSize, 0f, hits, interactableMask);
        
        if (count <= 0)
        {
            SetCurrent(null);
            return;
        }

        IInteractable best = null;
        float bestDist = float.MaxValue;
        Collider2D bestCol = null;

        for (int i = 0; i < count; i++)
        {
            var col = hits[i];
            if (!col) continue;

            // 允许 IInteractable 挂在父节点上
            var interactable = col.GetComponentInParent<IInteractable>();
            if (interactable == null) continue;

            if (!interactable.CanInteract(gameObject)) continue;

            float d = Vector2.Distance(o, col.ClosestPoint(o));
            if (d < bestDist)
            {
                bestDist = d;
                best = interactable;
                bestCol = col;
            }
        }

        if (best == null) SetCurrent(null);
        else SetCurrent(best, true, bestCol.bounds.center);
    }

    private void SetCurrent(IInteractable target, bool canInteract = true, Vector3 worldPos = default)
    {
        if (ReferenceEquals(current, target)) return;
        current = target;

        // 这里你可以通知 UI 提示刷新（例如事件回调）
        // OnTargetChanged?.Invoke(current);
        if (current == null)
        {
            OnPromptChanged?.Invoke(null);
        }
        else
        {
            OnPromptChanged?.Invoke(new PromptData
            {
                target = current,
                canInteract = canInteract,
                worldPos = worldPos
            });
        }
    }

    private void HandleInteract()
    {
        
        // 1) 如果对话正在显示，让对话系统吃掉 Z（继续/跳过）
        if (dialogManager != null && dialogManager.IsOpen)
        {
            dialogManager.OnInteractPressed();
            return;
        }
        
        
        if (playerController.CurrentMode != PlayerController2D.InputMode.Gameplay) return;

        // 没对象就不做事
        if (current == null) return;

        // 告诉提示 UI：进入“交互占用”状态，强制隐藏提示
        if (interactionPromptUI != null)
        {
            interactionPromptUI.SetForceHidden(true);
        }

        // 交互占用：进入交互后禁用移动（可选，看你是否希望交互时不能走）
        // 对“调查/对话”一般是禁用移动更舒服
        inInteraction = true;
        playerController.EnableMovement(false);

        current.Interact(gameObject);
    }

    private void HandleCancel()
    {
        if (playerController.CurrentMode != PlayerController2D.InputMode.Gameplay) return;

        // 如果交互对象支持 Cancel，就把 X 转发过去
        if (inInteraction && current != null)
        {
            current.Cancel(gameObject);

            // 退出交互占用
            inInteraction = false;
            playerController.EnableMovement(true);
            
            // 取消交互时，同样恢复提示 UI 的自动显示逻辑
            if (interactionPromptUI != null)
            {
                interactionPromptUI.SetForceHidden(false);
            }
            return;
        }

        // 如果不在交互状态，你也可以把 X 当“后退/关闭提示”等
    }
    
    public void EndInteraction()
    {
        inInteraction = false;
        playerController.EnableMovement(true);
        
        // 退出交互占用后，允许提示 UI 根据当前检测结果决定是否重新显示
        if (interactionPromptUI != null)
        {
            interactionPromptUI.SetForceHidden(false);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector2 o = origin != null ? (Vector2)origin.position : (Vector2)transform.position;
        float dir = (spriteRenderer != null && spriteRenderer.flipX) ? -1f : 1f;
        Vector2 center = o + Vector2.right * dir * forwardOffset;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(center, boxSize);
    }
#endif
    
    
    
}
