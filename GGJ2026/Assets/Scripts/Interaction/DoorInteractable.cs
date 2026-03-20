using UnityEngine;

public class DoorInteractable : MonoBehaviour, IInteractable
{
    [Header("Prompt")]
    [SerializeField] private string prompt = "进入";
    [SerializeField] private bool locked;

    [Header("Dialog")]
    [SerializeField] private DialogManager dialogManager;

    [Tooltip("没上锁时播放的对话脚本")]
    [SerializeField] private DialogScript enterDialog;

    [Tooltip("上锁时播放的对话脚本（比如：门锁住了）")]
    [SerializeField] private DialogScript lockedDialog;

    public string PromptText => locked ? "上锁了" : prompt;

    public bool CanInteract(GameObject interactor)
    {
        return true; // 锁着也允许交互（检查）
    }

    public void Interact(GameObject interactor)
    {
        var interactorComp = interactor.GetComponent<Interactor2D>();

        // 选择要播的脚本
        var script = locked ? lockedDialog : enterDialog;

        // 如果没配脚本，至少要释放占用，避免玩家卡住
        if (dialogManager == null || script == null)
        {
            interactorComp?.EndInteraction();
            return;
        }

        // 播放对话；结束时释放交互占用，让玩家能动
        dialogManager.Play(script, () =>
        {
            interactorComp?.EndInteraction();

            // 如果你需要“未上锁时真正进门/切场景”，可以在这里做：
            // if (!locked) DoEnter();
        });
    }

    public void Cancel(GameObject interactor)
    {
        // 门一般不用取消；如果你希望 X 关闭对话，可：
        // dialogManager?.Close();
    }
}