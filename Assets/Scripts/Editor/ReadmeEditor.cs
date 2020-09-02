#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using Application = UnityEngine.Application;
using Object = UnityEngine.Object;
using TheArtOfDev.HtmlRenderer.PdfSharp;
using PdfSharp;
using PdfSharp.Pdf;

namespace TP.Readme 
{
    [CustomEditor(typeof(Readme)), ExecuteInEditMode]
    public class ReadmeEditor : Editor
    {
        private Readme readme;

        private bool verbose = false;
        private bool liteEditor;
        
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
        private bool editorSelectIndexChanged = false;
        private bool editorCursorIndexChanged = false;
        private int previousCursorIndex = -1;
        private int previousSelectIndex = -1;
        private int currentCursorIndex = -1;
        private int currentSelectIndex = -1;
        private Rect textAreaRect;
        private bool richTextChanged = false;
        private string previousFocusedWindow = "";
        private bool windowFocusModified = false;
        private bool mouseCaptured = false;
        private bool allowSelectAll = false;
        
        private static bool showDebugButtons = false;
        private static bool showAdvancedOptions = false;
        private static bool showCursorPosition = false;
        private static bool showObjectIdPairs = false;
        private static bool showDebugInfo = false;
        
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
        private Vector2 objectDropPosition;
        private string objectIdPairListString = "";
        
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
        
        private string GetSettingsPath()
        {
            MonoScript monoScript = MonoScript.FromScriptableObject(this);
            string path = Path.GetDirectoryName(AssetDatabase.GetAssetPath(monoScript));
            path = Path.Combine(path, "..");
            path = Path.Combine(path, "Settings");
            return path;
        }

