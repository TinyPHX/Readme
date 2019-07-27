using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework.Constraints;
using UnityEngine;
using UnityEditor;
using UnityEngine.Collections;
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

        // Text area object fields
        private List<TextAreaObjectField> textAreaObjectFields = new List<TextAreaObjectField> { };

        // Text area focus
        private bool textAreaNeedsFocus = false;
        private int focusDelay = 0;
        private int focusCursorIndex = -1;
        private int focuseSelectIndex = -1;

        // Drag and drop object fields
        private Object[] objectsToDrop;

        public override void OnInspectorGUI()
        {
            if (verbose) {  Debug.Log("README: OnInspectorGUI"); }

            UpdateFocus();

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
            if (TextAreaRect.width > 0)
            {
                textAreaWidth = TextAreaRect.width;
            }
            float textAreaHeight = editableRichText.CalcHeight(new GUIContent(readme.Text), textAreaWidth);
            float smallButtonWidth = EditorGUIUtility.singleLineHeight * 2;

            AssignTextEditor();
            
            EditorGUILayout.Space();

            UpdateTextAreaObjectFields();
            UpdateTextAreaObjectFieldIds();
            
            DrawTextAreaObjectFields();

            if (!editing)
            {
                if (empty)
                {
                    EditorGUILayout.HelpBox("Click edit to add your readme!", MessageType.Info);
                }
                else
                {
                    readmeEditorActiveTextAreaName = readmeEditorTextAreaReadonlyName;
                    GUI.SetNextControlName(readmeEditorTextAreaReadonlyName);
                    EditorGUILayout.SelectableLabel(readme.RichText, selectableRichText, GUILayout.Height(textAreaHeight + 4));
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
                    readmeEditorActiveTextAreaName = readmeEditorTextAreaStyleName;
                    GUI.SetNextControlName(readmeEditorTextAreaStyleName);
                    readme.RichText = EditorGUILayout.TextArea(readme.RichText, editableRichText, GUILayout.Height(textAreaHeight));
                    TextAreaRect = GUILayoutUtility.GetLastRect();
                    
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
                    ForceTextAreaRefresh();
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
                if (GUILayout.Button(new GUIContent("Obj", "Insert Object Field"), objButtonStyle, GUILayout.Width(smallButtonWidth)))
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
                    EditorGUILayout.HelpBox("Source mode enabled! Supported tags:\n <b></b>\n <i></i>\n <color=#00ffff></color>", MessageType.Info);
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
                if (textEditor != null && SelectIndex <= readme.RichText.Length)
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
                    "graphicalCursorPos: " + (textEditor == null ? "" : textEditor.graphicalCursorPos.ToString()) + "\n" +
                    "Calc Cursor Position: " + (Event.current.mousePosition - TextAreaRect.position) + "\n" +
                    "cursorIndex: " + (textEditor == null ? "" : CursorIndex.ToString()) + "\n" +
                    "selectIndex: " + (textEditor == null ? "" : SelectIndex.ToString()) + "\n" +
                    "position: " + (textEditor == null ? "" : textEditor.position.ToString()) + "\n" +
                    "TagsError: " + TagsError(readme.RichText) + "\n" +
                    "Style Map Info: " + "\n" +
                    "\t<b> tags:" + (readme.StyleMaps.ContainsKey("b") ? readme.StyleMaps["b"].FindAll(isStyle => isStyle).Count.ToString() : "0") + "\n" + 
                    "\t<i> tags:" + (readme.StyleMaps.ContainsKey("i") ? readme.StyleMaps["i"].FindAll(isStyle => isStyle).Count.ToString() : "0") + "\n" + 
                    ""
                    , MessageType.Info);

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
                        AssignTextEditor();
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

            CheckBackspaceOrDelete();
            CheckKeyboardShortCuts();
            
            DrawTextAreaObjectFields();
            DragAndDropObjectField();
        }

        private TextEditor GetTextEditor()
        {
            return typeof(EditorGUI)
                .GetField("activeEditor", BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null) as TextEditor;
        }

        private void AssignTextEditor()
        {
            TextEditor activeTextEditor = GetTextEditor();
            
            if (activeTextEditor == null)
            {
                FocusOnInspectorWindow();
            }
            
            if (activeTextEditor != null)
            {
                if (textEditor != activeTextEditor)
                {
                    textEditor = activeTextEditor;
                    if (verbose) {  Debug.Log("README: Text Editor assigned!"); }
                    
                    ForceTextAreaRefresh(0, 0);
                }
            }
            else if (textEditor == null)
            {
                if (verbose) {  Debug.Log("README: Text Editor not found!"); }
                ForceTextAreaRefresh();
            }
        }

        private void FocusOnInspectorWindow()
        {
            if (EditorWindow.focusedWindow.titleContent.text != "Inspector")
            {
                EditorWindow[] allWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();
                EditorWindow inspectorWindow = allWindows.SingleOrDefault(window => window.titleContent.text == "Inspector");
                if (inspectorWindow != default(EditorWindow))
                {
                    inspectorWindow.Focus();
                }
            }
        }

        void ForceGuiRedraw()
        {
            Undo.RecordObject(readme, "Force update!");
            EditorUtility.SetDirty(readme);
            UpdateTextAreaObjectFields();
        }

        void ForceTextAreaRefresh(int selectIndex = -1, int cursorIndex = -1, int delay = 5)
        {
            if (!textAreaNeedsFocus)
            {
                if (verbose) {  Debug.Log("README: ForceTextAreaRefresh, selectIndex: " + selectIndex + " cursorIndex: " + cursorIndex); }
                if (GUI.GetNameOfFocusedControl() != readmeEditorTextAreaReadonlyName)
                {
                    EditorGUI.FocusTextInControl("");
                    GUI.FocusControl("");
                }
                textAreaNeedsFocus = true;
                focusDelay = delay;
                focuseSelectIndex = selectIndex == -1 && textEditor != null ? SelectIndex : selectIndex;
                focusCursorIndex = cursorIndex == -1 && textEditor != null ? CursorIndex : cursorIndex;
                ForceGuiRedraw();
            }
        }

        private void FocusTextArea()
        {
            textAreaNeedsFocus = false;
            EditorGUI.FocusTextInControl(readmeEditorActiveTextAreaName);
            GUI.FocusControl(readmeEditorActiveTextAreaName);
            ForceGuiRedraw();
        }

        private void UpdateFocus()
        {
            if (textAreaNeedsFocus)
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
            }
        }

        void UpdateTextAreaObjectFields()
        {
            if (readme.RichText != null && textEditor != null)
            {
                List<TextAreaObjectField> newTextAreaObjectFields = new List<TextAreaObjectField> { };
                string objectTagPattern = "<o=\"[-,a-zA-Z0-9]*\"></o>";
                foreach (Match match in Regex.Matches(readme.RichText, objectTagPattern, RegexOptions.None))
                {
                    if (match.Success)
                    {
                        string idValue = match.Value.Replace("<o=\"", "").Replace("\"></o>", "");
                        int id = 0;
                        bool parseSuccess = int.TryParse(idValue, out id);
                            
                        int startIndex = match.Index ;
                        int endIndex = match.Index + match.Value.Length;
                        
                        if (endIndex == readme.RichText.Length || readme.RichText[endIndex] != ' ') { readme.RichText = readme.RichText.Insert(endIndex, " "); }
                        if (startIndex == 0 || readme.RichText[startIndex - 1] != ' ') { readme.RichText = readme.RichText.Insert(startIndex, " "); }
                        
                        Rect rect = GetRect(startIndex - 1, endIndex + 1);
                        rect.position += TextAreaRect.position;

                        if (rect.x > 0 && rect.y > 0 && rect.width > 0 && rect.height > 0)
                        {
                            newTextAreaObjectFields.Add(new TextAreaObjectField(rect, id, startIndex, endIndex - startIndex));
                        }
                        else
                        {
                            return; //Abort everything. Position is incorrect!
                        }
                    }
                }

                textAreaObjectFields.Clear();
                textAreaObjectFields = newTextAreaObjectFields;
            }
        }

        void DrawTextAreaObjectFields()
        {
            if (!editing)
            {
                EditorGUI.BeginDisabledGroup(true);
                foreach (TextAreaObjectField textAreaObjectField in textAreaObjectFields)
                {
                    textAreaObjectField.Draw(textEditor);
                }
                EditorGUI.EndDisabledGroup();
            }
            else if (!sourceOn)
            {
                foreach (TextAreaObjectField textAreaObjectField in textAreaObjectFields)
                {
                    textAreaObjectField.Draw(textEditor);
                }
            }
        }

        void UpdateTextAreaObjectFieldIds()
        {
            StringBuilder newRichText = new StringBuilder(readme.RichText);
            string objectTagPattern = " <o=\"[-,a-zA-Z0-9]*\"></o> ";
            int startTagLength = " <o=\"".Length;
            int endTagLength = "\"></o> ".Length;
            foreach (TextAreaObjectField textAreaObjectField in textAreaObjectFields)
            {
                if (readme.RichText.Length > textAreaObjectField.Index)
                {
                    Match match = Regex.Match(readme.RichText.Substring(textAreaObjectField.Index - 1), objectTagPattern,
                        RegexOptions.None);

                    if (match.Success)
                    {
                        int idMaxLength = 7;
                        string textAreaId = match.Value.Replace(" <o=\"", "").Replace("\"></o> ", "");
                        string objectFieldId;

                        if (textAreaObjectField.ObjectId >= 0)
                        {
                            objectFieldId = textAreaObjectField.ObjectId.ToString();
                            while (objectFieldId.Length < idMaxLength)
                            {
                                objectFieldId = "0" + objectFieldId;
                            }
                        }
                        else
                        {
                            objectFieldId = Mathf.Abs(textAreaObjectField.ObjectId).ToString();
                            while (objectFieldId.Length < idMaxLength - 1)
                            {
                                objectFieldId = "0" + objectFieldId;
                            }
                            objectFieldId = "-" + objectFieldId;
                        }

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
        
        void DragAndDropObjectField() 
        { 
            Event evt = Event.current;
     
            switch (evt.type) {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!TextAreaRect.Contains(evt.mousePosition))
                    {
                        return; // Ignore drag and drop outside of textAread
                    }

                    foreach (TextAreaObjectField textAreaObjectField in textAreaObjectFields)
                    {
                        if (textAreaObjectField.FieldRect.Contains(evt.mousePosition))
                        {
                            return; // Ignore drag and drop over current Object Fields
                        }
                    }
             
                    DragAndDrop.visualMode = DragAndDropVisualMode.Link;
         
                    if (evt.type == EventType.DragPerform && objectsToDrop == null) 
                    {
                        DragAndDrop.AcceptDrag ();

                        objectsToDrop = DragAndDrop.objectReferences;
                    }
                    break;
            }

            if (objectsToDrop != null)
            {
                if (textEditor != null)
                {
                    int dropIndex = GetNearestPoorTextIndex(MousePositionToIndex);
                    InsertObjectFields(objectsToDrop, dropIndex);
                    objectsToDrop = null;
                }
                
                ForceTextAreaRefresh();
                
                Undo.RecordObject(readme, "object field added");
                
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
            if (textEditor != null)
            {
                if (index == -1)
                {
                    index = CursorIndex;
                }
                
                readme.RichText = readme.RichText.Insert(index, " <o=\"" + id + "\"></o> ");
                
                UpdateTextAreaObjectFields();
                int newIndex = GetNearestPoorTextIndex(index) + 1;
                ForceTextAreaRefresh(newIndex, newIndex);
            }
        }

        private Rect GetRect(int startIndex, int endIndex, bool autoAdjust = true)
        {
            Vector2 startPosition = GetGraphicalCursorPos(startIndex) + new Vector2(1, 0);
            Vector2 endPosition = GetGraphicalCursorPos(endIndex) + new Vector2(-1, 0);
            
            GUIStyle singleLineTextArea = new GUIStyle(activeTextAreaStyle);
            singleLineTextArea.wordWrap = false;
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

        private void CheckBackspaceOrDelete()
        {
            Event currentEvent = Event.current;

//            Debug.Log("Event.current: " + Event.current);
            
            if (SelectIndex == CursorIndex)
            {
                if (currentEvent.type == EventType.KeyUp)
                {
                    if (currentEvent.keyCode == KeyCode.Backspace)
                    {
                        Debug.Log("Fixing backspace");
                        int newIndex = GetNearestPoorTextIndex(CursorIndex, CursorIndex, -1);
                        CursorIndex = newIndex;
                        SelectIndex = newIndex;

                    }
                    else if (currentEvent.keyCode == KeyCode.Delete)
                    {
                        Debug.Log("Fixing delete");
                        int newIndex = GetNearestPoorTextIndex(CursorIndex, CursorIndex, 1);
                        CursorIndex = newIndex;
                        SelectIndex = newIndex;
                    }
                }
            }
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
                    3);
            }
        }

        void UpdateStyleState()
        {
            if (textEditor != null)
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
            if (fixCursorBug && textEditor != null && GUI.GetNameOfFocusedControl() == readmeEditorTextAreaStyleName && !TagsError(readme.RichText) && !sourceOn)
            {
                selectIndexChanged = previousSelctIndex != SelectIndex;
                cursorIndexChanged = previousCursorIndex != CursorIndex;
    
                if (selectIndexChanged || cursorIndexChanged)
                {
                    FixMouseCursor();
                    FixArrowCursor();
                }
                
                previousSelctIndex = SelectIndex;
                previousCursorIndex = CursorIndex;
            }
        }
    
        public void FixMouseCursor()
        {
            bool isKeyboard = new KeyCode[] {KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.RightArrow, KeyCode.LeftArrow }.Contains(Event.current.keyCode);
            bool typing = Event.current.character != '\0' || Event.current.keyCode != KeyCode.None;
            bool textAreaContainsMouse = TextAreaRect.Contains(Event.current.mousePosition);
            int selectIndex = SelectIndex;
    
            if (!typing && Event.current.clickCount <= 1 && textAreaContainsMouse)
            {
                int mousePositionIndex = GetNearestPoorTextIndex(MousePositionToIndex);
    
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
                    SelectIndex = mouseIndex;
                    CursorIndex = mouseIndex;
                    SelectIndex = WordStartIndex;
                    CursorIndex = WordEndIndex;
                }
                
                if (cursorIndexChanged)
                {
                    CursorIndex = GetNearestPoorTextIndex(CursorIndex, previousCursorIndex, direction);
                }
    
                if (selectIndexChanged)
                {
                    SelectIndex = GetNearestPoorTextIndex(SelectIndex, previousSelctIndex, direction);
                }
            }
        }
    
        public int GetNearestPoorTextIndex(int index, int previousIndex = -1, int direction = 0)
        {
            int nearestPoorTextIndex = index;
            
            if (IsOnTag(index) && index != readme.RichText.Length - 1)
            {
                int selectIndex = SelectIndex;
                int cursorIndex = CursorIndex;
                
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
                
                SelectIndex = selectIndex;
                CursorIndex = cursorIndex;
            }
    
            return nearestPoorTextIndex;
        }
    
        public bool TagsError(string richText)
        {
            bool tagsError = true;
//            bool hasTags = readme.richTextTagMap.Find(isTag => isTag);
            bool hasTags = readme.Text.Contains("<b>") || readme.Text.Contains("<\\b>") ||
                           readme.Text.Contains("<i>") || readme.Text.Contains("<\\i>") ||
                           readme.Text.Contains("<color") || readme.Text.Contains("<\\color>");
    
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
                int tmpCursorIndex = CursorIndex;
                int tmpSelectIndex = SelectIndex;
    
                Vector2 goalPosition = Event.current.mousePosition - TextAreaRect.position;
    
                float cursorYOffset = activeTextAreaStyle.lineHeight;
                
                CursorIndex = 0;
                SelectIndex = 0;
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
                        textEditor.MoveRight();
                        MoveCursorToNextPoorChar();
                    }
                    else if (currentGraphicalPosition.x < goalPosition.x && !isEndOfLine)
                    {
                        textEditor.MoveRight();
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
                
                textEditor.cursorIndex = tmpCursorIndex;
                textEditor.selectIndex = tmpSelectIndex;
                
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
                
                textEditor.MoveRight();
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
    
                textEditor.MoveWordLeft();
                int wordStartIndex = SelectIndex;
                
                textEditor.cursorIndex = tmpCursorIndex;
                textEditor.selectIndex = tmpSelectIndex;
    
                return wordStartIndex;
            }
        }
    
        public int WordEndIndex
        {
            get
            {
                int tmpCursorIndex = CursorIndex;
                int tmpSelectIndex = SelectIndex;
    
                textEditor.MoveWordRight();
                int wordStartIndex = SelectIndex;
                
                textEditor.cursorIndex = tmpCursorIndex;
                textEditor.selectIndex = tmpSelectIndex;
    
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
            if (textEditor == null)
            {
                return Vector2.zero;
            }
            
            int tmpCursorIndex = -1;
            if (cursorIndex != -1)
            {
                tmpCursorIndex = CursorIndex;
                textEditor.cursorIndex = cursorIndex;
            }
            
            Rect position = textEditor.position;
            GUIContent content = textEditor.content;
            int cursorPos = CursorIndex;
            
            if (tmpCursorIndex != -1)
            {
                textEditor.cursorIndex = tmpCursorIndex;
            }
            
            return editableRichText.GetCursorPixelPosition(new Rect(0, 0, position.width, position.height), content, cursorPos);
        }

        private int CursorIndex
        {
            get { return textEditor != null ? textEditor.cursorIndex : 0; }
            set
            {
                if (verbose) { Debug.Log("Cursor index changed: " + textEditor.cursorIndex + " -> " + value); }
                if (textEditor != null) { textEditor.cursorIndex = value; };
            }
        }

        private int SelectIndex
        {
            get { return textEditor != null ? textEditor.selectIndex : 0; }
            set
            {
                if (verbose) { Debug.Log("Select index changed: " + textEditor.cursorIndex + " -> " + value); }
                if (textEditor != null) { textEditor.selectIndex = value; };
            }
        }
    }
}