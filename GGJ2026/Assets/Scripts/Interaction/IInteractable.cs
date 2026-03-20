using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IInteractable 
{
    /// <summary>显示在提示里的名字，比如“门”“调查”“对话”</summary>
    string PromptText { get; }

    /// <summary>是否允许交互（比如门锁着/剧情未到）</summary>
    bool CanInteract(GameObject interactor);

    /// <summary>执行交互</summary>
    void Interact(GameObject interactor);

    /// <summary>可选：取消（比如退出调查界面/关闭小UI）</summary>
    void Cancel(GameObject interactor);
}
