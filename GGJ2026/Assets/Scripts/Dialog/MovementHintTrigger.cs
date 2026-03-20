using UnityEngine;

/// <summary>
/// 当玩家离开触发区域时隐藏移动提示。
/// </summary>
public class MovementHintTrigger : MonoBehaviour
{
    [SerializeField] private MovementHintUI movementHintUI;
    private bool _hasHidden; // 是否已经hide过的标记，保证只隐藏一次
    private void OnTriggerExit2D(Collider2D other)
    {
        if (_hasHidden || other == null)
            return;
        if (other.GetComponent<PlayerController2D>() == null)
            return;
        _hasHidden = true;
        if (movementHintUI != null)
            movementHintUI.HideWithFadeAndDeactivate();
    }
}

