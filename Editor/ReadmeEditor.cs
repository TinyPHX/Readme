#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace TP.Readme {
    [CustomEditor(typeof(Readme)), ExecuteInEditMode]
    public class ReadmeEditor : Editor
    {
        private Readme readme;

        private bool verbose = false;
        private bool debugButtons = false;
        
        // State
        private bool editing = false;
        private bool boldOn = false;
        private bool italicOn = false;
        private bool sourceOn = false;
        
        // Cursor fix 
        private bool fixCursorBug = true;
        private TextEditor textEditor;
        private string readmeEditorActiveTextAreaName = "";
        private const string readmeEditorTextAreaReadonlyName = "readme_text_editor_readonly";
        private const string readmeEditorTextAreaSourceName = "readme_text_editor_source";
        private const string readmeEditorTextAreaStyleName = "readme_text_editor_style";
        private bool selectIndexChanged = false;
        private bool cursorIndexChanged = false;
        private int previousCursorIndex = -1;
        private int previousSelectIndex = -1;
        private int currentCursorIndex = -1;
        private int currentSelectIndex = -1;
        private Rect textAreaRect;
        private bool richTextChanged = false;
        private string previousFocusedWindow = "";
        private bool windowFocusModified = false;
        private bool backspaceAdjust = false;
        
        // Styles
        private GUIStyle activeTextAreaStyle;
        private GUIStyle selectableRichText;
        private GUIStyle editableRichText;
        private GUIStyle editableText;
        private int textPadding = 5;

        // Text area focus
        private bool textAreaRefreshPending = false;
        private int focusDelay = 0;
        private int focusCursorIndex = -1;
        private int focuseSelectIndex = -1;

        // Drag and drop object fields
        private Object[] objectsToDrop;
        
        //Copy buffer fix
        private string previousCopyBuffer;

        private Event currentEvent;

        public void UpdateGuiStyles()
        {
            selectableRichText = new GUIStyle
            {
                focused = {textColor = readme.fontColor},
                normal = {textColor = readme.fontColor},
                font = readme.font,
                fontSize = readme.fontSize,
                wordWrap = true,
                padding = new RectOffset(textPadding, textPadding, textPadding + 2, textPadding)
            };

            editableRichText = new GUIStyle(GUI.skin.textArea)
            {
                richText = true,
                font = readme.font,
                fontSize = readme.fontSize,
                wordWrap = true,
                padding = new RectOffset(textPadding, textPadding, textPadding, textPadding)
            };

            editableText = new GUIStyle(GUI.skin.textArea)
            {
                richText = false,
                wordWrap = true,
                padding = new RectOffset(textPadding, textPadding, textPadding, textPadding)
            };
        }
        
        public override void OnInspectorGUI()
        {
//            if (verbose) {  Debug.Log("README: OnInspectorGUI"); }
            currentEvent = new Event(Event.current);

            Readme readmeTarget = (Readme) target;
            if (readmeTarget != null)
            {
                readme = readmeTarget;
            }

            bool empty = readme.Text == "";

            UpdateGuiStyles();

            float textAreaWidth = EditorGUIUtility.currentViewWidth - 19;
            if (TextAreaRect.width > 0)
            {
                textAreaWidth = TextAreaRect.width;
            }
            float textAreaHeight = editableRichText.CalcHeight(new GUIContent(readme.RichText), textAreaWidth);
            float smallButtonWidth = EditorGUIUtility.singleLineHeight * 2;

            UpdateTextEditor();
            UpdateTextAreaObjectFields();
            
            EditorGUILayout.Space();

            if (!editing)
            {
                if (empty)
                {
                    EditorGUILayout.HelpBox("Click edit to add your readme!", MessageType.Info);
                }
                else
                {
                    String displayText = !TagsError(readme.RichText) ? readme.RichText : readme.Text;
                    readmeEditorActiveTextAreaName = readmeEditorTextAreaReadonlyName;
                    GUI.SetNextControlName(readmeEditorTextAreaReadonlyName);
                    EditorGUILayout.SelectableLabel(displayText, selectableRichText, GUILayout.Height(textAreaHeight + 4));
                    activeTextAreaStyle = selectableRichText;
                    TextAreaRect = GUILayoutUtility.GetLastRect();
                }
            }
            else
            {
                if (sourceOn)
                {
                    readmeEditorActiveTextAreaName = readmeEditorTextAreaSourceName;
                    GUI.SetNextControlName(readmeEditorTextAreaSourceName);
                    readme.RichText = EditorGUILayout.TextArea(readme.RichText, editableText, GUILayout.Height(textAreaHeight));
                    TextAreaRect = GUILayoutUtility.GetLastRect();
                }
                else
                {
                    
                    int inputCurosrIndex = CursorIndex;
                    int inputSelectIndex = SelectIndex;
                    
                    readmeEditorActiveTextAreaName = readmeEditorTextAreaStyleName;
                    GUI.SetNextControlName(readmeEditorTextAreaStyleName);

                    PrepareForTextAreaChange(readme.RichText);
                    string newRichText = EditorGUILayout.TextArea(readme.RichText, editableRichText, GUILayout.Height(textAreaHeight));
                    readme.RichText = GetTextAreaChange(readme.RichText, inputCurosrIndex, inputSelectIndex, newRichText, CursorIndex, SelectIndex);
//                    if (TextEditorActive) { TextEditor.text = readme.RichText; }
                    TextAreaChangeComplete();
                    
                    TextAreaRect = GUILayoutUtility.GetLastRect();
                    activeTextAreaStyle = editableRichText;
                    
//                    int endValue = textEditor.cursorIndex;
//                    if (startValue != endValue)
//                    {
//                        Debug.Log("OMG ITS HAPPENING!");
//                    }
                }

                if (TagsError(readme.RichText))
                {
                    EditorGUILayout.HelpBox("Rich text error detected. Check for mismatched tags.", MessageType.Warning);
                }

                if (selectIndexChanged || cursorIndexChanged)
                {
                    UpdateStyleState();
                }

                Undo.RecordObject(target, "Readme");
            }
            
            FixCursorBug();
            
            EditorGUILayout.Space();
    
            if (!editing)
            {
                if (GUILayout.Button("Edit"))
                {
                    editing = true;
                    ForceTextAreaRefresh(-1, -1, 2);
                }
            }
            else
            {
                if (GUILayout.Button("Done"))
                {
                    editing = false;
                    ForceTextAreaRefresh();
                }
                
                GUILayout.BeginHorizontal();
                
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
                
                GUIStyle objButtonStyle = new GUIStyle(EditorStyles.toolbarButton);
                if (GUILayout.Button(new GUIContent("Obj", "Insert Object Field (alt+o)"), objButtonStyle, GUILayout.Width(smallButtonWidth)))
                {
                    AddObjectField();
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
                    EditorGUILayout.HelpBox("Source mode enabled! Supported tags:\n" + 
                                            " <b></b>\n" +
                                            " <i></i>\n" + 
                                            " <color=\"#00ffff\"></color>\n" + 
                                            " <size=\"20\"></size>\n" + 
                                            " <o=\"-01234\"></o> - uses GameObject.GetInstanceId()", 
                        MessageType.Info);
                }
            }

            if (editing || Readme.advancedOptions)
            {
                Readme.advancedOptions = EditorGUILayout.Foldout(Readme.advancedOptions, "Advanced");
            }

            if (Readme.advancedOptions)
            {
                EditorGUI.indentLevel++;
                GUI.enabled = false;
                SerializedProperty prop = serializedObject.FindProperty("m_Script");
                EditorGUILayout.PropertyField(prop, true, new GUILayoutOption[0]);
                GUI.enabled = true;
                
                fixCursorBug = EditorGUILayout.Toggle("Cursor Correction", fixCursorBug);
                verbose = EditorGUILayout.Toggle("Verbose", verbose);
                EditorGUILayout.LabelField("Cursor Position");
                string richTextWithCursor = readme.RichText;
                if (TextEditorActive && SelectIndex <= readme.RichText.Length)
                {
                    richTextWithCursor = richTextWithCursor.Insert(Mathf.Max(SelectIndex, CursorIndex), "|");
                    if (SelectIndex != CursorIndex)
                    {
                        richTextWithCursor = richTextWithCursor.Insert(Mathf.Min(SelectIndex, CursorIndex), "|");
                    }
                }
                richTextWithCursor = richTextWithCursor.Replace("\n", " \\n\n");
                float adjustedTextAreaHeight = editableRichText.CalcHeight(new GUIContent(richTextWithCursor), textAreaWidth - 50);
                EditorGUILayout.TextArea(richTextWithCursor, editableText, GUILayout.Height(adjustedTextAreaHeight));

                EditorGUILayout.HelpBox(
                    "mousePosition: " + Event.current.mousePosition + "\n" +
                    "FocusedWindow: " + EditorWindow.focusedWindow + "\n" +
                    "FocusedControlName: " + GUI.GetNameOfFocusedControl() + "\n" +
                    "textAreaRect: " + TextAreaRect + "\n" +
                    "graphicalCursorPos: " + (!TextEditorActive ? "" : TextEditor.graphicalCursorPos.ToString()) + "\n" +
                    "Calc Cursor Position: " + (Event.current.mousePosition - TextAreaRect.position) + "\n" +
                    "Text Editor Active: " + TextEditorActive + "\n" +
                    "cursorIndex: " + (!TextEditorActive ? "" : CursorIndex.ToString()) + "\n" +
                    "selectIndex: " + (!TextEditorActive ? "" : SelectIndex.ToString()) + "\n" +
                    "cursorIndex OnTag: " + IsOnTag(CursorIndex) + "\n" +
                    "selectIndex OnTag: " + IsOnTag(SelectIndex) + "\n" +
                    "position: " + (!TextEditorActive ? "" : TextEditor.position.ToString()) + "\n" +
                    "TagsError: " + TagsError(readme.RichText) + "\n" +
                    "Style Map Info: " + "\n" +
                    "\t<b> tags:" + (readme.StyleMaps.ContainsKey("b") ? readme.StyleMaps["b"].FindAll(isStyle => isStyle).Count.ToString() : "0") + "\n" + 
                    "\t<i> tags:" + (readme.StyleMaps.ContainsKey("i") ? readme.StyleMaps["i"].FindAll(isStyle => isStyle).Count.ToString() : "0") + "\n" + 
                    ""
                    , MessageType.Info);

                MessageType messageType = TextEditor != null ? MessageType.Info : MessageType.Warning;
                EditorGUILayout.HelpBox("Text Editor Found: " + (TextEditor != null ? "Yes" : "No"), messageType);
                
                EditorGUILayout.HelpBox("Shortcuts: \n" + 
                    "\tBold: alt+b\n" +
                    "\tItalic: alt+i" +
                    "\tAdd Obj: alt+o"
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


                if (editing || debugButtons)
                {
                    debugButtons = EditorGUILayout.Foldout(debugButtons, "Debug Buttons");
                }

                if (debugButtons)
                {
                    float debugButtonWidth = smallButtonWidth * 6;
                    if (GUILayout.Button("ForceTextAreaRefresh", GUILayout.Width(debugButtonWidth)))
                    {
                        ForceTextAreaRefresh();
                    }

                    if (GUILayout.Button("GUI.FocusControl", GUILayout.Width(debugButtonWidth)))
                    {
                        GUI.FocusControl(readmeEditorActiveTextAreaName);
                    }

                    if (GUILayout.Button("OnGui", GUILayout.Width(debugButtonWidth)))
                    {
                        EditorUtility.SetDirty(readmeTarget.gameObject);
                    }

                    if (GUILayout.Button("AssignTextEditor", GUILayout.Width(debugButtonWidth)))
                    {
                        UpdateTextEditor();
                    }

                    if (GUILayout.Button("SetDirty", GUILayout.Width(debugButtonWidth)))
                    {
                        EditorUtility.SetDirty(readme);
                    }

                    if (GUILayout.Button("Repaint", GUILayout.Width(debugButtonWidth)))
                    {
                        Repaint();
                    }

                    if (GUILayout.Button("RecordObject", GUILayout.Width(debugButtonWidth)))
                    {
                        Undo.RecordObject(readme, "Force update!");
                    }

                    if (GUILayout.Button("FocusTextInControl", GUILayout.Width(debugButtonWidth)))
                    {
                        EditorGUI.FocusTextInControl(readmeEditorActiveTextAreaName);
                    }

                    if (GUILayout.Button("Un-FocusTextInControl", GUILayout.Width(debugButtonWidth)))
                    {
                        EditorGUI.FocusTextInControl("");
                    }
                }

                EditorGUI.indentLevel--;
            }
            
            CheckKeyboardShortCuts();

            UpdateTextAreaObjectFields();
            DragAndDropObjectField();
            
            UpdateFocus();
            
            FixCopyBuffer();
        }

        private TextEditor GetTextEditor()
        {
            return typeof(EditorGUI)
                .GetField("activeEditor", BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null) as TextEditor;
        }

        private void UpdateTextEditor()
        {
            TextEditor newTextEditor = GetTextEditor();

            if (newTextEditor == null)
            {
                FocusOnInspectorWindow();
            }

            if (newTextEditor != null)
            {
                if (TextEditor != newTextEditor)
                {
                    TextEditor = newTextEditor;
                    if (verbose) { Debug.Log("README: Text Editor assigned!"); }

                    if (TextEditorActive)
                    {
                        ForceTextAreaRefresh();
                    }
                }
            }
            else if (TextEditor == null)
            {
                if (verbose) { Debug.Log("README: Text Editor not found!"); }

                ForceTextAreaRefresh();
            }
        }

        private void FocusOnInspectorWindow()
        {
            if (EditorWindow.focusedWindow != null)
            {
                string currentFocusedWindow = EditorWindow.focusedWindow.titleContent.text;
                if (currentFocusedWindow == "Hierarchy")
                {
                    FocusEditorWindow("Inspector");

                    previousFocusedWindow = currentFocusedWindow;
                    windowFocusModified = true;
                }
            }
        }

        private void FocusEditorWindow(string windowTitle)
        {
            EditorWindow inspectorWindow = GetEditorWindow(windowTitle);
            if (inspectorWindow != default(EditorWindow))
            {
                inspectorWindow.Focus();
            }
        }

        private EditorWindow GetEditorWindow(string windowTitle)
        {
            EditorWindow[] allWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            EditorWindow editorWindow = allWindows.SingleOrDefault(window => window.titleContent.text == windowTitle);

            return editorWindow;
        }

        void ForceGuiRedraw()
        {
            Undo.RecordObject(readme, "Force update!");
            EditorUtility.SetDirty(readme);
            UpdateTextAreaObjectFields();
        }

        void ForceTextAreaRefresh(int selectIndex = -1, int cursorIndex = -1, int delay = 10)
        {
            delay = 3;
            if (!textAreaRefreshPending)
            {
                if (verbose) {  Debug.Log("README: ForceTextAreaRefresh, selectIndex: " + selectIndex + " cursorIndex: " + cursorIndex); }
                textAreaRefreshPending = true;

                string forcusedControl = GUI.GetNameOfFocusedControl();
                if (forcusedControl != readmeEditorTextAreaReadonlyName && forcusedControl != "")
                {
                    EditorGUI.FocusTextInControl("");
                    GUI.FocusControl("");
                }
                focusDelay = delay;
                focuseSelectIndex = selectIndex == -1 && TextEditorActive ? SelectIndex : selectIndex;
                focusCursorIndex = cursorIndex == -1 && TextEditorActive ? CursorIndex : cursorIndex;
                ForceGuiRedraw();
            }
        }

        private void FocusTextArea()
        {
            textAreaRefreshPending = false;
            EditorGUI.FocusTextInControl(readmeEditorActiveTextAreaName);
            GUI.FocusControl(readmeEditorActiveTextAreaName);
            ForceGuiRedraw();
        }

        private void UpdateFocus()
        {
            if (textAreaRefreshPending)
            {
                if (verbose) {  Debug.Log("README: textAreaNeedsFocus, focusDelay: " + focusDelay); }
                if (focusDelay <= 0)
                {
                    if (verbose) {  Debug.Log("README: FocusTextArea: " + readmeEditorActiveTextAreaName + " selectIndex: " + focuseSelectIndex + " cursorIndex: " + focusCursorIndex); }
                    FocusTextArea();
                }
                else
                {
                    focusDelay--;
                    ForceGuiRedraw();
                }
            }
            else
            {
                if (focusCursorIndex != -1)
                {
                    CursorIndex = focusCursorIndex;
                    focusCursorIndex = -1;
                }

                if (focuseSelectIndex != -1)
                {
                    SelectIndex = focuseSelectIndex;
                    focuseSelectIndex = -1;
                }
                
                //Stop cursor change from being detected
//                previousSelectIndex = SelectIndex;
//                previousCursorIndex = CursorIndex;
            }

            if (windowFocusModified && textEditor != null)
            {
                windowFocusModified = false;
                FocusEditorWindow(previousFocusedWindow);
            }
        }

        void UpdateTextAreaObjectFields()
        {
            if (readme.RichText != null)
            {
                UpdateTextAreaObjectFieldArray();
                DrawTextAreaObjectFields();
                UpdateTextAreaObjectFieldIds();
            }
        }

        void UpdateTextAreaObjectFieldArray()
        {
            string objectTagPattern = "<o=\"[-,a-zA-Z0-9]*\"></o>";
            MatchCollection matches = Regex.Matches(readme.RichText, objectTagPattern, RegexOptions.None);
            TextAreaObjectField[] newTextAreaObjectFields = new TextAreaObjectField[matches.Count];
            for (int i = matches.Count - 1; i >= 0; i--)
            {
                Match match = matches[i];
                
                if (match.Success)
                {
                    string idValue = match.Value.Replace("<o=\"", "").Replace("\"></o>", "");
                    int objectId = 0;
                    bool parseSuccess = int.TryParse(idValue, out objectId);
                        
                    int startIndex = match.Index ;
                    int endIndex = match.Index + match.Value.Length;
                    
                    if (endIndex == readme.RichText.Length || readme.RichText[endIndex] != ' ') { readme.RichText = readme.RichText.Insert(endIndex, " "); }
                    if (startIndex == 0 || readme.RichText[startIndex - 1] != ' ') { readme.RichText = readme.RichText.Insert(startIndex, " "); }
                    
                    Rect rect = GetRect(startIndex - 1, endIndex + 1);
                    rect.position += TextAreaRect.position;
                    
                    Rect rectWithCorrectHeigh = GetRect(startIndex - 1, endIndex);
                    rect.height = rectWithCorrectHeigh.height;

                    if (rect.x > 0 && rect.y > 0 && rect.width > 0 && rect.height > 0)
                    {
                        TextAreaObjectField matchedField = TextAreaObjectFields.FirstOrDefault(item => item.ObjectId == objectId);
                        if (matchedField != null && !matchedField.IdInSync)
                        {
                            matchedField.UpdateId();
                            objectId = matchedField.ObjectId;

                            int idStartIndex = match.Index + 4;
                            readme.RichText = readme.RichText
                                .Remove(idStartIndex, idValue.Length)
                                .Insert(idStartIndex, GetFixedLengthId(objectId.ToString()));
                            
                            ForceTextAreaRefresh();
                        }
                        
                        TextAreaObjectField newField = new TextAreaObjectField(rect, objectId, startIndex, endIndex - startIndex);
                        
                        newTextAreaObjectFields[i] = newField;
                    }
                    else
                    {
                        return; //Abort everything. Position is incorrect!
                    }
                }
            }

            if (!TextAreaObjectFields.SequenceEqual(newTextAreaObjectFields))
            {
                TextAreaObjectFields = newTextAreaObjectFields;
            }
        }

        void DrawTextAreaObjectFields()
        {
            if (!editing)
            {
                EditorGUI.BeginDisabledGroup(true);
                int readonlyOffset = 2;
                foreach (TextAreaObjectField textAreaObjectField in TextAreaObjectFields)
                {
                    textAreaObjectField.Draw(TextEditor, readonlyOffset);
                }
                EditorGUI.EndDisabledGroup();
            }
            else if (!sourceOn)
            {
                foreach (TextAreaObjectField textAreaObjectField in TextAreaObjectFields)
                {
                    textAreaObjectField.Draw(TextEditor);
                }
            }
        }

        void UpdateTextAreaObjectFieldIds()
        {
            StringBuilder newRichText = new StringBuilder(readme.RichText);
            string objectTagPattern = "<o=\"[-,a-zA-Z0-9]*\"></o>";
            int startTagLength = "<o=\"".Length;
            int endTagLength = "\"></o>".Length;
            for (int i = TextAreaObjectFields.Length - 1; i >= 0; i--)
            {
                TextAreaObjectField textAreaObjectField = TextAreaObjectFields[i];

                if (readme.RichText.Length > textAreaObjectField.Index)
                {
                    Match match =
                        Regex.Match(readme.RichText.Substring(Mathf.Max(0, textAreaObjectField.Index - 1)),
                            objectTagPattern, RegexOptions.None);

                    if (match.Success)
                    {
                        int idMaxLength = 7;
                        string textAreaId = GetFixedLengthId(match.Value.Replace("<o=\"", "").Replace("\"></o>", ""));
                        string objectFieldId = GetFixedLengthId(textAreaObjectField.ObjectId);

                        if (textAreaId != objectFieldId)
                        {
                            int idStartIndex = textAreaObjectField.Index + match.Index + startTagLength;
                            newRichText.Remove(idStartIndex - 1, textAreaId.Length);
                            newRichText.Insert(idStartIndex - 1, objectFieldId);
                        }
                    }
                }
            }

            readme.RichText = newRichText.ToString();
        }

        string GetFixedLengthId(int id, int length = 7)
        {
            return GetFixedLengthId(id.ToString(), length);
        }
        
        string GetFixedLengthId(string id, int length = 7)
        {
            string fixedLengthId = id;
            bool isNegative = id[0] == '-';
            string prepend = "";
            
            if (isNegative)
            {
                prepend = "-";
                fixedLengthId = id.Substring(1, id.Length - 1);
            }

            while (fixedLengthId.Length + prepend.Length < length)
            {
                fixedLengthId = "0" + fixedLengthId;
            }

            fixedLengthId = prepend + fixedLengthId;

            return fixedLengthId;
        }
        
        void DragAndDropObjectField() 
        {
            if (editing)
            {
                Event evt = Event.current;

                switch (evt.type)
                {
                    case EventType.DragUpdated:
                    case EventType.DragPerform:
                        if (!TextAreaRect.Contains(evt.mousePosition))
                        {
                            return; // Ignore drag and drop outside of textArea
                        }

                        foreach (TextAreaObjectField textAreaObjectField in TextAreaObjectFields)
                        {
                            if (textAreaObjectField.FieldRect.Contains(evt.mousePosition))
                            {
                                return; // Ignore drag and drop over current Object Fields
                            }
                        }

                        DragAndDrop.visualMode = DragAndDropVisualMode.Link;

                        if (evt.type == EventType.DragPerform && objectsToDrop == null)
                        {
                            DragAndDrop.AcceptDrag();

                            objectsToDrop = DragAndDrop.objectReferences;
                        }

                        break;
                }

                if (objectsToDrop != null)
                {
                    int newCursorIndex = -1;
                    if (TextEditorActive)
                    {
                        int dropIndex = GetNearestPoorTextIndex(MousePositionToIndex);
                        InsertObjectFields(objectsToDrop, dropIndex);
                        objectsToDrop = null;
                        newCursorIndex = GetNearestPoorTextIndex(dropIndex + 1);
                    }

                    ForceTextAreaRefresh(newCursorIndex, newCursorIndex);

                    Undo.RecordObject(readme, "object field added");

                }
            }
        }

        void InsertObjectFields(Object[] objects, int index)
        {
            for (int i = objects.Length - 1; i >= 0; i--)
            {
                Object objectDragged = objects[i];

                AddObjectField(index, objectDragged.GetInstanceID().ToString());
            }
        }

        void AddObjectField(int index = -1, string id = "0000000")
        {
            if (TextEditorActive)
            {
                if (index == -1)
                {
                    index = CursorIndex;
                }

                string objectString = " <o=\"" + GetFixedLengthId(id) + "\"></o> ";
                readme.RichText = readme.RichText.Insert(index, objectString);
                
                int newIndex = GetNearestPoorTextIndex(index + objectString.Length);
                ForceTextAreaRefresh(newIndex, newIndex);
            }
        }

        private Rect GetRect(int startIndex, int endIndex, bool autoAdjust = true)
        {
            Vector2 startPosition = GetGraphicalCursorPos(startIndex) + new Vector2(1, 0);
            Vector2 endPosition = GetGraphicalCursorPos(endIndex) + new Vector2(-1, 0);
            float height = editableRichText.CalcHeight(new GUIContent(""), 100) - 10;
            
            endPosition.y += height;

            Vector2 size = endPosition - startPosition;
            
            Rect rect = new Rect(startPosition, size);

            if (autoAdjust)
            {
                if (rect.width <= 0) { rect = GetRect(startIndex + 1, endIndex, false); }
                if (rect.width <= 0) { rect = GetRect(startIndex, endIndex - 1, false); }
                if (rect.width <= 0) { rect = GetRect(startIndex + 1, endIndex - 1, false); }
            }
            
            return rect;
        }

        private void PrepareForTextAreaChange(string input)
        {
            backspaceAdjust = false;
            
            if (!TagsError(input))
            {
                if (AllTextSelected())
                {
                    SelectIndex = previousSelectIndex;
                    CursorIndex = previousCursorIndex;
                }
                
                if (currentEvent.type == EventType.KeyDown && 
                    new KeyCode[] {KeyCode.Backspace, KeyCode.Delete}.Contains(currentEvent.keyCode) &&
                    CursorIndex == SelectIndex)
                {
                    int direction = currentEvent.keyCode == KeyCode.Backspace ? -1 : 0;
                    int charIndex = textEditor.cursorIndex + direction;
                    string objTag = direction == 0 ? " <o=" : "</o> ";
                    int objTagStart = direction == 0 ? charIndex - 1 : charIndex - 4;
                    int objTagLength = objTag.Length;
                    bool objectField = objTagStart > 0 && 
                                       objTagStart + objTagLength <= input.Length && 
                                       input.Substring(objTagStart, objTagLength) == objTag;
                    
                    if (objectField)
                    {
                        int nextPoorIndex = GetNearestPoorTextIndex(charIndex, -1, direction);
                        bool poorCharFound = (nextPoorIndex - charIndex) * (direction == 1 ? 1 : -1) > 0;
                        
                        if (!poorCharFound) { nextPoorIndex = 0; }
//                        CursorIndex = previousCursorIndex;
                        SelectIndex = nextPoorIndex;
                        EndIndex -= 1;
                        Event.current.Use();
                        
//                        ForceTextAreaRefresh(previousCursorIndex, nextPoorIndex, 10); //Highlight object field instead of deleting it
//                        else if (poorCharFound)
//                        {
//                            finalOutput = input.Remove(nextPoorIndex, 1);
//                            ForceTextAreaRefresh(charIndex, charIndex, 2);
//                        }
//                        else //Probably at the beginning of a line
//                        {
//                            finalOutput = input;
//                            ForceTextAreaRefresh(previousCursorIndex, previousCursorIndex, 2);
//                        }
                    }
                    else if (IsOnTag(charIndex))
                    {
                        CursorIndex += direction == 1 ? 1 : -1;
                        SelectIndex += direction == 1 ? 1 : -1;

                        if (CursorIndex > 0 && CursorIndex <= readme.RichText.Length)
                        {
                            PrepareForTextAreaChange(input);
                            backspaceAdjust = true;
                        }
                    }
                }
            }
        }

        private string GetTextAreaChange(string input, int inputCursorIndex, int inputSelectIndex, string output, int outputCursorIndex, int outputSelectIndex)
        {
            string finalOutput = output;
            bool inputAllSelcted = AllTextSelected(input, inputCursorIndex, inputSelectIndex);
            bool outputAllSelcted = AllTextSelected(output, outputCursorIndex, outputSelectIndex);

            if (textAreaRefreshPending)
            {
                return input;
            }
            
            if (inputAllSelcted)
            {
                return input; //ABORT THIS SHIT
            }

            if (outputAllSelcted)
            {   
                SelectIndex = previousSelectIndex;
                CursorIndex = previousCursorIndex;
                ForceGuiRedraw();
                return input;
//                ForceTextAreaRefresh(previousSelectIndex, previousCursorIndex, 3);
            }

            if (output == "" && inputCursorIndex != 1 && inputSelectIndex != -1)
            {
                Debug.Log("WHAT THE FUCK inputCursorIndex: " + inputCursorIndex + " inputSelectIndex: " + inputSelectIndex);
                ForceTextAreaRefresh(inputCursorIndex, inputSelectIndex, 2); //Highlight object field instead of deleting it
                return input;
            }


//            string objTag = direction == 1 ? " <o=" : "</o> ";
//            int objTagStart = direction == 1 ? charIndex : charIndex - 4;
//            int objTagLength = objTag.Length;
//            bool objectField = objTagStart > 0 && 
//                               objTagStart + objTagLength <= input.Length && 
//                               input.Substring(objTagStart, objTagLength) == objTag;
//            if (objectField)
//            {
//                //do nothing
//            }
//            else if (!TagsError(input))
//            {
//                if (currentEvent.type == EventType.KeyDown && 
//                    new KeyCode[] {KeyCode.Backspace, KeyCode.Delete}.Contains(currentEvent.keyCode) &&
//                    CursorIndex == SelectIndex)
//                {
//                    int direction = currentEvent.keyCode == KeyCode.Backspace ? -1 : 1;
//                    CursorIndex = GetNearestPoorTextIndex(CursorIndex, -1, -direction);
//                    SelectIndex = GetNearestPoorTextIndex(SelectIndex, -1, -direction);
//                    ForceGuiRedraw();

//                    richTextChanged = true;
//                    
//                    int charIndex = textEditor.cursorIndex;
//                    int direction = currentEvent.keyCode == KeyCode.Backspace ? -1 : 1;
//                    string objTag = direction == 1 ? " <o=" : "</o> ";
//                    int objTagStart = direction == 1 ? charIndex : charIndex - 4;
//                    int objTagLength = objTag.Length;
//                    bool objectField = objTagStart > 0 && 
//                                       objTagStart + objTagLength <= input.Length && 
//                                       input.Substring(objTagStart, objTagLength) == objTag;
//                    
//                    if (IsOnTag(charIndex) || objectField)
//                    {
//                        int nextPoorIndex = GetNearestPoorTextIndex(charIndex + direction, -1, direction);
//                        bool poorCharFound = (nextPoorIndex - charIndex) * direction > 0;
//                        
//                        if (objectField)
//                        {
//                            if (!poorCharFound) { nextPoorIndex = 0; }
//                            finalOutput = input;
//                            ForceTextAreaRefresh(previousCursorIndex, nextPoorIndex, 10); //Highlight object field instead of deleting it
//                        }
//                        else if (poorCharFound)
//                        {
//                            finalOutput = input.Remove(nextPoorIndex, 1);
//                            ForceTextAreaRefresh(charIndex, charIndex, 2);
//                        }
//                        else //Probably at the beginning of a line
//                        {
//                            finalOutput = input;
//                            ForceTextAreaRefresh(previousCursorIndex, previousCursorIndex, 2);
//                        }
//                    }
//                }
//            }
            
            return finalOutput;
        }

        private void TextAreaChangeComplete()
        {
//            if (objectField)
//            {
//                //do nothing
//            }
//            else if (!TagsError(input))
//            {
//                if (currentEvent.type == EventType.KeyDown &&
//                    new KeyCode[] {KeyCode.Backspace, KeyCode.Delete}.Contains(currentEvent.keyCode) &&
//                    CursorIndex == SelectIndex)
//                {
//                    int direction = currentEvent.keyCode == KeyCode.Backspace ? -1 : 1;
//                    CursorIndex = GetNearestPoorTextIndex(CursorIndex, -1, -direction);
//                    SelectIndex = GetNearestPoorTextIndex(SelectIndex, -1, -direction);
////                    ForceGuiRedraw();
//                }
//            }

            if (backspaceAdjust)
            {
                int direction = currentEvent.keyCode == KeyCode.Backspace ? -1 : 1;
                CursorIndex = GetNearestPoorTextIndex(CursorIndex, -1, -direction);
                SelectIndex = GetNearestPoorTextIndex(SelectIndex, -1, -direction);
            }
            else if (IsOnTag())
            {
//                FixArrowCursor();
                CursorIndex = GetNearestPoorTextIndex(CursorIndex);
                SelectIndex = GetNearestPoorTextIndex(SelectIndex);
            }
        }

        private void CheckKeyboardShortCuts()
        {
//            Event currentEvent = Event.current;
            
            //Alt + b for bold
            if (currentEvent.type == EventType.KeyDown && currentEvent.alt && currentEvent.keyCode == KeyCode.B)
            {
                ToggleStyle("b");
                currentEvent.Use();
            }
            
            //Alt + i for italic
            if (currentEvent.type == EventType.KeyUp && currentEvent.alt && currentEvent.keyCode == KeyCode.I)
            {
                ToggleStyle("i");
                currentEvent.Use();
            }
            
            //Alt + o for object
            if (currentEvent.type == EventType.KeyUp && currentEvent.alt && currentEvent.keyCode == KeyCode.O)
            {
                AddObjectField();
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
            
            if (TextEditorActive)
            {
                int styleStartIndex = readme.GetPoorIndex(Mathf.Min(CursorIndex, SelectIndex));
                int styleEndIndex = readme.GetPoorIndex(Mathf.Max(CursorIndex, SelectIndex));
                int poorStyleLength = styleEndIndex - styleStartIndex;
    
                readme.ToggleStyle(tag, styleStartIndex, poorStyleLength);
    
                if (TagsError(readme.RichText))
                {
                    readme.LoadLastSave();
                    Debug.LogWarning("You can't do that!");
                }

                UpdateStyleState();
                
                ForceTextAreaRefresh(
                    GetNearestPoorTextIndex(readme.GetRichIndex(styleStartIndex)), 
                    GetNearestPoorTextIndex(readme.GetRichIndex(styleEndIndex)),
                    4);
            }
        }

        void UpdateStyleState()
        {
            if (TextEditorActive)
            {
                int index = 0;
                int poorCursorIndex = readme.GetPoorIndex(CursorIndex);
                int poorSelectIndex = readme.GetPoorIndex(SelectIndex);

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
        }

        private void FixCursorBug()
        {
            if (fixCursorBug && TextEditorActive && !TagsError(readme.RichText) && !sourceOn)
            {
                selectIndexChanged = previousSelectIndex != SelectIndex;
                cursorIndexChanged = previousCursorIndex != CursorIndex;

                if (selectIndexChanged || cursorIndexChanged || richTextChanged)
                {
                    if (!AllTextSelected())
                    {
                        FixMouseCursor();   
                    }
                    FixArrowCursor();
                    
                    richTextChanged = false;
                }
            }
        }
    
        public void FixMouseCursor()
        {
            bool mouseEvent = new EventType[] { EventType.mouseDown, EventType.mouseDrag, EventType.mouseUp }.Contains(currentEvent.type);
            
            if (mouseEvent && Event.current.clickCount <= 1)
            {
                int rawMousePositionIndex = MousePositionToIndex;
                if (rawMousePositionIndex != -1)
                {
                    int mousePositionIndex = GetNearestPoorTextIndex(rawMousePositionIndex);

                    if (selectIndexChanged)
                    {
                        SelectIndex = mousePositionIndex;
                    }

                    if (cursorIndexChanged)
                    {
                        CursorIndex = mousePositionIndex;
                    }
                }
            }
        }
    
        public void FixArrowCursor()
        {
            bool isKeyboard = new KeyCode[] {KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.RightArrow, KeyCode.LeftArrow }.Contains(Event.current.keyCode);
            bool isDoubleClick = Event.current.clickCount == 2;
            if (isKeyboard || isDoubleClick || richTextChanged || AllTextSelected())
            {
                int direction = isDoubleClick ? 1 : 0;
    
                if (isDoubleClick)
                {
                    int mouseIndex = MousePositionToIndex;
                    SelectIndex = mouseIndex;
                    CursorIndex = mouseIndex;
                    SelectIndex = WordStartIndex;
                    CursorIndex = WordEndIndex;
                }
                
                if (cursorIndexChanged || richTextChanged)
                {
                    CursorIndex = GetNearestPoorTextIndex(CursorIndex, previousCursorIndex, direction);
                }
    
                if (selectIndexChanged || richTextChanged)
                {
                    SelectIndex = GetNearestPoorTextIndex(SelectIndex, previousSelectIndex, direction);
                }
            }
        }

        public void FixCopyBuffer()
        {
            if (TextEditorActive && (!sourceOn && !TagsError(readme.RichText)))
            {
                if (EditorGUIUtility.systemCopyBuffer != previousCopyBuffer && previousCopyBuffer != null)
                {
                    List<string> tagPatterns = new List<string>
                    {
                        "<b>",
                        "<i>",
                    };

                    foreach (string tagPattern in tagPatterns)
                    {
                        int textStart = StartIndex - tagPattern.Length;
                        int textLength = tagPattern.Length;

                        if (textStart >= 0 && readme.RichText.Substring(textStart, textLength) == tagPattern)
                        {
                            EditorGUIUtility.systemCopyBuffer = readme.RichText.Substring(textStart, EndIndex - textStart);
                            break;
                        }
                    }
                }
                previousCopyBuffer = EditorGUIUtility.systemCopyBuffer;
            }
        }

        public void ForceCopyBufferToPoorText()
        {
            string newCopyBuffer = EditorGUIUtility.systemCopyBuffer;
            if (TextEditorActive && (!sourceOn && !TagsError(readme.RichText)))
            {
                newCopyBuffer = Readme.MakePoorText(newCopyBuffer);
            }

            EditorGUIUtility.systemCopyBuffer = newCopyBuffer;
            previousCopyBuffer = newCopyBuffer;
        }
    
        public int GetNearestPoorTextIndex(int index, int previousIndex = -1, int direction = 0)
        {
            int nearestPoorTextIndex = index;
            
            if (IsOnTag(index) && index <= readme.RichText.Length)
            {
                int tmpSelectIndex = SelectIndex;
                int tmpCursorIndex = CursorIndex;
                
                int attempts = readme.richTextTagMap.Count * 2;
    
                if (direction == 0)
                {
                    direction = index - previousIndex;
                }
    
                for (int i = 0; i < attempts && IsOnTag(index); i++)
                {
                    if (index == 0)
                    {
                        direction = 1;
                    }
                    
                    if (index == TextEditor.text.Length)
                    {
                        break; //end of text always not rich text.
                    }
    
                    previousIndex = index;
                    if (direction >= 0)
                    {
                        TextEditor.MoveRight();
                        index++;
                    }
                    else
                    {
                        TextEditor.MoveLeft();
                        index--;
                    }
                }
    
                nearestPoorTextIndex = index;
                
                textEditor.selectIndex = tmpSelectIndex;
                textEditor.cursorIndex = tmpCursorIndex;
            }
    
            return nearestPoorTextIndex;
        }
    
        public bool TagsError(string richText)
        {
            bool tagsError = true;
//            bool hasTags = readme.richTextTagMap.Find(isTag => isTag);
            bool hasTags = readme.RichText.Contains("<b>") || readme.RichText.Contains("<\\b>") ||
                           readme.RichText.Contains("<i>") || readme.RichText.Contains("<\\i>") ||
                           readme.RichText.Contains("<size>") || readme.RichText.Contains("<\\size>") ||
                           readme.RichText.Contains("<color") || readme.RichText.Contains("<\\color>");
    
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
    
            if (readme != null &&  readme.richTextTagMap != null && readme.richTextTagMap.Count > index)
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
                int tmpCursorIndex = CursorIndex;
                int tmpSelectIndex = SelectIndex;
    
                Vector2 goalPosition = Event.current.mousePosition - TextAreaRect.position;
    
                float cursorYOffset = activeTextAreaStyle.lineHeight;
                
                textEditor.cursorIndex = 0;
                textEditor.selectIndex = 0;
                int maxAttempts = 1000;
                MoveCursorToNextPoorChar();
                Vector2 currentGraphicalPosition = GetGraphicalCursorPos();
                int attempts = 0;
                for (int currentIndex = CursorIndex; index == -1; currentIndex = CursorIndex)
                {
                    attempts++;
                    if (attempts > maxAttempts)
                    {
                        Debug.LogWarning("ReadmeEditor took too long to find mouse position and is giving up!");
                        break;
                    }
                    
                    //TODO: Check for end of word wrapped line.
                    bool isEndOfLine = readme.RichText.Length > currentIndex ? readme.RichText[currentIndex] == '\n' : true;
    
                    if (currentGraphicalPosition.y < goalPosition.y - cursorYOffset)
                    {
                        TextEditor.MoveRight();
                        MoveCursorToNextPoorChar();
                    }
                    else if (currentGraphicalPosition.x < goalPosition.x && !isEndOfLine)
                    {
                        TextEditor.MoveRight();
                        MoveCursorToNextPoorChar();
    
                        if (GetGraphicalCursorPos().x < currentGraphicalPosition.x)
                        {
                            index = CursorIndex;
                        }
                    }
                    else
                    {
                        index = CursorIndex;
                    }
    
                    if (CursorIndex == readme.RichText.Length)
                    {
                        index = CursorIndex;
                    }
                    
                    currentGraphicalPosition = GetGraphicalCursorPos();
                }
                
                TextEditor.cursorIndex = tmpCursorIndex;
                TextEditor.selectIndex = tmpSelectIndex;
                
                return index;
            }
        }
    
        public void MoveCursorToNextPoorChar()
        {
            int previousCursorIndex = -1;
            for (int i = CursorIndex; i < readme.RichText.Length; i++)
            {
                if (!readme.richTextTagMap[CursorIndex])
                {
                    break;
                }
                
                TextEditor.MoveRight();
                if (CursorIndex == previousCursorIndex)
                {
                    CursorIndex += 1;
                    SelectIndex += 1;
                }
                previousCursorIndex = CursorIndex;
            }
        }
    
        public int WordStartIndex
        {
            get
            {
                int tmpCursorIndex = CursorIndex;
                int tmpSelectIndex = SelectIndex;
    
                TextEditor.MoveWordLeft();
                int wordStartIndex = SelectIndex;
                
                TextEditor.cursorIndex = tmpCursorIndex;
                TextEditor.selectIndex = tmpSelectIndex;
    
                return wordStartIndex;
            }
        }
    
        public int WordEndIndex
        {
            get
            {
                int tmpCursorIndex = CursorIndex;
                int tmpSelectIndex = SelectIndex;
    
                TextEditor.MoveWordRight();
                int wordStartIndex = SelectIndex;
                
                TextEditor.cursorIndex = tmpCursorIndex;
                TextEditor.selectIndex = tmpSelectIndex;
    
                return wordStartIndex;
            }
        }

        public Rect TextAreaRect
        {
            get { return textAreaRect; }
            set
            {
                if (value.x != 0 || value.y != 0)
                {
                    if (textAreaRect.width != value.width)
                    {
                        ForceTextAreaRefresh(-1, -1, 10);
                    }
                    
                    textAreaRect = value;
                }
            }
        }

        public Vector2 GetGraphicalCursorPos(int cursorIndex = -1)
        {
            if (!TextEditorActive)
            {
                return Vector2.zero;
            }
            
            int tmpCursorIndex = -1;
            if (cursorIndex != -1)
            {
                tmpCursorIndex = CursorIndex;
                TextEditor.cursorIndex = cursorIndex;
            }
            
            Rect position = TextEditor.position;
            GUIContent content = TextEditor.content;
            int cursorPos = CursorIndex;
            
            if (tmpCursorIndex != -1)
            {
                TextEditor.cursorIndex = tmpCursorIndex;
            }
            
            return editableRichText.GetCursorPixelPosition(new Rect(0, 0, position.width, position.height), content, cursorPos);
        }

        private bool AllTextSelected(string text = "", int cursorIndex = -1, int selectIndex = -1)
        {
            if (String.IsNullOrEmpty(text))
            {
                text = readme.RichText;
            }

            int startIndex = -1;
            int endIndex = -1;

            bool defaultIndex = cursorIndex == -1 || selectIndex == -1;
            
            startIndex = defaultIndex ? StartIndex : Mathf.Min(cursorIndex, selectIndex);
            endIndex = defaultIndex ? EndIndex : Mathf.Max(cursorIndex, selectIndex);
            
            return TextEditorActive && (startIndex == 0 && endIndex == text.Length);
        }

        private int StartIndex
        {
            get { return Math.Min(CursorIndex, SelectIndex); }
            set
            {
                if (CursorIndex < SelectIndex)
                {
                    CursorIndex = value;
                }
                else
                {
                    SelectIndex = value;
                }
            }
        }

        private int EndIndex
        {
            get { return Math.Max(CursorIndex, SelectIndex); }
            set
            {
                if (CursorIndex > SelectIndex)
                {
                    CursorIndex = value;
                }
                else
                {
                    SelectIndex = value;
                }
            }
        }

        private int CursorIndex
        {
            get { return textEditor != null ? TextEditor.cursorIndex : 0; }
            set
            {
                if (verbose && textEditor != null) { Debug.Log("Cursor index changed: " + TextEditor.cursorIndex + " -> " + value); }
                if (textEditor != null)
                {
                    previousCursorIndex = currentCursorIndex;
                    currentCursorIndex = value;
                    TextEditor.cursorIndex = value;
                };
            }
        }

        private int SelectIndex
        {
            get { return textEditor != null ? TextEditor.selectIndex : 0; }
            set
            {
                if (verbose && textEditor != null) { Debug.Log("Select index changed: " + TextEditor.cursorIndex + " -> " + value); }

                if (textEditor != null)
                {
                    previousSelectIndex = currentSelectIndex;
                    currentSelectIndex = value;
                    TextEditor.selectIndex = value;
                };
            }
        }

        public TextEditor TextEditor
        {
            get { return textEditor; }
            set
            {
                textEditor = value;
            }
        }

        public bool TextEditorActive
        {
            get { return textEditor != null && textEditor.text == readme.RichText; }
        }

        public TextAreaObjectField[] TextAreaObjectFields
        {
            get { return readme.TextAreaObjectFields; }
            set { readme.TextAreaObjectFields = value; }
        }
    }
}

#endif