        public override void OnInspectorGUI()
        {
            currentEvent = new Event(Event.current);
            bool textChanged = false;

            Readme readmeTarget = (Readme) target;
            if (readmeTarget != null)
            {
                readme = readmeTarget;
            }
            
            readme.Initialize();
            readme.ConnectManager();
            readme.UpdateSettings(GetSettingsPath());
            
            liteEditor = readme.ActiveSettings.lite;
            
            Object selectedObject = Selection.activeObject;
            if (selectedObject != null)
            {
                if (readme.useTackIcon && !Readme.neverUseTackIcon)
                {
                    Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>( "Assets/Packages/TP/Readme/Textures/readme_icon_256_256.png");
                    IconManager.SetIcon(selectedObject as GameObject, icon);
                    readme.iconBeingUsed = true;
                }
                else if (readme.iconBeingUsed)
                {
                    IconManager.RemoveIcon(selectedObject as GameObject);
                    readme.iconBeingUsed = false;
                }
            }

            bool empty = readme.Text == "";

            UpdateGuiStyles();

            float textAreaWidth = EditorGUIUtility.currentViewWidth - 19;
            if (TextAreaRect.width > 0)
            {
                textAreaWidth = TextAreaRect.width;
            }
            float textAreaHeight = editableRichText.CalcHeight(new GUIContent(RichText), textAreaWidth);
            float smallButtonWidth = EditorGUIUtility.singleLineHeight * 2;

            UpdateTextEditor();
            UpdateTextAreaObjectFields();
            
            CheckKeyboardShortCuts();
            
            EditorGUILayout.Space();

            if (!editing)
            {
                if (empty)
                {
                    if (readme.readonlyMode && readme.ActiveSettings.redistributable == true)
                    {
                        string message = "You are using the readonly version of Readme. If you'd like to create and " +
                                         "edit readme files you can purchase a copy of Readme from the Unity Asset" +
                                         "Store.";
                        string website = "https://assetstore.unity.com/packages/slug/152336";
                        EditorGUILayout.HelpBox(message, MessageType.Warning);
                        
                        EditorGUILayout.SelectableLabel(website, GUILayout.Height(textAreaHeight + 4));
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Click edit to add your readme!", MessageType.Info);
                    }
                }
                else
                {
                    String displayText = !TagsError(RichText) ? RichText : readme.Text;
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
                    string newRichText = EditorGUILayout.TextArea(RichText, editableText, new[] {GUILayout.Height(textAreaHeight), GUILayout.Width(textAreaWidth)});
                    if (newRichText != RichText)
                    {
                        textChanged = true;
                        SetTargetDirty();
                    }
                    RichText = newRichText;
                    TextAreaRect = GUILayoutUtility.GetLastRect();
                }
                else
                {
                    readmeEditorActiveTextAreaName = readmeEditorTextAreaStyleName;
                    GUI.SetNextControlName(readmeEditorTextAreaStyleName);

                    PrepareForTextAreaChange(RichText);
                    string newRichText = EditorGUILayout.TextArea(RichText, editableRichText, GUILayout.Height(textAreaHeight));
                    if (newRichText != RichText)
                    {
                        textChanged = true;
                        SetTargetDirty();
                    }
                    RichText = GetTextAreaChange(RichText, newRichText);
                    TextAreaChangeComplete();
                    
                    TextAreaRect = GUILayoutUtility.GetLastRect();
                    activeTextAreaStyle = editableRichText;
                }

                if (TagsError(RichText))
                {
                    EditorGUILayout.HelpBox("Rich text error detected. Check for mismatched tags.", MessageType.Warning);
                }

                if (selectIndexChanged || cursorIndexChanged)
                {
                    UpdateStyleState();
                }
            }
            
            FixCursorBug();
            
            EditorGUILayout.Space();
    
            if (!editing)
            {
                if (!readme.readonlyMode || Readme.disableAllReadonlyMode)
                {
                    if (GUILayout.Button("Edit"))
                    {
                        editing = true;
                    }
    
                    if (IsPrefab(readmeTarget.gameObject))
                    {
                        if (GUILayout.Button("Export To PDF"))
                        {
                            string currentPath = AssetDatabase.GetAssetPath(readmeTarget);
                            string pdfSavePath = EditorUtility.SaveFilePanel(
                                "Save Readme",
                                Path.GetDirectoryName(currentPath),
                                readme.gameObject.name + ".pdf",
                                "pdf");
    
                            if (pdfSavePath != "")
                            {
                                PdfDocument pdf = PdfGenerator.GeneratePdf(readme.HtmlText, PageSize.A4);
                                pdf.Save(pdfSavePath);
                                AssetDatabase.Refresh();
                            }
                        }
                    }
                }
            }
            else
            {
                if (GUILayout.Button("Done"))
                {
                    editing = false;
                }
                
                GUILayout.BeginHorizontal();

                SetFontColor(EditorGUILayout.ColorField(readme.fontColor, GUILayout.Width(smallButtonWidth)));
                
                SetFontStyle(EditorGUILayout.ObjectField(readme.font, typeof(Font), true) as Font);
                
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
                int fontSize = int.Parse(options[selected]);
                SetFontSize(fontSize);
                
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
                    if (!sourceOn && liteEditor)
                    {
                        ShowLiteVersionDialog();
                    }
                    else
                    {
                        sourceOn = !sourceOn;                        
                    }
                }
                
                GUILayout.EndHorizontal();
                
                if (sourceOn)
                {
                    EditorGUILayout.HelpBox("Source mode enabled! Supported tags:\n" + 
                                            " <b></b>\n" +
                                            " <i></i>\n" + 
                                            " <color=\"#00ffff\"></color>\n" + 
                                            " <size=\"20\"></size>\n" + 
                                            " <o=\"0000001\"></o>", 
                        MessageType.Info);
                }
            }

            if (editing || showAdvancedOptions)
            {
                showAdvancedOptions = EditorGUILayout.Foldout(showAdvancedOptions, "Advanced");
            }

