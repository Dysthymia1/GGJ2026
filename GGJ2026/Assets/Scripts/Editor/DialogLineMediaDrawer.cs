using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(DialogLineMedia))]
public class DialogLineMediaDrawer : PropertyDrawer
{
    private const float HelpBoxHeightLines = 2f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var lineHeight = EditorGUIUtility.singleLineHeight;
        var spacing = EditorGUIUtility.standardVerticalSpacing;

        // Foldout header
        var foldoutRect = new Rect(position.x, position.y, position.width, lineHeight);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;
        float y = position.y + lineHeight + spacing;

        void DrawSectionLabel(string text)
        {
            var r = new Rect(position.x, y, position.width, lineHeight);
            EditorGUI.LabelField(r, text, EditorStyles.boldLabel);
            y += lineHeight + spacing;
        }

        void DrawProp(SerializedProperty p, GUIContent contentOverride = null)
        {
            var r = new Rect(position.x, y, position.width, lineHeight);
            if (contentOverride != null) EditorGUI.PropertyField(r, p, contentOverride, true);
            else EditorGUI.PropertyField(r, p, true);
            y += EditorGUI.GetPropertyHeight(p, true) + spacing;
        }

        // --- SFX ---
        DrawSectionLabel("SFX");
        DrawProp(property.FindPropertyRelative("sfx"));
        DrawProp(property.FindPropertyRelative("sfxVolume"));
        DrawProp(property.FindPropertyRelative("stopSfxBeforePlay"));

        // --- BGM ---
        DrawSectionLabel("BGM");
        var applyBgmProp = property.FindPropertyRelative("applyBgm");
        DrawProp(applyBgmProp);

        if (applyBgmProp.boolValue)
        {
            var actionProp = property.FindPropertyRelative("bgmAction");
            var useFadeProp = property.FindPropertyRelative("bgmUseFade");

            DrawProp(actionProp);
            DrawProp(useFadeProp);

            var action = (DialogLineMedia.BgmAction)actionProp.enumValueIndex;
            bool isPlay = action == DialogLineMedia.BgmAction.Play;

            if (useFadeProp.boolValue)
            {
                DrawProp(property.FindPropertyRelative("bgmFadeOut"));
                if (isPlay)
                    DrawProp(property.FindPropertyRelative("bgmFadeIn"));
            }

            if (isPlay)
            {
                var bgmClipProp = property.FindPropertyRelative("bgm");
                DrawProp(bgmClipProp);
                DrawProp(property.FindPropertyRelative("bgmVolume"));
                DrawProp(property.FindPropertyRelative("bgmLoop"));

                if (bgmClipProp.objectReferenceValue == null)
                {
                    var helpRect = new Rect(position.x, y, position.width, lineHeight * HelpBoxHeightLines);
                    EditorGUI.HelpBox(helpRect, "Bgm Action 为 Play，但 Bgm 为空：本行不会切换/播放新的 BGM。", MessageType.Info);
                    y += helpRect.height + spacing;
                }
            }
        }

        // --- Background ---
        DrawSectionLabel("Background");
        DrawProp(property.FindPropertyRelative("background"));
        DrawProp(property.FindPropertyRelative("fadeBackground"));
        DrawProp(property.FindPropertyRelative("bgFadeDuration"));

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var lineHeight = EditorGUIUtility.singleLineHeight;
        var spacing = EditorGUIUtility.standardVerticalSpacing;

        if (!property.isExpanded)
            return lineHeight;

        float height = 0f;

        // Foldout header
        height += lineHeight + spacing;

        // Helper to add one line-ish property height
        float PropHeight(string relativeName)
        {
            var p = property.FindPropertyRelative(relativeName);
            return EditorGUI.GetPropertyHeight(p, true) + spacing;
        }

        // Section label lines: SFX, BGM, Background
        float SectionLabel() => lineHeight + spacing;

        // --- SFX ---
        height += SectionLabel();
        height += PropHeight("sfx");
        height += PropHeight("sfxVolume");
        height += PropHeight("stopSfxBeforePlay");

        // --- BGM ---
        height += SectionLabel();
        height += PropHeight("applyBgm");

        var applyBgmProp = property.FindPropertyRelative("applyBgm");
        if (applyBgmProp.boolValue)
        {
            height += PropHeight("bgmAction");
            height += PropHeight("bgmUseFade");

            var actionProp = property.FindPropertyRelative("bgmAction");
            var useFadeProp = property.FindPropertyRelative("bgmUseFade");
            var action = (DialogLineMedia.BgmAction)actionProp.enumValueIndex;
            bool isPlay = action == DialogLineMedia.BgmAction.Play;

            if (useFadeProp.boolValue)
            {
                height += PropHeight("bgmFadeOut");
                if (isPlay) height += PropHeight("bgmFadeIn");
            }

            if (isPlay)
            {
                height += PropHeight("bgm");
                height += PropHeight("bgmVolume");
                height += PropHeight("bgmLoop");

                var bgmClipProp = property.FindPropertyRelative("bgm");
                if (bgmClipProp.objectReferenceValue == null)
                {
                    height += (lineHeight * HelpBoxHeightLines) + spacing;
                }
            }
        }

        // --- Background ---
        height += SectionLabel();
        height += PropHeight("background");
        height += PropHeight("fadeBackground");
        height += PropHeight("bgFadeDuration");

        // remove last spacing to match Unity's typical layout
        height -= spacing;
        return height;
    }
}

