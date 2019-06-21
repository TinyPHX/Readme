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
        private bool editing = false;
        private bool sourceOn = false;
        private int previousSelctIndex = -1;
        private bool selectIndexChanged = false;
        private int previousCursorIndex = -1;
        private bool cursorIndexChanged = false;
        private TextEditor textEditor;
        private Rect textAreaRect;
        private GUIStyle activeTextAreaStyle;
        private GUIStyle selectableRichText;
        private GUIStyle editableRichText;
        private GUIStyle editableText;
        private const string readmeEditorTextAreaName = "Readme Text Editor";
        private bool fixCursorBug = true;
        private int textPadding = 5;
        
        public override void OnInspectorGUI()
        {
            readme = (Readme) target;
            bool empty = readme.Text == "";
            
            selectableRichText = new GUIStyle();
            selectableRichText.focused.textColor = readme.fontColor;
            selectableRichText.normal.textColor = readme.fontColor;
            selectableRichText.wordWrap = true;
            selectableRichText.padding = new RectOffset(textPadding, textPadding, textPadding + 2, textPadding);
            
            editableRichText = new GUIStyle(GUI.skin.textArea);
            editableRichText.richText = true;
            editableRichText.wordWrap = true;
            editableRichText.padding = new RectOffset(textPadding, textPadding, textPadding, textPadding);
            
            editableText = new GUIStyle(GUI.skin.textArea);
            editableText.richText = false;
            editableText.wordWrap = true;
            editableText.padding = new RectOffset(textPadding, textPadding, textPadding, textPadding);
    
            float textAreaWidth = EditorGUIUtility.currentViewWidth - 19;
            float textAreaHeight = editableRichText.CalcHeight(new GUIContent(readme.Text), textAreaWidth);
            
            textEditor = typeof(EditorGUI)
                .GetField("activeEditor", BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null) as TextEditor;
            
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
                    readme.RichText = EditorGUILayout.TextArea(readme.RichText, editableRichText, GUILayout.Height(textAreaHeight));
                    Rect lastRect = GUILayoutUtility.GetLastRect();
    
                    if (lastRect.x != 0 || lastRect.y != 0)
                    {
                        textAreaRect = lastRect;
                    }
    
                    activeTextAreaStyle = editableRichText;
                }
    
                FixCursorBug();

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
                    fixCursorBug = EditorGUILayout.Toggle("Cursor Correction", fixCursorBug);
                    GUILayout.BeginHorizontal();
    
                    if (GUILayout.Button("Save to File", GUILayout.Width(smallButtonWidth * 4)))
                    {
                        readme.Save();
                    }
    
                    if (GUILayout.Button("Load from File", GUILayout.Width(smallButtonWidth * 4)))
                    {
                        readme.LoadLastSave();
                        Repaint();
                    }
    
                    GUILayout.EndHorizontal();
                }
            }
        }
        
        private void FixCursorBug()
        {
            if (fixCursorBug && textEditor != null && GUI.GetNameOfFocusedControl() == readmeEditorTextAreaName && !TagsError(readme.RichText) && !sourceOn)
            {
                selectIndexChanged = previousSelctIndex != textEditor.selectIndex;
                cursorIndexChanged = previousCursorIndex != textEditor.cursorIndex;
    
                if (selectIndexChanged || cursorIndexChanged)
                {
                    FixMouseCursor();
                    FixArrowCursor();
                }
                
                previousSelctIndex = textEditor.selectIndex;
                previousCursorIndex = textEditor.cursorIndex;
            }
        }
    
        public void FixMouseCursor()
        {
            bool isKeyboard = new KeyCode[] {KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.RightArrow, KeyCode.LeftArrow }.Contains(Event.current.keyCode);
            bool typing = Event.current.character != '\0' || Event.current.keyCode != KeyCode.None;
    
            int selectIndex = textEditor.selectIndex;
    
            if (!typing && Event.current.clickCount <= 1)
            {
                int mousePositionIndex = MousePositionToIndex;
    
                if (selectIndexChanged)
                {
                    textEditor.selectIndex = mousePositionIndex;
                }
    
                if (cursorIndexChanged)
                {
                    textEditor.cursorIndex = mousePositionIndex;
                }
            }
        }
    
        public void FixArrowCursor()
        {
            bool isKeyboard = new KeyCode[] {KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.RightArrow, KeyCode.LeftArrow }.Contains(Event.current.keyCode);
            bool isDoubleClick = Event.current.clickCount == 2;
            if (isKeyboard || isDoubleClick)
            {
                int direction = isDoubleClick ? 1 : 0;
    
                if (isDoubleClick)
                {
                    int mouseIndex = MousePositionToIndex;
                    textEditor.selectIndex = mouseIndex;
                    textEditor.cursorIndex = mouseIndex;
                    textEditor.selectIndex = WordStartIndex;
                    textEditor.cursorIndex = WordEndIndex;
                }
                
                if (cursorIndexChanged)
                {
                    textEditor.cursorIndex = GetNearestPoorTextIndex(textEditor.cursorIndex, previousCursorIndex, direction);
                }
    
                if (selectIndexChanged)
                {
                    textEditor.selectIndex = GetNearestPoorTextIndex(textEditor.selectIndex, previousSelctIndex, direction);
                }
            }
        }
    
        public int GetNearestPoorTextIndex(int index, int previousIndex, int direction = 0)
        {
            int nearestPoorTextIndex = index;
            
            if (IsOnTag(index))
            {
                int selectIndex = textEditor.selectIndex;
                int cursorIndex = textEditor.cursorIndex;
                
                int attempts = readme.richTextTagMap.Count;
    
                if (direction == 0)
                {
                    direction = index - previousIndex;
                }
    
                for (int i = 0; i < attempts && IsOnTag(index); i++)
                {
                    if (index == 0 || index == textEditor.text.Length - 1)
                    {
                        direction *= -1;
                    }   
    
                    previousIndex = index;
                    if (direction > 0)
                    {
                        textEditor.MoveRight();
                        index++;
                    }
                    else
                    {
                        textEditor.MoveLeft();
                        index--;
                    }
                }
    
                nearestPoorTextIndex = index;
                
                textEditor.selectIndex = selectIndex;
                textEditor.cursorIndex = cursorIndex;
            }
    
            return nearestPoorTextIndex;
        }
    
        public bool TagsError(string richText)
        {
            bool tagsError = true;
            bool hasTags = readme.richTextTagMap.Find(isTag => isTag);
    
            if (!hasTags)
            {
                tagsError = false;
            }
            else
            {
                string badTag = "</b>";
                GUIStyle richStyle = new GUIStyle();
                richStyle.richText = true;
                richStyle.wordWrap = false;
    
                richText = richText.Replace('\n', ' ');
    
                float minWidth;
                float maxWidth;
                richStyle.CalcMinMaxWidth(new GUIContent(richText), out minWidth, out maxWidth);
    
                GUILayout.MaxWidth(100000);
                
                float badTagWidth = richStyle.CalcSize(new GUIContent(badTag)).x;
                float textAndBadTagWidth = richStyle.CalcSize(new GUIContent(badTag + richText)).x;
                float textWidth = richStyle.CalcSize(new GUIContent(richText)).x;
    
                if (textWidth != textAndBadTagWidth - badTagWidth)
                {
                    tagsError = false;
                }
            }
    
            return tagsError;
        }
    
        public bool IsOnTag(int index)
        {
            bool isOnTag = false;
    
            if (readme != null && readme.richTextTagMap.Count > index)
            {
                isOnTag = readme.richTextTagMap[index];
            }
    
            return isOnTag;
        }
    
        public int MousePositionToIndex
        {
            get
            {
                int index = -1;
                Vector2 graphicalPosition = Vector2.zero;
                int tmpCursorIndex = textEditor.cursorIndex;
                int tmpSelectIndex = textEditor.selectIndex;
    
                Vector2 goalPosition = Event.current.mousePosition - textAreaRect.position;
    
                float cursorYOffset = activeTextAreaStyle.lineHeight;
                
                textEditor.cursorIndex = 0;
                textEditor.selectIndex = 0;
                MoveCursorToNextPoorChar();
                Vector2 currentGraphicalPosition = GraphicalCursorPos;
                int attempts = 0;
                for (int currentIndex = textEditor.cursorIndex; index == -1; currentIndex = textEditor.cursorIndex)
                {
                    attempts++;
                    if (attempts == 500)
                    {
                        Debug.Log("Yo dude you might want to check this out. ");
                    }
                    if (attempts > 1000)
                    {
                        Debug.Log("Too many attempts at finding mouse cursor position!");
                        break;
                    }
                    
                    bool isEndOfLine = readme.RichText.Length > currentIndex ? readme.RichText[currentIndex] == '\n' : true;
    
                    if (currentGraphicalPosition.y < goalPosition.y - cursorYOffset)
                    {
                        textEditor.MoveRight();
                        MoveCursorToNextPoorChar();
                    }
                    else if (currentGraphicalPosition.x < goalPosition.x && !isEndOfLine)
                    {
                        textEditor.MoveRight();
                        MoveCursorToNextPoorChar();
    
                        if (GraphicalCursorPos.x < currentGraphicalPosition.x)
                        {
                            index = textEditor.cursorIndex;
                        }
                    }
                    else
                    {
                        index = textEditor.cursorIndex;
                    }
    
                    if (textEditor.cursorIndex == readme.RichText.Length)
                    {
                        index = textEditor.cursorIndex;
                    }
                    
                    currentGraphicalPosition = GraphicalCursorPos;
                }
                
                textEditor.cursorIndex = tmpCursorIndex;
                textEditor.selectIndex = tmpSelectIndex;
                
                return index;
            }
        }
    
        public void MoveCursorToNextPoorChar()
        {
            while (readme.RichText.Length - 1 > textEditor.cursorIndex &&
                   readme.richTextTagMap[textEditor.cursorIndex])
            {
                textEditor.MoveRight();   
            }
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