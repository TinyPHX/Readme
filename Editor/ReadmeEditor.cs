using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TP.Readme {

    public class TextAreaObjectField
    {
        private Rect bounds;
        private string objectId;
        
        private static readonly Color textBoxBackgroundColor = new Color(211, 211, 211);

        public TextAreaObjectField(Rect bounds, string objectId)
        {
            this.bounds = bounds;
            this.objectId = objectId;
        }

        public void Draw()
        {
            //TODO get object from objectId.
            Object test = GameObject.FindObjectOfType<GameManager>();
            
            EditorGUI.DrawRect(bounds, textBoxBackgroundColor);
            EditorGUI.ObjectField(bounds, test, typeof(GameManager), false);
        }

        public Rect Bounds
        {
            get { return bounds; }
        }

        public string ObjectId
        {
            get { return objectId; }
        }
    }

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
        private bool fixCursorBug = true;
        private TextEditor textEditor;
        private const string readmeEditorTextAreaName = "Readme Text Editor";
        private int previousSelctIndex = -1;
        private bool selectIndexChanged = false;
        private int previousCursorIndex = -1;
        private bool cursorIndexChanged = false;
        private Rect textAreaRect;
        
        // Styles
        private GUIStyle activeTextAreaStyle;
        private GUIStyle selectableRichText;
        private GUIStyle editableRichText;
        private GUIStyle editableText;
        private int textPadding = 5;

        private List<TextAreaObjectField> textAreaObjectFields = new List<TextAreaObjectField> { };
        
        public override void OnInspectorGUI()
        {
            Readme readmeTarget = (Readme) target;
            if (readmeTarget != null)
            {
                readme = readmeTarget;
            }

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
            
            textEditor = typeof(EditorGUI)
                .GetField("activeEditor", BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null) as TextEditor;
            
            EditorGUILayout.Space();
            
            DrawTextAreaObjectFields(); //Have to draw these first so they get cursor event data.
            
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
                GUI.SetNextControlName(readmeEditorTextAreaName);
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

                if (selectIndexChanged || cursorIndexChanged)
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
                    GUI.enabled = false;
                    SerializedProperty prop = serializedObject.FindProperty("m_Script");
                    EditorGUILayout.PropertyField(prop, true, new GUILayoutOption[0]);
                    GUI.enabled = true;
                    
                    fixCursorBug = EditorGUILayout.Toggle("Cursor Correction", fixCursorBug);
                    EditorGUILayout.LabelField("Cursor Position");
                    EditorGUI.indentLevel++;
                    string richTextWithCursor = readme.RichText;
                    if (textEditor != null && textEditor.selectIndex <= readme.RichText.Length)
                    {
                        richTextWithCursor = richTextWithCursor.Insert(Mathf.Max(textEditor.selectIndex, textEditor.cursorIndex), "|");
                        if (textEditor.selectIndex != textEditor.cursorIndex)
                        {
                            richTextWithCursor = richTextWithCursor.Insert(Mathf.Min(textEditor.selectIndex, textEditor.cursorIndex), "|");
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
                            "textAreaRect.position: " + textAreaRect.position + "\n" +
                            "graphicalCursorPos: " + textEditor.graphicalCursorPos + "\n" +
                            "Calc Cursor Position: " + (Event.current.mousePosition - textAreaRect.position) + "\n" +
                            "cursorIndex: " + textEditor.cursorIndex + "\n" +
                            "selectIndex: " + textEditor.selectIndex + "\n" +
                            "position: " + textEditor.position + "\n" +
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

            UpdateTextAreaObjectFields();
        }

        void UpdateTextAreaObjectFields()
        {
            if (readme.RichText != null && textEditor != null)
            {
                textAreaObjectFields.Clear();
                foreach (Match match in Regex.Matches(readme.RichText, " <o=\"[a-z0-9]*\"></o> ", RegexOptions.None))
                {
                    if (match.Success)
                    {
                        string id = match.Value.Replace(" <o=\"", "").Replace("\"></o> ", "");
                            
                        Debug.Log("Found " + match.Value + " with id " + id + " at position " + match.Index + ".");
                        int startIndex = match.Index;
                        int endIndex = match.Index + match.Value.Length;
                        Rect rect = GetRect(startIndex, endIndex);
                        rect.position += textAreaRect.position;
                        Debug.Log("rect: " + rect);
                        
                        textAreaObjectFields.Add(new TextAreaObjectField(rect, id));
                    }
                }

                DrawTextAreaObjectFields();
            }
        }

        void DrawTextAreaObjectFields()
        {
            if (readme.RichText != null && textEditor != null)
            {
                foreach (TextAreaObjectField textAreaObjectField in textAreaObjectFields)
                {
                    textAreaObjectField.Draw();
                }
            }
        }

        private Rect GetRect(int startIndex, int endIndex)
        {
            Vector2 startPosition = GetGraphicalCursorPos(startIndex) + new Vector2(1, 0);
            Vector2 endPosition = GetGraphicalCursorPos(endIndex) + new Vector2(-1, 0);
            
            GUIStyle singleLineTextArea = new GUIStyle(activeTextAreaStyle);
            singleLineTextArea.wordWrap = false;
            float height = editableRichText.CalcHeight(new GUIContent(""), 100) - 10;
            
            endPosition.y += height;

            Vector2 size = endPosition - startPosition;
            
            Rect rect = new Rect(startPosition, size);

            return rect;
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
                Vector2 currentGraphicalPosition = GetGraphicalCursorPos();
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
                    
                    //TODO: Check for end of word wrapped line.
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
    
                        if (GetGraphicalCursorPos().x < currentGraphicalPosition.x)
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
                    
                    currentGraphicalPosition = GetGraphicalCursorPos();
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

        public Vector2 GetGraphicalCursorPos(int cursorIndex = -1)
        {
            if (textEditor == null)
            {
                return Vector2.zero;
            }
            
            int tmpCursorIndex = -1;
            if (cursorIndex != -1)
            {
                tmpCursorIndex = textEditor.cursorIndex;
                textEditor.cursorIndex = cursorIndex;
            }
            
            Rect position = textEditor.position;
            GUIContent content = textEditor.content;
            int cursorPos = textEditor.cursorIndex;
            
            if (tmpCursorIndex != -1)
            {
                textEditor.cursorIndex = tmpCursorIndex;
            }
            
            return editableRichText.GetCursorPixelPosition(new Rect(0, 0, position.width, position.height), content, cursorPos);
        }
    }
}