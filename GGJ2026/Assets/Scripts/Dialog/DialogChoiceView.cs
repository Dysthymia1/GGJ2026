using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 对话选项视图：根据当前行的 DialogChoiceOption 列表动态生成按钮，使用 VerticalLayoutGroup 自动垂直布局。
/// </summary>
public class DialogChoiceView : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private RectTransform optionsRoot;
    [SerializeField] private Button optionButtonPrefab;

    private readonly List<GameObject> _spawnedButtons = new List<GameObject>();
    private Action<int> _onSelected;

    public bool IsVisible => optionsRoot != null && optionsRoot.gameObject.activeSelf;

    public void ShowOptions(IReadOnlyList<DialogChoiceOption> options, Action<int> onSelected)
    {
        _onSelected = onSelected;

        if (!optionsRoot || !optionButtonPrefab)
            return;

        Clear();

        optionsRoot.gameObject.SetActive(true);

        GameObject firstButton = null;

        for (int i = 0; i < options.Count; i++)
        {
            var data = options[i];
            if (data == null) continue;

            var btnObj = Instantiate(optionButtonPrefab, optionsRoot);
            _spawnedButtons.Add(btnObj.gameObject);

            var label = btnObj.GetComponentInChildren<TMP_Text>();
            if (label)
                label.text = data.text ?? string.Empty;

            int index = i;
            btnObj.onClick.AddListener(() => HandleButtonClicked(index));

            if (!firstButton)
                firstButton = btnObj.gameObject;
        }

        if (EventSystem.current && firstButton)
            EventSystem.current.SetSelectedGameObject(firstButton);
    }

    public void Hide()
    {
        Clear();
        if (optionsRoot)
            optionsRoot.gameObject.SetActive(false);
        _onSelected = null;
    }

    private void Clear()
    {
        foreach (var go in _spawnedButtons)
        {
            if (go)
                Destroy(go);
        }
        _spawnedButtons.Clear();
    }

    private void HandleButtonClicked(int index)
    {
        _onSelected?.Invoke(index);
    }
}