            if (showAdvancedOptions)
            {
                EditorGUI.indentLevel++;
                GUI.enabled = false;
                SerializedProperty prop = serializedObject.FindProperty("m_Script");
                EditorGUILayout.PropertyField(prop, true, new GUILayoutOption[0]);
                GUI.enabled = true;
                
                GUIContent fixCursorBugTooltip = new GUIContent(
                    "Cursor Correction",
                    "Override Unity text box cursor placement.");
                fixCursorBug = EditorGUILayout.Toggle(fixCursorBugTooltip, fixCursorBug);
                verbose = EditorGUILayout.Toggle("Verbose", verbose);
                readme.useTackIcon = EditorGUILayout.Toggle("Use Tack Icon", readme.useTackIcon);
                Readme.neverUseTackIcon = EditorGUILayout.Toggle("Never Use Tack Icon", Readme.neverUseTackIcon);
                readme.readonlyMode = EditorGUILayout.Toggle("Readonly Mode", readme.readonlyMode);
                GUIContent disableAllReadonlyModeTooltip = new GUIContent(
                    "Disable All Readonly Mode",
                    "Global setting to enable editing without changing each readonly readme.");
                Readme.disableAllReadonlyMode = EditorGUILayout.Toggle(disableAllReadonlyModeTooltip, Readme.disableAllReadonlyMode);
                
                showCursorPosition = EditorGUILayout.Foldout(showCursorPosition, "Cursor Position");
                if (showCursorPosition)
                {
                    EditorGUI.indentLevel++;
                    string richTextWithCursor = RichText;
                    if (TextEditorActive && SelectIndex <= RichText.Length)
                    {
                        richTextWithCursor = richTextWithCursor.Insert(Mathf.Max(SelectIndex, CursorIndex), "|");
                        if (SelectIndex != CursorIndex)
                        {
                            richTextWithCursor = richTextWithCursor.Insert(Mathf.Min(SelectIndex, CursorIndex), "|");
                        }
                    }

                    richTextWithCursor = richTextWithCursor.Replace("\n", " \\n\n");
                    float adjustedTextAreaHeight =
                        editableText.CalcHeight(new GUIContent(richTextWithCursor), textAreaWidth - 50);
                    EditorGUILayout.SelectableLabel(richTextWithCursor, editableText,
                        GUILayout.Height(adjustedTextAreaHeight));
                    EditorGUI.indentLevel--;
                }
                
                showObjectIdPairs = EditorGUILayout.Foldout(showObjectIdPairs, "Master Object Field Dictionary");
                if (showObjectIdPairs)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("Object Id Pairs");
                    float objectDictHeight = editableText.CalcHeight(new GUIContent(objectIdPairListString), textAreaWidth - 50);
                    EditorGUILayout.SelectableLabel(objectIdPairListString, editableText, GUILayout.Height(objectDictHeight));
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Refresh Pairs", GUILayout.Width(smallButtonWidth * 4)) || objectIdPairListString == "")
                    {
                        objectIdPairListString = ReadmeManager.GetObjectIdPairListString();
                        Repaint();
                    }
                    if (GUILayout.Button("Clear Pairs", GUILayout.Width(smallButtonWidth * 4)))
                    {
                        ReadmeManager.Clear();
                        Repaint();
                    }
                    GUILayout.EndHorizontal();
                    EditorGUI.indentLevel--;
                }
                
                showDebugInfo = EditorGUILayout.Foldout(showDebugInfo, "Debug Info");
                if (showDebugInfo)
                {
                    EditorGUI.indentLevel++;
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
                        "TagsError: " + TagsError(RichText) + "\n" +
                        "Style Map Info: " + "\n" +
                        "\t<b> tags:" + (readme.StyleMaps.ContainsKey("b") ? readme.StyleMaps["b"].FindAll(isStyle => isStyle).Count.ToString() : "0") + "\n" + 
                        "\t<i> tags:" + (readme.StyleMaps.ContainsKey("i") ? readme.StyleMaps["i"].FindAll(isStyle => isStyle).Count.ToString() : "0") + "\n" + 
                        ""
                        , MessageType.Info);
    
                    MessageType messageType = TextEditor != null ? MessageType.Info : MessageType.Warning;
                    EditorGUILayout.HelpBox("Text Editor Found: " + (TextEditor != null ? "Yes" : "No"), messageType);
                    
                    EditorGUILayout.HelpBox( 
                        "Toggle Bold: alt+b\n" +
                        "Toggle Italic: alt+i\n" +
                        "Add Object: alt+o\n" +
                        "Show Advanced Options: alt+a\n"
                        , MessageType.Info);
                    
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
    
                    GUILayout.BeginHorizontal();
    
                    if (GUILayout.Button("New Settings File", GUILayout.Width(smallButtonWidth * 4)))
                    {
                        ReadmeSettings newSettings = new ReadmeSettings(GetSettingsPath());
                        newSettings.SaveSettings();
                        Repaint();
                    }
                    
