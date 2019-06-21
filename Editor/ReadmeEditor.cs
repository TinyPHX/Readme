using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace TP.Readme {

    [CustomEditor(typeof(Readme)), ExecuteInEditMode]
    public class ReadmeEditor : Editor
    {
        private Readme readme;
        
        // State
        private bool editing = false;
        private bool boldOn = false;
        private bool italicOn = false;
        private bool sourceOn = false;
        
        // Cursor fix 
        private RichTextEditor richTextEditor;
        private int previousCursorIndex;
        
        // Styles
        private GUIStyle activeTextAreaStyle;
        private GUIStyle selectableRichText;
        private GUIStyle editableRichText;
        private GUIStyle editableText;
        private int textPadding = 5;
        
        public override void OnInspectorGUI()
        {
            readme = (Readme) target;
            bool empty = readme.Text == "";
            
            selectableRichText = new GUIStyle();
            selectableRichText.focused.textColor = readme.fontColor;
            selectableRichText.normal.textColor = readme.fontColor;
            selectableRichText.font = readme.font;
            selectableRichText.fontSize = readme.fontSize;
            selectableRichText.wordWrap = true;
            selectableRichText.padding = new RectOffset(textPadding, textPadding, textPadding + 2, textPadding);
            
            editableRichText = new GUIStyle(GUI.skin.textArea);
            editableRichText.richText = true;
            editableRichText.font = readme.font;
            editableRichText.fontSize = readme.fontSize;
            editableRichText.wordWrap = true;
            editableRichText.padding = new RectOffset(textPadding, textPadding, textPadding, textPadding);
            
            editableText = new GUIStyle(GUI.skin.textArea);
            editableText.richText = false;
            editableText.wordWrap = true;
            editableText.padding = new RectOffset(textPadding, textPadding, textPadding, textPadding);
    
            float textAreaWidth = EditorGUIUtility.currentViewWidth - 19;
            float textAreaHeight = editableRichText.CalcHeight(new GUIContent(readme.Text), textAreaWidth);
            
//            textEditor = typeof(EditorGUI)
//                .GetField("activeEditor", BindingFlags.Static | BindingFlags.NonPublic)
//                .GetValue(null) as TextEditor;
            
            richTextEditor = new RichTextEditor();
            
            EditorGUILayout.Space();
            
            if (!editing)
            {
                if (empty)
                {
                    EditorGUILayout.HelpBox("Click edit to add your readme!", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.SelectableLabel(readme.RichText, selectableRichText, GUILayout.Height(textAreaHeight + 4));
                    activeTextAreaStyle = selectableRichText;
                }
            }
            else
            {
                if (sourceOn)
                {
                    readme.RichText = EditorGUILayout.TextArea(readme.RichText, editableText, GUILayout.Height(textAreaHeight));
                }
                else
                {
                    string textAreaName = "Readme Text Editor";
                    GUI.SetNextControlName(textAreaName);
                    readme.RichText = EditorGUILayout.TextArea(readme.RichText, editableRichText, GUILayout.Height(textAreaHeight));
                    Rect textAreaRect = GUILayoutUtility.GetLastRect();
                    
                    if (textAreaRect.x != 0 || textAreaRect.y != 0)
                    {
                        richTextEditor.setTextArea(textAreaName, textAreaRect);
                    }
    
                    activeTextAreaStyle = editableRichText;
                }
    
                FixCursorBug();

                if (richTextEditor.cursorIndex !=  previousCursorIndex)
                {
                    UpdateStyleState();
                }

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
                
                GUILayout.BeginHorizontal();
    
                float smallButtonWidth = EditorGUIUtility.singleLineHeight * 2;
                
                readme.fontColor = EditorGUILayout.ColorField(readme.fontColor, GUILayout.Width(smallButtonWidth));
                readme.font = EditorGUILayout.ObjectField(readme.font, typeof(Font)) as Font;
                
                string[] options = new string[]
                {
                    "8", "9", "10", "11", "12", "14", "16", "18", "20", "22", "24", "26", "28", "36", "48", "72" 
                };
                int selected = options.ToList().IndexOf(readme.fontSize.ToString());
                if (selected == -1)
                {
                    selected = 4;
                }
                selected = EditorGUILayout.Popup(selected, options, GUILayout.Width(smallButtonWidth));
                readme.fontSize = int.Parse(options[selected]);
                
                GUIStyle boldButtonStyle = new GUIStyle(EditorStyles.toolbarButton);
                boldButtonStyle.fontStyle = FontStyle.Normal;
                if (boldOn)
                {
                    boldButtonStyle.fontStyle = FontStyle.Bold;
                }
                if (GUILayout.Button(new GUIContent("B", "Bold (alt+b)"), boldButtonStyle, GUILayout.Width(smallButtonWidth)))
                {
                    ToggleStyle("b");
                }
                
                GUIStyle italicizedButtonStyle = new GUIStyle(EditorStyles.toolbarButton);
                italicizedButtonStyle.fontStyle = FontStyle.Normal;
                if (italicOn)
                {
                    italicizedButtonStyle.fontStyle = FontStyle.Italic;
                }
                if (GUILayout.Button(new GUIContent("I", "Italic (alt+i)"), italicizedButtonStyle, GUILayout.Width(smallButtonWidth)))
                {
                    ToggleStyle("i");
                }
                
                GUIStyle sourceButtonStyle = new GUIStyle(EditorStyles.toolbarButton);
                sourceButtonStyle.fontStyle = FontStyle.Normal;
                GUIContent sourceButtonContent = new GUIContent("</>", "View Source");
                if (sourceOn)
                {
                    sourceButtonContent = new GUIContent("Abc", "View Style");
                }
                if (GUILayout.Button(sourceButtonContent, sourceButtonStyle, GUILayout.Width(smallButtonWidth)))
                {
                    sourceOn = !sourceOn;
                }
                
                GUILayout.EndHorizontal();
                
                if (sourceOn)
                {
                    EditorGUILayout.HelpBox("Source mode enabled! Supported tags:\n <b></b>\n <i></i>\n <color=#00ffff></color>", MessageType.Info);
                }
                
                Readme.advancedOptions = EditorGUILayout.Foldout(Readme.advancedOptions, "Advanced");
                if (Readme.advancedOptions)
                {
                    richTextEditor.FixCursorBug = EditorGUILayout.Toggle("Cursor Correction", richTextEditor.FixCursorBug);
                    EditorGUILayout.LabelField("Cursor Position");
                    EditorGUI.indentLevel++;
                    string richTextWithCursor = readme.RichText;
                    if (richTextEditor != null && richTextEditor.selectIndex <= readme.RichText.Length)
                    {
                        richTextWithCursor = richTextWithCursor.Insert(Mathf.Max(richTextEditor.selectIndex, textEditor.cursorIndex), "|");
                        if (richTextEditor.selectIndex != richTextEditor.cursorIndex)
                        {
                            richTextWithCursor = richTextWithCursor.Insert(Mathf.Min(richTextEditor.selectIndex, textEditor.cursorIndex), "|");
                        }
                    }
                    richTextWithCursor = richTextWithCursor.Replace("\n", " \\n\n");
                    float adjustedTextAreaHeight = editableRichText.CalcHeight(new GUIContent(richTextWithCursor), textAreaWidth - 50);
                    EditorGUILayout.TextArea(richTextWithCursor, editableText, GUILayout.Height(adjustedTextAreaHeight));
                    EditorGUI.indentLevel--;
    
                    if (textEditor != null)
                    {
                        EditorGUILayout.HelpBox(
                            "mousePosition: " + Event.current.mousePosition + "\n" +
                            "textAreaRect.position: " + richTextEditor.TextAreaRect.position + "\n" +
                            "graphicalCursorPos: " + richTextEditor.graphicalCursorPos + "\n" +
                            "Calc Cursor Position: " + (Event.current.mousePosition - richTextEditor.position) + "\n" +
                            "cursorIndex: " + richTextEditor.cursorIndex + "\n" +
                            "selectIndex: " + richTextEditor.selectIndex + "\n" +
                            "position: " + richTextEditor.position + "\n" +
                            "TagsError: " + TagsError(readme.RichText) + "\n" +
                            "Style Map Info: " + "\n" +
                            "\t<b> tags:" + (readme.StyleMaps.ContainsKey("b") ? readme.StyleMaps["b"].FindAll(isStyle => isStyle).Count.ToString() : "0") + "\n" + 
                            "\t<i> tags:" + (readme.StyleMaps.ContainsKey("i") ? readme.StyleMaps["i"].FindAll(isStyle => isStyle).Count.ToString() : "0") + "\n" + 
                            ""
                            , MessageType.Info);
                    }
    
                    bool textEditorActive = textEditor != null;
                    MessageType messageType = textEditorActive ? MessageType.Info : MessageType.Warning;
                    EditorGUILayout.HelpBox("Text Editor Active: " + (textEditorActive ? "Yes" : "No"), messageType);
                    
                    EditorGUILayout.HelpBox("Shortcuts: \n" + 
                        "\tBold: alt+b\n" +
                        "\tItalic: alt+i"
                        , MessageType.Info);
                    
                    GUILayout.BeginHorizontal();
    
                    if (GUILayout.Button("Save to File", GUILayout.Width(smallButtonWidth * 4)))
                    {
                        readme.Save();
                    }
    
                    GUIStyle loadButtonStyle = new GUIStyle(GUI.skin.button);
                    if (GUILayout.Button("Load from File", GUILayout.Width(smallButtonWidth * 4)))
                    {
                        readme.LoadLastSave();
                        Repaint();
                    }
    
                    GUILayout.EndHorizontal();
                    
                    EditorGUI.indentLevel--;
                }
            }
    
            CheckKeyboardShortCuts();
        }
    
        private void CheckKeyboardShortCuts()
        {
            Event currentEvent = Event.current;
            
            //Alt + b for bold
            if (currentEvent.type != EventType.KeyUp && currentEvent.alt && currentEvent.keyCode == KeyCode.B)
            {
                ToggleStyle("b");
                currentEvent.Use();
            }
            
            //Alt + i for italic
            if (currentEvent.type != EventType.KeyUp && currentEvent.alt && currentEvent.keyCode == KeyCode.I)
            {
                ToggleStyle("i");
                currentEvent.Use();
            }
        }
    
        private void ToggleStyle(string tag)
        {
            if (TagsError(readme.RichText))
            {
                Debug.LogWarning("Please fix any mismatched tags first!");
                return;
            }
            
            if (textEditor != null)
            {
                int styleStartIndex = readme.GetPoorIndex(Mathf.Min(textEditor.cursorIndex, textEditor.selectIndex));
                int styleEndIndex = readme.GetPoorIndex(Mathf.Max(textEditor.cursorIndex, textEditor.selectIndex));
                int poorStyleLength = styleEndIndex - styleStartIndex;
    
                readme.ToggleStyle(tag, styleStartIndex, poorStyleLength);
    
                if (TagsError(readme.RichText))
                {
                    readme.LoadLastSave();
                    Debug.LogWarning("You can't do that!");
                }
                
                EditorGUI.FocusTextInControl("");
                textEditor.cursorIndex = readme.GetRichIndex(styleEndIndex);
    //            EditorGUI.FocusTextInControl(readmeEditorTextAreaName);

                UpdateStyleState();
            }
        }

        void UpdateStyleState()
        {
            int index = 0;
            int poorCursorIndex = readme.GetPoorIndex(textEditor.cursorIndex);
            int poorSelectIndex = readme.GetPoorIndex(textEditor.selectIndex);

            if (poorSelectIndex != poorCursorIndex)
            {
                index = Mathf.Max(poorCursorIndex, poorSelectIndex) - 1;
            }
            else
            {
                index = poorCursorIndex;
            }
            
            boldOn = readme.IsStyle("b", index);
            italicOn = readme.IsStyle("i", index);
        }
    
        public int WordStartIndex
        {
            get
            {
                int tmpCursorIndex = textEditor.cursorIndex;
                int tmpSelectIndex = textEditor.selectIndex;
    
                textEditor.MoveWordLeft();
                int wordStartIndex = textEditor.selectIndex;
                
                textEditor.cursorIndex = tmpCursorIndex;
                textEditor.selectIndex = tmpSelectIndex;
    
                return wordStartIndex;
            }
        }
    
        public int WordEndIndex
        {
            get
            {
                int tmpCursorIndex = textEditor.cursorIndex;
                int tmpSelectIndex = textEditor.selectIndex;
    
                textEditor.MoveWordRight();
                int wordStartIndex = textEditor.selectIndex;
                
                textEditor.cursorIndex = tmpCursorIndex;
                textEditor.selectIndex = tmpSelectIndex;
    
                return wordStartIndex;
            }
        }
    
        public Vector2 GraphicalCursorPos
        {
            get
            {
                Rect position = textEditor.position;
                GUIContent content = textEditor.content;
                int cursorPos = textEditor.cursorIndex;
                return editableRichText.GetCursorPixelPosition(new Rect(0, 0, position.width, position.height), content, cursorPos);
            }
        }
    }
}