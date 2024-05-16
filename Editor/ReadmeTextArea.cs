using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TP
{
    public class ReadmeTextArea
    {
        private Readme readme;
        private Editor editor;

        public string Text { get; private set; } = "";
        
        private int instanceId;
        private Action<string, TextAreaObjectField[]> textAreaChangeCallback;
        private string emptyText = "";

        private TextAreaObjectField[] objectFields;
        
        // Scrolling
        public Vector2 Scroll { get; private set; } = new Vector2();
        private bool pendingScrollUpdate = false;
        public bool ScrollEnabled { get; set; } = true;
        private int scrollMaxHeight = 400;
        private int scrollAreaPad = 6;
        private int scrollBarSize = 15;
        private bool mouseDownInScrollArea;
        private (int, int) previousCursor = (-1, -1);
        
        private float availableWidth;

        public bool Editing { get; private set; }
        public bool SourceOn { get; private set; }
        private bool empty;
        private string controlName;
        private GUIStyle style;
        private bool selectable;
        
        private Rect textAreaRect;
        private Rect scrollAreaRect;
        private Rect intersectRect; // Overlaying area between textArea and scrollArea.
        
        private GUIStyle activeTextAreaStyle;
        private GUIStyle emptyRichTextStyle;
        private GUIStyle selectableRichTextStyle;
        private GUIStyle editableRichTextStyle;
        private GUIStyle editableTextStyle;

        public string ActiveName { get; private set; } = "";
        public string EmptyName { get; }
        public string ReadonlyName { get; }
        public string SourceName { get; }
        public string StyleName { get; }

        // Delayed function call parameters. This is a workaround to not having access to coroutines in Unity Editors.  
        private int updateFocusFrame = int.MaxValue;
        private int updateReturnFocusFrame = int.MaxValue;
        private int updateCursorFrame = int.MaxValue;
        private int updateForceTextEditor = int.MaxValue;
        private (int, int) savedCursorFocus;
        private string savedWindowFocus = "";
        
        // Drag and drop object fields
        private Object[] objectsToDrop;
        private Vector2 objectDropPosition;
        private int updateObjectFieldsFrame = int.MaxValue;
        
        private bool inputTextChanged;
        
        private int frame = 0;

        private ReadmeTextEditor textEditor => ReadmeTextEditor.Instance;

        private Event currentEvent => new Event(Event.current);
        
        public ReadmeTextArea(Editor editor, Readme readme, TextAreaObjectField[] objectFields, Action<string, TextAreaObjectField[]> textAreaChangeCallback, string emptyText)
        {
            this.editor = editor;
            this.readme = readme;
            instanceId = readme.GetInstanceID();
            this.textAreaChangeCallback = textAreaChangeCallback;
            this.emptyText = emptyText;

            foreach (var objectField in objectFields)
            {   
                if (objectField != null && !objectField.IdInSync)
                {
                    ReadmeManager.AddObjectIdPair(objectField.ObjectRef, objectField.ObjectId);
                }
            }
            
            this.objectFields = objectFields;
            
            EmptyName = GetName(editing:false, empty:true);
            ReadonlyName = GetName(editing:false, empty:false);
            SourceName = GetName(editing:true, sourceOn:true);
            StyleName = GetName(editing:true, sourceOn:false);
            
            textEditor.RegisterTextArea(this);
            
            Update();
        }

        public void Draw(bool editing, bool sourceOn, string text)
        {
            frame++;
            
            ProcessInputText(editing, sourceOn, text);
            DragAndDropObjectField();
            UpdateAvailableWidth();
            ScrollToCursor();

            // if (!Editing)
            // {
            //     DrawTextAreaObjectFields(); //Bit of a weird hack to get events to execute in the correct order. Without this clicking object fields doesn't highlight where it is in the project window.
            // }
            DrawTextAreaObjectFields();
            DrawTextArea();
            DrawTextAreaObjectFields();
            
            UpdateObjectFields();
        }

        private void UpdateAvailableWidth()
        {
            EditorGUILayout.Space();
            float defaultWidth = availableWidth != 0 ? availableWidth : EditorGUIUtility.currentViewWidth - 20;
            float newWidth = ReadmeUtil.GetLastRect(new Rect(0, 0, defaultWidth, 0)).width;
            if (newWidth != availableWidth)
            {
                availableWidth = newWidth;
                if (ActiveControl.Style != null)
                {
                    ActiveControl.Style.fixedWidth = availableWidth;
                }

                TriggerUpdateObjectFields();
                TriggerUpdateForceEditor(0);
            }
        }
        
        private void DrawTextArea()
        {
            Vector2 size = GetTextAreaSize();
            
            GUILayoutOption[] options = new[] { GUILayout.Width(size.x), GUILayout.Height(size.y) };
            Vector2 scrollAreaSize = new Vector2(size.x + scrollAreaPad, size.y + scrollAreaPad);

            if (ScrollShowing(size.y))
            {
                scrollAreaSize.x += scrollBarSize;
                scrollAreaSize.y = scrollMaxHeight;
            }

            GUILayoutOption[] scrollAreaOptions = new[] { GUILayout.Width(scrollAreaSize.x), GUILayout.Height(scrollAreaSize.y) };
            Scroll = EditorGUILayout.BeginScrollView(Scroll, scrollAreaOptions);

            GUI.SetNextControlName(ActiveName);
            if (Editing)
            {
                textEditor.BeforeTextAreaChange(this);
                string newText = EditorGUILayout.TextArea(Text, Style, options);
                OnTextChanged(newText);
            }
            else
            {
                if (selectable)
                {
                    EditorGUILayout.SelectableLabel(Text, Style, options);
                }
                else
                {
                    EditorGUILayout.LabelField(Text, Style, options);
                }
            }
            
            AddControl(new Control(ActiveName, GetLastControlId(), Style, options));
            
            textAreaRect = ReadmeUtil.GetLastRect(textAreaRect, scrollAreaRect.position);
            
            EditorGUILayout.EndScrollView();
            scrollAreaRect = ReadmeUtil.GetLastRect(scrollAreaRect);

            intersectRect = new Rect()
            {
                x = textAreaRect.x,
                y = textAreaRect.y - 3,
                width = textAreaRect.width,
                height = scrollAreaRect.height - (textAreaRect.y - scrollAreaRect.y) + 3
            };
            
            if (Editing && TagsError())
            {
                EditorGUILayout.HelpBox("Rich text error detected. Check for mismatched tags.", MessageType.Warning);
            }
        }

        #region Refresh Focus Helpers
        public void Update()
        {
            if (ReadmeUtil.UnityInFocus || AwaitingTrigger)
            {
                UpdateForceTextEditor();
                UpdateFocus();
                UpdateReturnFocus();
                UpdateCursor();

                if (AwaitingTrigger)
                {
                    TriggerOnInspectorGUI();
                }
            }

            #if UNITY_2018
            if (frame == 5)
            {
                RepaintTextArea(); // Hack for 2018 which isn't ready until frame 5 to draw object fields. 
            }
            #endif

            return;

            #region Local Methods
            void UpdateForceTextEditor()
            {
                if (Text == "")
                {
                    updateForceTextEditor = int.MaxValue;
                    return;
                }
                
                if (frame >= updateForceTextEditor)
                {
                    if (!textEditor.HasTextEditor)
                    {
                        TriggerUpdateFocus();
                    }
                    else
                    {
                        TriggerUpdateFocus();
                        updateForceTextEditor = int.MaxValue;
                        TriggerUpdateReturnFocus();
                    }
                }
            }
            
            void UpdateFocus()
            {
                if (frame >= updateFocusFrame)
                {
                    if (savedWindowFocus == "" && EditorWindow.focusedWindow != null)
                    {
                        savedWindowFocus = EditorWindow.focusedWindow.titleContent.text;
                    }
                    
                    updateFocusFrame = int.MaxValue;
                    ReadmeUtil.FocusEditorWindow("Inspector");
                    EditorGUI.FocusTextInControl(ActiveName);
                    GUI.FocusControl(ActiveName);
                }
            }

            void UpdateCursor()
            {
                if (frame >= updateCursorFrame)
                {
                    updateCursorFrame = int.MaxValue;
                    if (textEditor != null)
                    {
                        textEditor.SetCursors(savedCursorFocus);
                        savedCursorFocus = (-1, -1);
                    }
                }
            }
            
            void UpdateReturnFocus()
            {
                if (frame >= updateReturnFocusFrame)
                {
                    updateReturnFocusFrame = int.MaxValue;
                    
                    if (savedWindowFocus != "")
                    {
                        ReadmeUtil.FocusEditorWindow(savedWindowFocus);
                        savedWindowFocus = "";
                    }
                }
            }
            #endregion
        }
        
        public void RepaintTextArea(int newCursorIndex = -1, int newSelectIndex = -1, bool focusText = false, int delay = 0)
        {
            TriggerOnInspectorGUI();
            if (HasTextEditorFocus)
            {
                textEditor.SetText(Text);
                textEditor.SetCursors((newCursorIndex, newSelectIndex));
            }

            bool textAlreadyFocused = textEditor != null && GetControlId(GetName()) == textEditor.controlID;
            if (focusText && !textAlreadyFocused)
            {
                TriggerUpdateFocus(2 + delay);
                TriggerUpdateCursor(textEditor.GetCursors(), 4 + delay);
            }
            
            TriggerUpdateObjectFields(delay);
        }

        private void TriggerUpdateForceEditor(int frameDelay = 0)
        {
            updateForceTextEditor = frame + frameDelay;
        }

        private void TriggerUpdateFocus(int frameDelay = 0)
        {
            updateFocusFrame = frame + frameDelay;
        }

        private void TriggerUpdateReturnFocus(int frameDelay = 0)
        {
            updateReturnFocusFrame = frame + frameDelay;
        }

        private void TriggerUpdateCursor((int, int) cursors, int frameDelay = 0)
        {
            updateCursorFrame = frame + frameDelay;
            savedCursorFocus = cursors;
        }

        public bool AwaitingTrigger => updateForceTextEditor != int.MaxValue || updateFocusFrame != int.MaxValue ||
                                       updateReturnFocusFrame != int.MaxValue || updateCursorFrame != int.MaxValue ||
                                       updateObjectFieldsFrame != int.MaxValue;

        private void TriggerOnInspectorGUI()
        {
            editor.Repaint();
        }
        #endregion

        private string GetName()
        {
            return GetName(Editing, SourceOn, Text == "");
        }

        private string GetName(bool editing, bool sourceOn=false, bool empty=false)
        {
            if (editing)
            {
                return "edit_" + (sourceOn ? "source_" : "style_") + instanceId;
            }
            else
            {
                return "view_" + (empty ? "empty_" : "style_") + instanceId;
            }
        }

        
        private void ProcessInputText(bool editing, bool sourceOn, string newText)
        {
            empty = newText == "";
            newText = empty && !editing ? emptyText : newText;
            if (Text != newText || SourceOn != sourceOn || Editing != editing)
            {
                Text = newText;
                textEditor.SetText(Text);
                SourceOn = sourceOn;
                Editing = editing;
                selectable = !empty;
                TriggerUpdateObjectFields();
            }
        }

        private void OnTextChanged(string newText)
        {
            if (Text != newText)
            {
                Text = newText;
                TriggerUpdateObjectFields();
                textAreaChangeCallback(Text, objectFields);
                textEditor.AfterTextAreaChange(this);
            }
        }

        #region ObjectField Helpers
        
        private void DrawTextAreaObjectFields()
        {
            if (!SourceOn || !Editing)
            {
                EditorGUI.BeginDisabledGroup(!Editing);
                foreach (TextAreaObjectField textAreaObjectField in objectFields)
                {
                    textAreaObjectField.Draw(textEditor.TextEditor, -Scroll, Bounds);
                }
                EditorGUI.EndDisabledGroup();
            }
        }

        private void TextAreaObjectFieldChanged(TextAreaObjectField changed)
        {
            string newText = Text
                .Remove(changed.IdIndex, changed.ObjectId.ToString().Length)
                .Insert(changed.IdIndex, changed.ObjectId.ToString());

            OnTextChanged(newText);
        }

        private List<(int, int, int, int)> Matches => UpdateMatches();

        private List<(int, int, int, int)> UpdateMatches()
        {
            List<(int, int, int, int)> matches = new List<(int, int, int, int)>();
            string objectTagPattern = "<o=\"[-,a-zA-Z0-9]{7}\"></o>";
            MatchCollection matchCollection = Regex.Matches(Text, objectTagPattern, RegexOptions.None);
            int matchIndex = 0;
            foreach (Match match in matchCollection)
            {
                if (match.Success)
                {
                    string idValue = match.Value.Replace("<o=\"", "").Replace("\"></o>", "");
                    bool parseSuccess = int.TryParse(idValue, out int objectId);
                    if (!parseSuccess)
                    {
                        Debug.LogWarning("Unable to parse id: " + idValue);
                    }
                    else
                    {
                        int startIndex = match.Index;
                        int endIndex = match.Index + match.Value.Length;
                        matches.Insert(0, (objectId, matchIndex, startIndex, endIndex)); // Add in reverse order for easy iteration where you modify indices. 
                        matchIndex++; 
                    }
                }
            }

            return matches;
        }

        private void PadObjectFieldsWithSpaces()
        {
            string newText = Text;

            foreach (var (_, _, startIndex, endIndex) in Matches)
            {
                // 1 space after all object fields
                bool firstEndSpace = endIndex < newText.Length && newText[endIndex] == ' ';
                if (!firstEndSpace)
                {
                    newText = newText.Insert(endIndex, " ");
                }
                
                // 2 spaces between all back to back object fields
                bool secondEndSpace = endIndex + 1 < newText.Length && newText[endIndex + 1] == ' ';
                bool objectTagAfter = endIndex + 4 < newText.Length && newText.Substring(endIndex + 1, 3) == "<o=";
                if (objectTagAfter && !secondEndSpace)
                {
                    newText = newText.Insert(endIndex + 1, " ");
                }
                
                // 1 space before all object fields
                bool startSpace = startIndex > 0 && Text[startIndex - 1] == ' ';
                if (!startSpace)
                {
                    newText = newText.Insert(startIndex, " ");
                }
            }
            
            OnTextChanged(newText);
        }

        private void TriggerUpdateObjectFields(int frameDelay = 2)
        {
            updateObjectFieldsFrame = frame + frameDelay;
        }
        
        private void UpdateObjectFields()
        {
            if (frame >= updateObjectFieldsFrame)
            {
                updateObjectFieldsFrame = int.MaxValue;

                PadObjectFieldsWithSpaces();

                TextAreaObjectField[] newObjectFields = new TextAreaObjectField[Matches.Count];
                
                foreach (var (objectId, i, startIndex, endIndex) in Matches)
                {
                    Rect rect = textEditor.GetRect(startIndex - 1, endIndex + 1, Text);
                    Rect rectWithCorrectHeight = textEditor.GetRect(startIndex - 1, endIndex, Text); // Have to do this for when a space is moved to the next line.
                    rect.height = rectWithCorrectHeight.height;
                    TextAreaObjectField newField = new TextAreaObjectField(rect, objectId, startIndex, endIndex - startIndex, TextAreaObjectFieldChanged);
                    newObjectFields[i] = newField;
                };

                if (!TextAreaObjectField.BaseFieldsEqual(objectFields, newObjectFields))
                {
                    textAreaChangeCallback(Text, newObjectFields);
                }

                if (!TextAreaObjectField.AllFieldsEqual(objectFields, newObjectFields))
                {
                    objectFields = newObjectFields;
                }
            }
        }

        private bool IsMouseOverObjectField()
        {
            foreach (var objectField in objectFields)
            {
                if (objectField.AbsoluteFieldRect.Contains(currentEvent.mousePosition))
                {
                    return true;
                }
            }

            return false;
        }

        private void DragAndDropObjectField()
        {
            if (Editing && ReadmeUtil.UnityInFocus)
            {
                switch (currentEvent.type)
                {
                    case EventType.DragUpdated:
                    case EventType.DragPerform:
                        if (!Contains(currentEvent.mousePosition))
                        {
                            return; // Ignore drag and drop outside of textArea
                        }

                        foreach (TextAreaObjectField textAreaObjectField in objectFields)
                        {
                            if (textAreaObjectField.FieldRect.Contains(currentEvent.mousePosition))
                            {
                                return; // Ignore drag and drop over current Object Fields
                            }
                        }

                        DragAndDrop.visualMode = DragAndDropVisualMode.Link;

                        if (currentEvent.type == EventType.DragPerform && objectsToDrop == null && !IsMouseOverObjectField())
                        {
                            DragAndDrop.AcceptDrag();

                            objectsToDrop = DragAndDrop.objectReferences;
                            objectDropPosition = currentEvent.mousePosition;
                        }

                        break;
                }

                if (objectsToDrop != null && textEditor != null)
                {
                    int dropIndex = textEditor.PositionToIndex(objectDropPosition);
                    if (dropIndex == -1) // Dropped on last line away from last character.
                    {
                        dropIndex = Text.Length;
                    }
                    dropIndex = GetNearestPoorTextIndex(dropIndex);
                    InsertObjectFields(dropIndex, objectsToDrop);
                    objectsToDrop = null;
                    objectDropPosition = Vector2.zero;
                    Undo.RecordObject(readme, "object field added");
                }
            }
        }
        
        private void InsertObjectFields(int index, Object[] objects = null)
        {
            for (int i = objects.Length - 1; i >= 0; i--)
            {
                Object objectDragged = objects[i];

                AddObjectField(index, ReadmeManager.GetIdFromObject(objectDragged).ToString());
            }
        }

        public void AddObjectField(int index = -1, string id = "0000000")
        {
            if (textEditor != null)
            {
                if (index == -1)
                {
                    index = textEditor.CursorIndex;
                }

                string objectString = " <o=\"" + ReadmeUtil.GetFixedLengthId(id) + "\"></o> ";
                string newText = Text.Insert(index, objectString);

                int newIndex = GetNearestPoorTextIndex(index + objectString.Length);

                OnTextChanged(newText);
                RepaintTextArea(newIndex, newIndex, true);
            }
        }

        #endregion

        public void UpdateGuiStyles(GUIStyle emptyRichText, GUIStyle selectableRichText, GUIStyle editableRichText, GUIStyle editableText)
        {
            this.emptyRichTextStyle = emptyRichText;
            this.selectableRichTextStyle = selectableRichText;
            this.editableRichTextStyle = editableRichText;
            this.editableTextStyle = editableText;
        }

        public GUIStyle GetGuiStyle(string activeName)
        {
            GUIStyle style = new GUIStyle();
            if (activeName == EmptyName) { style = emptyRichTextStyle; }
            else if (activeName == ReadonlyName) { style = selectableRichTextStyle; }
            else if (activeName == StyleName) { style = editableRichTextStyle; }
            else if (activeName == SourceName) { style = editableTextStyle; }
            return style;
        }

        private bool ScrollShowing(float height) => ScrollEnabled && height + scrollAreaPad > scrollMaxHeight;

        public Vector2 GetTextAreaSize()
        {
            int padding = -10;
            Vector2 size = CalcSize(Text, padding, 0);
            if (ScrollShowing(size.y))
            {
                size = CalcSize(Text, padding - scrollBarSize);
            }

            return size;

            Vector2 CalcSize(string text, float xPadding = 0, float yPadding = 0)
            {
                Vector2 calculatedSize = new Vector2();
                calculatedSize.x = availableWidth + xPadding;
                calculatedSize.y = CalcHeight(text, calculatedSize.x) + yPadding;
                return calculatedSize;
            }
        }

        public GUIStyle Style => GetGuiStyle(GetName(Editing, SourceOn, empty));
        public GUIStyle TextAreaStyle => ActiveControl.Style ?? editableRichTextStyle;
        public bool Contains(Vector2 position) => intersectRect.Contains(position);
        public bool InvalidClick => currentEvent.type == EventType.MouseDown && textAreaRect.Contains(currentEvent.mousePosition) && !scrollAreaRect.Contains(currentEvent.mousePosition);      
        public float lineHeight => Style.lineHeight;
        public Rect Bounds => new Rect(intersectRect);
        public bool HasGuiFocus => HasControl(GUI.GetNameOfFocusedControl());
        public bool HasTextEditorFocus => HasControl(textEditor?.controlID ?? -1);

        public float CalcHeight(string content = " ", float width = 100, int fontSize = 0)
        {
            if (fontSize > 0)
            {
                content = $"<size={fontSize}>{content}</size>";
            }

            return Style.CalcHeight(new GUIContent(content), width);
        }

        public Vector2 GetCursorPixelPosition(int cursorIndex, string text = null)
        {
            ReadmeUtil.SetIfNull(ref text, () => Text);
            Vector2 standardPosition = new Vector2(21, 13);
            Rect tempRect = textAreaRect;
            if (textAreaRect.x < standardPosition.x && textAreaRect.y < standardPosition.y)
            {
                tempRect.position = standardPosition;
                tempRect.size = GetTextAreaSize();
            }
            
            return TextAreaStyle.GetCursorPixelPosition(tempRect, new GUIContent(text), cursorIndex);
        }

        private void ScrollToCursor()
        {
            if (Event.current.type == EventType.MouseDown)
            {
                mouseDownInScrollArea = intersectRect.Contains(Event.current.mousePosition);
            }

            if (!previousCursor.Equals(textEditor.GetCursors()))
            {
                pendingScrollUpdate = true;
                previousCursor = textEditor.GetCursors();
            }

            if (pendingScrollUpdate)
            {
                Vector2 resultScroll = Scroll;

                if (textEditor.HasTextEditor)
                {
                    int index = textEditor.GetCursors().Item1;
                    Rect cursorRect = textEditor.GetRect(index, index);
                    cursorRect.position -= Scroll;
                    bool dragScroll = mouseDownInScrollArea && currentEvent.type == EventType.MouseDrag;
                    bool fullScroll = !textEditor.AllTextSelected() && currentEvent.isKey;
                    float topDiff = Mathf.Min(0, cursorRect.yMin - scrollAreaRect.yMin);
                    float bottomDiff = -Mathf.Min(0, scrollAreaRect.yMax - cursorRect.yMax);
                    float scrollDiff = topDiff + bottomDiff;

                    if (scrollDiff != 0)
                    {
                        if (dragScroll)
                        {
                            resultScroll.y += Mathf.Sign(scrollDiff) * cursorRect.height; //Scroll one line at a time. 
                            pendingScrollUpdate = false;
                        }

                        if (fullScroll)
                        {
                            resultScroll.y += scrollDiff; //Scroll full distance to cursor 
                            pendingScrollUpdate = false; 
                        }
                    }
                }

                Scroll = resultScroll;

                RepaintTextArea();
            }
        }

        public bool RichTextDisplayed => !SourceOn && !TagsError();
        
        public int GetNearestPoorTextIndex(int index, int direction = 0)
        {
            index = Mathf.Clamp(index, 0, Text.Length);
            
            int maxRight = Text.Length - index;
            for (int i = 0; direction >= 0 && i < maxRight; i++)
            {
                if (!IsInTag(index + i))
                {
                    return (index + i);
                }
            }

            if (direction == 1)
            {
                return Text.Length;
            }
            
            int maxLeft = index + 1;
            for (int i = 0; direction <= 0 && i < maxLeft; i++)
            {
                if (!IsInTag(index - i))
                {
                    return (index - i);
                }
            }

            return 0;
        }

        public bool TagsError()
        {
            bool tagsError = true;
            bool hasTags = Text.Contains("<b>") || Text.Contains("<\\b>") ||
                           Text.Contains("<i>") || Text.Contains("<\\i>") ||
                           Text.Contains("<size") || Text.Contains("<\\size>") ||
                           Text.Contains("<color") || Text.Contains("<\\color>");

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

                string tempRichText = Text.Replace('\n', ' ');

                float minWidth;
                float maxWidth;
                richStyle.CalcMinMaxWidth(new GUIContent(tempRichText), out minWidth, out maxWidth);

                GUILayout.MaxWidth(100000);

                float badTagWidth = richStyle.CalcSize(new GUIContent(badTag)).x;
                float textAndBadTagWidth = richStyle.CalcSize(new GUIContent(badTag + tempRichText)).x;
                float textWidth = richStyle.CalcSize(new GUIContent(tempRichText)).x;

                if (textWidth != textAndBadTagWidth - badTagWidth)
                {
                    tagsError = false;
                }
            }

            return tagsError;
        }

        public bool IsInTag(int index)
        {
            if (index == 0 || index == Text.Length)
            {
                return false;
            }

            return IsOnTag(index) && IsOnTag(index - 1); 
        }

        public bool IsOnTag(int index)
        {
            bool isOnTag = false;

            if (readme != null && readme.richTextTagMap != null && readme.richTextTagMap.Count > index)
            {
                try
                {
                    isOnTag = readme.richTextTagMap[index];
                }
                catch (Exception exception)
                {
                    Debug.Log("Issue checking for tag: " + exception);
                }
            }

            return isOnTag;
        }
        
        private Dictionary<int, Control> ControlIdToName { get; } = new Dictionary<int, Control>();
        private Dictionary<string, Control> ControlNameToId { get; } = new Dictionary<string, Control>();
        
        private int GetLastControlId() 
        {
            int lastControlID = -1;

            Type type = typeof(EditorGUIUtility);
            FieldInfo field = type.GetField("s_LastControlID", BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
            {
                lastControlID = (int)field.GetValue(null);
            }

            return lastControlID;
        }
        
        public struct Control
        {
            public string Name { get; }
            public int ID { get; }
            public GUIStyle Style { get; }
            public GUILayoutOption[] Options { get; }

            public Control(string name, int id, GUIStyle style, GUILayoutOption[] Options)
            {
                this.Name = name;
                this.ID = id;
                this.Style = style;
                this.Options = Options;
            }

            public bool RichTextSupported => Style.richText;
        }

        public Control ActiveControl { get; set; } = default;

        private void AddControl(Control control)
        {   
            ControlIdToName[control.ID] = control;
            ControlNameToId[control.Name] = control;
            ActiveControl = control;
        }

        public int GetControlId(string controlName)
        {
            return !ControlNameToId.TryGetValue(controlName, out Control control) ? -1 : control.ID;
        }

        public string GetControlName(int controlId)
        {
            return !ControlIdToName.TryGetValue(controlId, out Control control) ? controlId.ToString() : control.Name;
        }

        public bool HasControl(int controlId)
        {
            return ControlIdToName.TryGetValue(controlId, out Control control);
        }

        public bool HasControl(string controlName)
        {
            return ControlNameToId.TryGetValue(controlName, out Control control);
        }
    }
}