                    if (GUILayout.Button("Reload Settings", GUILayout.Width(smallButtonWidth * 4)))
                    {
                        readme.UpdateSettings(GetSettingsPath(), true, verbose);
                        Repaint();
                    }
                    
                    GUILayout.EndHorizontal();
    
                    showDebugButtons = EditorGUILayout.Foldout(showDebugButtons, "Debug Buttons");
                    if (showDebugButtons)
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

                EditorGUI.indentLevel--;
            }
            
            UpdateTextAreaObjectFields();
            DragAndDropObjectField();
            
            UpdateFocus();
            
            FixCopyBuffer();

            if (textChanged)
            {
                SetTargetDirty();
            }
        }

        private TextEditor GetTextEditor()
        {
            return typeof(EditorGUI)
                .GetField("activeEditor", BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null) as TextEditor;
        }

        private void UpdateTextEditor()
        {
            if (readme.Text.Length > 0 || editing)
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
                        if (verbose)
                        {
                            Debug.Log("README: Text Editor assigned!");
                        }

                        if (TextEditorActive)
                        {
                            ForceTextAreaRefresh();
                        }
                    }
                }
                else if (TextEditor == null)
                {
                    if (verbose)
                    {
                        Debug.Log("README: Text Editor not found!");
                    }

                    ForceTextAreaRefresh();
                }
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

        public void SetTargetDirty()
        {
            if (!Application.isPlaying)
            {
                Undo.RegisterCompleteObjectUndo(readme.gameObject, "Readme edited");
                
                if (IsPrefab(readme.gameObject))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(readme.gameObject);
                }
            }
        }

        void ForceGuiRedraw()
        {
            UpdateTextAreaObjectFields();
        }

        void ForceTextAreaRefresh(int selectIndex = -1, int cursorIndex = -1, int delay = 3)
        {
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
            }

            if (windowFocusModified && textEditor != null)
            {
                windowFocusModified = false;
                FocusEditorWindow(previousFocusedWindow);
            }
        }

        void UpdateTextAreaObjectFields()
        {
            if (RichText != null)
            {
                UpdateTextAreaObjectFieldArray();
                DrawTextAreaObjectFields();
                UpdateTextAreaObjectFieldIds();
            }
        }

        void UpdateTextAreaObjectFieldArray()
        {
            string objectTagPattern = "<o=\"[-,a-zA-Z0-9]*\"></o>";
            MatchCollection matches = Regex.Matches(RichText, objectTagPattern, RegexOptions.None);
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
                    
                    if (endIndex == RichText.Length || RichText[endIndex] != ' ') { RichText = RichText.Insert(endIndex, " "); }
                    if (startIndex == 0 || RichText[startIndex - 1] != ' ') { RichText = RichText.Insert(startIndex, " "); }
                    
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
                            RichText = RichText
                                .Remove(idStartIndex, idValue.Length)
                                .Insert(idStartIndex, GetFixedLengthId(objectId.ToString()));
                            
                            ForceTextAreaRefresh();
                        }
                        
                        TextAreaObjectField newField = new TextAreaObjectField(rect, objectId, startIndex, endIndex - startIndex);
                        
                        newTextAreaObjectFields[i] = newField;
                    }
                    else
                    {
                        return; //Abort everything. Position is incorrect! Probably no TextEditor found.
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
            StringBuilder newRichText = new StringBuilder(RichText);
            string objectTagPattern = "<o=\"[-,a-zA-Z0-9]*\"></o>";
            int startTagLength = "<o=\"".Length;
            int endTagLength = "\"></o>".Length;
            int expectedFieldCount = Regex.Matches(RichText, "<o=\"[-,a-zA-Z0-9]*\"></o>", RegexOptions.None).Count;

            if (expectedFieldCount != TextAreaObjectFields.Length)
            {
                return;
            }
            
            for (int i = TextAreaObjectFields.Length - 1; i >= 0; i--)
            {
                TextAreaObjectField textAreaObjectField = TextAreaObjectFields[i];

                if (RichText.Length > textAreaObjectField.Index)
                {
                    Match match =
                        Regex.Match(RichText.Substring(Mathf.Max(0, textAreaObjectField.Index - 1)),
                            objectTagPattern, RegexOptions.None);

                    if (match.Success)
                    {
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

            RichText = newRichText.ToString();
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

        void ShowLiteVersionDialog(string feature = "")
        {
            string title = "Paid Feature Only";
            string message = "This is a paid feature. To use this feature please purchase a copy of Readme from the Unity Asset Store.";
            string ok = "Go to Asset Store";
            string cancel = "Nevermind";
            bool result = EditorUtility.DisplayDialog(title, message, ok, cancel);

            if (result)
            {
                Application.OpenURL("https://assetstore.unity.com/packages/slug/152336");
            }
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
                            objectDropPosition = currentEvent.mousePosition;
                        }

                        break;
                }

                if (objectsToDrop != null)
                {
                    if (!TextEditorActive)
                    {
                        ForceTextAreaRefresh();
                    }
                    else
                    {
                        int dropIndex = GetNearestPoorTextIndex(PositionToIndex(objectDropPosition));
                        InsertObjectFields(objectsToDrop, dropIndex);
                        objectsToDrop = null;
                        objectDropPosition = Vector2.zero;
                        Undo.RecordObject(readme, "object field added");
                    }
                }
            }
        }

        void InsertObjectFields(Object[] objects, int index)
        {
            for (int i = objects.Length - 1; i >= 0; i--)
            {
                Object objectDragged = objects[i];

                AddObjectField(index, ReadmeManager.GetIdFromObject(objectDragged).ToString());
            }
        }

        void AddObjectField(int index = -1, string id = "0000000")
        {
            if (liteEditor)
            {
                ShowLiteVersionDialog();
                return;
            }
            
            if (TextEditorActive)
            {
                if (index == -1)
                {
                    index = CursorIndex;
                }

                string objectString = " <o=\"" + GetFixedLengthId(id) + "\"></o> ";
                RichText = RichText.Insert(index, objectString);
                
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
            if (!TagsError(input))
            {
                if (AllTextSelected())
                {

                    if (currentEvent.control && currentEvent.keyCode == KeyCode.A)
                    {
                        allowSelectAll = true;
                    }
                    
                    if (mouseCaptured && currentEvent.type == EventType.MouseDrag) 
                    {
                        allowSelectAll = true;
                    }
                    
                    if (!allowSelectAll && currentEvent.type != EventType.Layout && currentEvent.type != EventType.Repaint)
                    {
                        SelectIndex = previousSelectIndex;
                        CursorIndex = previousCursorIndex;
                    }
                }
                else
                {
                    allowSelectAll = false;
                }

                if (SelectIndex == 0 && CursorIndex == 0 && (currentCursorIndex != CursorIndex || currentSelectIndex != SelectIndex))
                {
                    if (!currentEvent.isMouse && !currentEvent.isKey)
                    {
                        SelectIndex = currentSelectIndex;
                        CursorIndex = currentCursorIndex;
                    }
                }
                
                if (currentEvent.type == EventType.KeyDown && 
                    new KeyCode[] {KeyCode.Backspace, KeyCode.Delete}.Contains(currentEvent.keyCode) &&
                    CursorIndex == SelectIndex)
                {
                    int direction = currentEvent.keyCode == KeyCode.Backspace ? -1 : 0;
                    int charIndex = textEditor.cursorIndex + direction;
                    string objTag = direction == 0 ? " <o=" : "</o> ";
                    int objTagStart = direction == 0 ? charIndex : charIndex - 4;
                    int objTagLength = objTag.Length;
                    bool objectField = objTagStart > 0 && 
                                       objTagStart + objTagLength <= input.Length && 
                                       input.Substring(objTagStart, objTagLength) == objTag;
                    
                    if (objectField)
                    {
                        int nextPoorIndex = GetNearestPoorTextIndex(charIndex + (direction == 0 ? 1 : 0), direction);
                        bool poorCharFound = (nextPoorIndex - charIndex) * (direction == 0 ? 1 : -1) > 0;
                        
                        if (!poorCharFound) { nextPoorIndex = 0; }
                        SelectIndex = nextPoorIndex;
                        EndIndex -= 1;
                        Event.current.Use();
                    }
                    else 
                    {
                        if (charIndex < 0 || CursorIndex > RichText.Length)
                        {
                            int newIndex = GetNearestPoorTextIndex(charIndex - direction);
                            CursorIndex = newIndex;
                            SelectIndex = newIndex;
                            Event.current.Use();
                        }
                        else if (IsOnTag(charIndex))
                        {
                            CursorIndex += direction == 1 ? 1 : -1;
                            SelectIndex += direction == 1 ? 1 : -1;
                            
                            PrepareForTextAreaChange(input);
                        }
                    }
                }
            }
        }

        private string GetTextAreaChange(string input, string output)
        {
            if (textAreaRefreshPending)
            {
                return input;
            }

            if (input != output)
            {
                richTextChanged = true;
            }

            return output;
        }

        private void TextAreaChangeComplete()
        {
            if (richTextChanged)
            {
                int direction = currentEvent.keyCode == KeyCode.Backspace ? -1 : 0;
                CursorIndex = GetNearestPoorTextIndex(CursorIndex, -direction);
                SelectIndex = GetNearestPoorTextIndex(SelectIndex, -direction);
            }
        }

        private void CheckKeyboardShortCuts()
        {
            //Alt + a for toggle advanced mode
            if (currentEvent.type == EventType.KeyDown && currentEvent.alt && currentEvent.keyCode == KeyCode.A)
            {
                showAdvancedOptions = !showAdvancedOptions; 
                Event.current.Use();
                Repaint();
            }
            
            if (editing)
            {
            //Alt + b for bold
            if (currentEvent.type == EventType.KeyDown && currentEvent.alt && currentEvent.keyCode == KeyCode.B)
            {
                ToggleStyle("b");
                    Event.current.Use();
            }
            
            //Alt + i for italic
            if (currentEvent.type == EventType.KeyDown && currentEvent.alt && currentEvent.keyCode == KeyCode.I)
            {
                ToggleStyle("i");
                    Event.current.Use();
            }
            
            //Alt + o for object
            if (currentEvent.type == EventType.KeyDown && currentEvent.alt && currentEvent.keyCode == KeyCode.O)
            {
                AddObjectField();
                    Event.current.Use();
                }
            }
            
            //Ctrl + v for paste
            if (currentEvent.type == EventType.KeyDown && currentEvent.control && currentEvent.keyCode == KeyCode.V)
            {

            }
        }

        private void SetFontColor(Color color)
        {
            if (color == readme.fontColor)
            {
                return;
            }
            
            if (liteEditor)
            {
                ShowLiteVersionDialog();
                return;
            }

            readme.fontColor = color;
        }

        private void SetFontStyle(Font font)
        {
            if (font == readme.font)
            {
                return;
            }
            
            if (liteEditor)
            {
                ShowLiteVersionDialog();
                return;
            }

            readme.font = font;
        }

        private void SetFontSize(int size)
        {
            if (readme.fontSize != 0)
            {
                if (size == readme.fontSize)
                {
                    return;
                }

                if (liteEditor)
                {
                    ShowLiteVersionDialog();
                    return;
                }
            }

            readme.fontSize = size;
        }
    
        private void ToggleStyle(string tag)
        {
            if (liteEditor)
            {
                ShowLiteVersionDialog();
                return;
            }
            
            if (TagsError(RichText))
            {
                Debug.LogWarning("Please fix any mismatched tags first!");
                return;
            }
            
            if (TextEditorActive)
            {
                int styleStartIndex = readme.GetPoorIndex(StartIndex);
                int styleEndIndex = readme.GetPoorIndex(EndIndex);
                int poorStyleLength = styleEndIndex - styleStartIndex;
    
                readme.ToggleStyle(tag, styleStartIndex, poorStyleLength);
                TextEditor.text = RichText;
    
                if (TagsError(RichText))
                {
                    readme.LoadLastSave();
                    Debug.LogWarning("You can't do that!");
                }

                UpdateStyleState();

                int newCursorIndex = GetNearestPoorTextIndex(readme.GetRichIndex(styleStartIndex));
                int newSelectIndex = GetNearestPoorTextIndex(readme.GetRichIndex(styleEndIndex));
                
                ForceTextAreaRefresh(newCursorIndex, newSelectIndex);
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
            if (fixCursorBug && TextEditorActive && !TagsError(RichText) && !sourceOn)
            {
                editorSelectIndexChanged = currentSelectIndex != SelectIndex;
                editorCursorIndexChanged = currentCursorIndex != CursorIndex;
                
                if (!AllTextSelected())
                {
                    FixMouseCursor();   
                }
                FixArrowCursor();
            }
        }
    
        public void FixMouseCursor()
        {
            bool mouseEvent = new EventType[] { EventType.MouseDown, EventType.MouseDrag, EventType.MouseUp }.Contains(currentEvent.type);
            
            if (currentEvent.type == EventType.MouseDown && TextAreaRect.Contains(currentEvent.mousePosition))
            {
                mouseCaptured = true;
            }
            
            if (mouseCaptured && mouseEvent && Event.current.clickCount <= 1)
            {
                int rawMousePositionIndex = MousePositionToIndex;
                if (rawMousePositionIndex != -1)
                {
                    int mousePositionIndex = GetNearestPoorTextIndex(rawMousePositionIndex);

                    if (editorSelectIndexChanged)
                    {
                        SelectIndex = mousePositionIndex;
                    }

                    if (editorCursorIndexChanged)
                    {
                        CursorIndex = mousePositionIndex;
                    }
                }
            }

            if (currentEvent.type == EventType.MouseUp)
            {
                mouseCaptured = false;
            }
        }
    
        public void FixArrowCursor()
        {
            bool isKeyboard = new KeyCode[] {KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.RightArrow, KeyCode.LeftArrow }.Contains(Event.current.keyCode);
            bool isDoubleClick = Event.current.clickCount == 2;
            bool mouseInTextArea = TextAreaRect.Contains(currentEvent.mousePosition);
            if (isKeyboard || isDoubleClick || richTextChanged || AllTextSelected())
            {
                int direction = isDoubleClick ? 1 : 0;
                
                if (isDoubleClick && mouseInTextArea)
                {
                    int mouseIndex = MousePositionToIndex;
                    SelectIndex = mouseIndex;
                    CursorIndex = mouseIndex;
                    SelectIndex =  GetNearestPoorTextIndex(WordStartIndex, -1);
                    CursorIndex = GetNearestPoorTextIndex(WordEndIndex, 1);
                }

                if (currentEvent.keyCode == KeyCode.LeftArrow)
                {
                    direction = -1;
                }
                else if (currentEvent.keyCode == KeyCode.RightArrow)
                {
                    direction = 1;
                }
                
                if (editorSelectIndexChanged || editorCursorIndexChanged || richTextChanged)
                {
                    CursorIndex = GetNearestPoorTextIndex(CursorIndex, direction);
                    SelectIndex = GetNearestPoorTextIndex(SelectIndex, direction);
                    cursorIndexChanged = false;
                    selectIndexChanged = false;
                }
            }
                
            richTextChanged = false;
        }

        public void FixCopyBuffer()
        {
            if (TextEditorActive && (!sourceOn && !TagsError(RichText)))
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

                        if (textStart >= 0 && RichText.Substring(textStart, textLength) == tagPattern)
                        {
                            EditorGUIUtility.systemCopyBuffer = RichText.Substring(textStart, EndIndex - textStart);
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
            if (TextEditorActive && (!sourceOn && !TagsError(RichText)))
            {
                newCopyBuffer = Readme.MakePoorText(newCopyBuffer);
            }

            EditorGUIUtility.systemCopyBuffer = newCopyBuffer;
            previousCopyBuffer = newCopyBuffer;
        }
    
        public int GetNearestPoorTextIndex(int index, int direction = 0)
        {
            int nearestPoorTextIndex = index;
            
            if (IsOnTag(index) && index <= RichText.Length)
            {
                int tmpSelectIndex = SelectIndex;
                int tmpCursorIndex = CursorIndex;
                
                int attempts = readme.richTextTagMap.Count * 2;
    
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
            bool hasTags = RichText.Contains("<b>") || RichText.Contains("<\\b>") ||
                           RichText.Contains("<i>") || RichText.Contains("<\\i>") ||
                           RichText.Contains("<size>") || RichText.Contains("<\\size>") ||
                           RichText.Contains("<color") || RichText.Contains("<\\color>");
    
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
    
        public int MousePositionToIndex
        {
            get
            {
                return PositionToIndex(currentEvent.mousePosition);
            }
        }

        public int PositionToIndex(Vector2 position)
        {
            int index = -1;
            int tmpCursorIndex = CursorIndex;
            int tmpSelectIndex = SelectIndex;

            Vector2 goalPosition = position - TextAreaRect.position;

            float cursorYOffset = activeTextAreaStyle.lineHeight;
            
            textEditor.cursorIndex = 0;
            textEditor.selectIndex = 0;
            int maxAttempts = 1000;
            textEditor.cursorIndex = GetNearestPoorTextIndex(textEditor.cursorIndex);
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
                bool isEndOfLine = RichText.Length > currentIndex ? RichText[currentIndex] == '\n' : true;

                if (currentGraphicalPosition.y < goalPosition.y - cursorYOffset)
                {
                    TextEditor.MoveRight();
                    textEditor.cursorIndex = GetNearestPoorTextIndex(textEditor.cursorIndex);
                    textEditor.selectIndex = GetNearestPoorTextIndex(textEditor.cursorIndex);
                }
                else if (currentGraphicalPosition.x < goalPosition.x && !isEndOfLine)
                {
                    TextEditor.MoveRight();
                    textEditor.cursorIndex = GetNearestPoorTextIndex(textEditor.cursorIndex);
                    textEditor.selectIndex = GetNearestPoorTextIndex(textEditor.cursorIndex);

                    if (GetGraphicalCursorPos().x < currentGraphicalPosition.x)
                    {
                        index = CursorIndex;
                    }
                }
                else
                {
                    index = CursorIndex;
                }

                if (CursorIndex == RichText.Length)
                {
                    index = CursorIndex;
                }
                
                currentGraphicalPosition = GetGraphicalCursorPos();
            }
            
            TextEditor.cursorIndex = tmpCursorIndex;
            TextEditor.selectIndex = tmpSelectIndex;
            
            return index;
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
                        ForceTextAreaRefresh();
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
                text = RichText;
            }

            int startIndex = -1;
            int endIndex = -1;

            bool defaultIndex = cursorIndex == -1 || selectIndex == -1;
            
            startIndex = defaultIndex ? StartIndex : Mathf.Min(cursorIndex, selectIndex);
            endIndex = defaultIndex ? EndIndex : Mathf.Max(cursorIndex, selectIndex);
            
            return TextEditorActive && (startIndex == 0 && endIndex == text.Length);
        }
        
        private static bool IsPrefab(GameObject gameObject)
        {
            bool isPrefab = gameObject != null && (gameObject.scene.name == null || gameObject.gameObject != null && gameObject.gameObject.scene.name == null);

            return isPrefab;
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
                if (textEditor != null)
                {
                    if (currentCursorIndex != value)
                    {
                        if (verbose) { Debug.Log("README: Cursor index changed: " + currentCursorIndex + " -> " + value); }

                        cursorIndexChanged = true;
                        previousCursorIndex = currentCursorIndex;
                        currentCursorIndex = value;
                    }

                    TextEditor.cursorIndex = value;
                };
            }
        }

        private int SelectIndex
        {
            get { return textEditor != null ? TextEditor.selectIndex : 0; }
            set
            {
                if (textEditor != null)
                {
                    if (currentSelectIndex != value)
                    {
                        if (verbose) { Debug.Log("README: Select index changed: " + currentSelectIndex + " -> " + value); }

                        selectIndexChanged = true;
                        previousSelectIndex = currentSelectIndex;
                        currentSelectIndex = value;
                    }

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
        
        public string RichText
        {
            get { return readme.RichText; }
            set
            {
                readme.RichText = value;
            }
        }
        

        public bool TextEditorActive
        {
            get { return textEditor != null && textEditor.text == RichText; }
        }

        public TextAreaObjectField[] TextAreaObjectFields
        {
            get { return readme.TextAreaObjectFields; }
            set { readme.TextAreaObjectFields = value; }
        }
    }
}

#endif