using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class InteractionPromptUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Interactor2D interactor;
    [SerializeField] private Camera worldCamera; // Overlay也建议指定主摄像机
    [SerializeField] private RectTransform root;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text keyText;
    [SerializeField] private TMP_Text actionText;

    [Header("Text")]
    [SerializeField] private string keyLabel = "Z";

    [Header("Follow")]
    [SerializeField] private Vector2 screenOffset = new Vector2(0, 40); // 往上飘一点
    [SerializeField] private float followLerp = 20f;

    [Header("Fade")]
    [SerializeField] private float fadeSpeed = 12f;

    [Header("State Style")]
    [SerializeField] private float disabledAlpha = 0.45f;
    
    private Interactor2D.PromptData? currentData;
    private Vector2 targetScreenPos;
    private bool forceHidden;
    
     private void Awake()
    {
        if (worldCamera == null) worldCamera = Camera.main;
        HideImmediate();
    }

    private void OnEnable()
    {
        if (interactor != null)
            interactor.OnPromptChanged += OnPromptChanged;
    }

    private void OnDisable()
    {
        if (interactor != null)
            interactor.OnPromptChanged -= OnPromptChanged;
    }

    private void OnPromptChanged(Interactor2D.PromptData? data)
    {
        currentData = data;

        if (data == null)
        {
            // 目标消失：开始淡出
            return;
        }

        // 刷新文本
        keyText.text = keyLabel;
        actionText.text = data.Value.target.PromptText;

        // 立即计算一次位置，避免第一帧跳动
        targetScreenPos = WorldToScreen(data.Value.worldPos) + screenOffset;
        root.position = targetScreenPos;
    }

    private void LateUpdate()
    {
        if (forceHidden)
        {
            FadeTo(0f);
            return;
        }

        bool shouldShow = currentData != null;

        if (!shouldShow)
        {
            FadeTo(0f);
            return;
        }

        var data = currentData.Value;

        // 位置跟随
        targetScreenPos = WorldToScreen(data.worldPos) + screenOffset;
        root.position = Vector2.Lerp(root.position, targetScreenPos, 1f - Mathf.Exp(-followLerp * Time.deltaTime));

        // 状态样式：不可交互则变淡
        float targetAlpha = data.canInteract ? 1f : disabledAlpha;
        FadeTo(targetAlpha);
    }

    private Vector2 WorldToScreen(Vector3 worldPos)
    {
        if (!worldCamera) return worldPos;
        return RectTransformUtility.WorldToScreenPoint(worldCamera, worldPos);
    }

    private void FadeTo(float target)
    {
        if (!canvasGroup) return;
        canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, target, 1f - Mathf.Exp(-fadeSpeed * Time.deltaTime));
        canvasGroup.blocksRaycasts = canvasGroup.alpha > 0.01f;
        canvasGroup.interactable = canvasGroup.alpha > 0.01f;
    }

    public void SetForceHidden(bool hidden)
    {
        // 仅控制“强制隐藏”标志，不清空 currentData。
        // 这样在交互结束后，如果 Interactor2D 仍认为目标有效，提示可以自动恢复。
        forceHidden = hidden;
    }

    private void HideImmediate()
    {
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
    }
    
    
}
