using UnityEditor;
using UnityEngine;

/// <summary>
/// 自定义 VariableAssignment 的 Inspector 展示：
/// 根据 ValueType 只显示对应的值字段（Bool / Int / String），避免多个值同时出现。
/// </summary>
[CustomPropertyDrawer(typeof(VariableAssignment))]
public class VariableAssignmentDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // 折叠头
        var foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;

            var lineHeight = EditorGUIUtility.singleLineHeight;
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = position.y + lineHeight + spacing;

            // Key
            var keyProp = property.FindPropertyRelative("key");
            var keyRect = new Rect(position.x, y, position.width, lineHeight);
            EditorGUI.PropertyField(keyRect, keyProp);
            y += lineHeight + spacing;

            // ValueType
            var typeProp = property.FindPropertyRelative("valueType");
            var typeRect = new Rect(position.x, y, position.width, lineHeight);
            EditorGUI.PropertyField(typeRect, typeProp);
            y += lineHeight + spacing;

            // Value (one field depending on ValueType)
            var valueRect = new Rect(position.x, y, position.width, lineHeight);
            var type = (VariableValueType)typeProp.enumValueIndex;
            switch (type)
            {
                case VariableValueType.Bool:
                    EditorGUI.PropertyField(valueRect, property.FindPropertyRelative("boolValue"), new GUIContent("Bool Value"));
                    break;
                case VariableValueType.Int:
                    EditorGUI.PropertyField(valueRect, property.FindPropertyRelative("intValue"), new GUIContent("Int Value"));
                    break;
                case VariableValueType.String:
                    EditorGUI.PropertyField(valueRect, property.FindPropertyRelative("stringValue"), new GUIContent("String Value"));
                    break;
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var lineHeight = EditorGUIUtility.singleLineHeight;
        var spacing = EditorGUIUtility.standardVerticalSpacing;

        if (!property.isExpanded)
            return lineHeight;

        // 折叠头 + Key + ValueType + 1 行 Value
        int lines = 1 + 3;
        return lines * lineHeight + (lines - 1) * spacing;
    }
}

