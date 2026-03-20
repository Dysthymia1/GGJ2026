using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 对话头像显示：根据 DialogLine 控制左右头像的显隐与贴图。
/// </summary>
public class DialogPortraitController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject leftPortraitRoot;
    [SerializeField] private GameObject rightPortraitRoot;
    [SerializeField] private Image leftPortraitImage;
    [SerializeField] private Image rightPortraitImage;

    [Tooltip("角色数据库：从角色ID与姿势ID解析立绘与默认偏移")]
    [SerializeField] private CharacterDatabase characterDatabase;

    // 当前状态缓存：用于在行间继承角色/pose/offset
    private string _currentLeftCharacterId;
    private string _currentRightCharacterId;
    private string _currentLeftPoseId;
    private string _currentRightPoseId;

    private Sprite _currentLeftSprite;
    private Sprite _currentRightSprite;

    private Vector2 _currentLeftOffset;
    private Vector2 _currentRightOffset;
    private bool _hasLeftOffset;
    private bool _hasRightOffset;

    /// <summary> 根据当前行设置左右头像显示/隐藏与贴图。 </summary>
    public void SetPortrait(DialogLine line)
    {
        if (line == null)
        {
            HideAll();
            return;
        }

        ApplySide(
            isLeft: true,
            root: leftPortraitRoot,
            image: leftPortraitImage,
            lineCharacterId: line.leftCharacterId,
            linePoseId: line.leftPoseId,
            show: line.showLeftPortrait,
            overrideOffset: line.overrideLeftOffset,
            lineOffset: line.leftOffset,
            ref _currentLeftCharacterId,
            ref _currentLeftPoseId,
            ref _currentLeftSprite,
            ref _currentLeftOffset,
            ref _hasLeftOffset
        );

        ApplySide(
            isLeft: false,
            root: rightPortraitRoot,
            image: rightPortraitImage,
            lineCharacterId: line.rightCharacterId,
            linePoseId: line.rightPoseId,
            show: line.showRightPortrait,
            overrideOffset: line.overrideRightOffset,
            lineOffset: line.rightOffset,
            ref _currentRightCharacterId,
            ref _currentRightPoseId,
            ref _currentRightSprite,
            ref _currentRightOffset,
            ref _hasRightOffset
        );
    }

    /// <summary> 隐藏所有头像。 </summary>
    public void HideAll()
    {
        if (leftPortraitRoot) leftPortraitRoot.SetActive(false);
        if (rightPortraitRoot) rightPortraitRoot.SetActive(false);

        if (leftPortraitImage) leftPortraitImage.sprite = null;
        if (rightPortraitImage) rightPortraitImage.sprite = null;

        _currentLeftCharacterId = null;
        _currentRightCharacterId = null;
        _currentLeftPoseId = null;
        _currentRightPoseId = null;
        _currentLeftSprite = null;
        _currentRightSprite = null;
        _hasLeftOffset = false;
        _hasRightOffset = false;
        _currentLeftOffset = Vector2.zero;
        _currentRightOffset = Vector2.zero;
    }

    private void ApplySide(
        bool isLeft,
        GameObject root,
        Image image,
        string lineCharacterId,
        string linePoseId,
        bool show,
        bool overrideOffset,
        Vector2 lineOffset,
        ref string currentCharacterId,
        ref string currentPoseId,
        ref Sprite currentSprite,
        ref Vector2 currentOffset,
        ref bool hasOffset
    )
    {
        // 更新当前角色ID/pose
        if (!string.IsNullOrEmpty(lineCharacterId))
        {
            currentCharacterId = lineCharacterId;
        }

        if (!string.IsNullOrEmpty(linePoseId))
        {
            currentPoseId = linePoseId;
        }

        // 解析 Sprite
        if (!string.IsNullOrEmpty(currentCharacterId) && characterDatabase)
        {
            var s = currentCharacterId;
            var entry = characterDatabase.characters?.FirstOrDefault(c => c != null && c.id == s);

            if (entry != null)
            {
                currentSprite = ResolveSprite(entry, currentPoseId);

                // 默认 offset：来自 CharacterEntry.defaultOffset
                if (!hasOffset)
                {
                    currentOffset = entry.defaultOffset;
                    hasOffset = true;
                }
            }
        }

        // 行级覆盖 offset
        if (overrideOffset)
        {
            currentOffset = lineOffset;
            hasOffset = true;
        }

        // 应用到 UI
        if (image)
            image.sprite = currentSprite;

        if (!root) return;
        // var rt = root.GetComponent<RectTransform>();
        // if (rt && hasOffset)
        // {
        //     rt.anchoredPosition = currentOffset;
        // }

        bool shouldShow = show && currentSprite;
        root.SetActive(shouldShow);
    }

    private static Sprite ResolveSprite(CharacterEntry entry, string poseId)
    {
        // 暂时仅支持 defaultPortrait；若后续引入 poseId → poses[] 映射，可在此扩展
        return entry?.defaultPortrait;
    }
}
