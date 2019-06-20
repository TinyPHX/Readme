using UnityEngine;
using UnityEditor;

namespace TP.Readme {
    [CustomEditor(typeof(Readme)), ExecuteInEditMode]
    public class ReadmeEditor : Editor
    {
        private bool editing = false;
        private Readme readme;
        private GUIStyle selectableText;
        private GUIStyle editableText;
        private int textPadding = 5;
        
        public override void OnInspectorGUI()
        {
            readme = (Readme) target;
            bool empty = readme.Text == "";
            
            selectableText = new GUIStyle();
            selectableText.focused.textColor = readme.fontColor;
            selectableText.normal.textColor = readme.fontColor;
            selectableText.wordWrap = true;
            selectableText.padding = new RectOffset(textPadding, textPadding, textPadding + 2, textPadding);
            
            editableText = new GUIStyle(GUI.skin.textArea);
            editableText.padding = new RectOffset(textPadding, textPadding, textPadding, textPadding);
    
            float textAreaWidth = EditorGUIUtility.currentViewWidth - 19;
            float textAreaHeight = editableText.CalcHeight(new GUIContent(readme.Text), textAreaWidth);
            
            EditorGUILayout.Space();
            
            if (!editing)
            {
                if (empty)
                {
                    EditorGUILayout.HelpBox("Click edit to add your readme!", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.SelectableLabel(readme.Text, selectableText, GUILayout.Height(textAreaHeight + 4));
                }
            }
            else
            {
                readme.Text = EditorGUILayout.TextArea(readme.Text, editableText, GUILayout.Height(textAreaHeight));

                float smallButtonWidth = EditorGUIUtility.singleLineHeight * 2;
                readme.fontColor = EditorGUILayout.ColorField(readme.fontColor, GUILayout.Width(smallButtonWidth));
                
                Undo.RecordObject(target, "Readme");
            }
            
            EditorGUILayout.Space();
    
            if (!editing)
            {
                if (GUILayout.Button("Edit"))
                {
                    editing = true;
                }
            }
            else
            {
                if (GUILayout.Button("Save"))
                {
                    editing = false;
                }
            }
        }
    }
}