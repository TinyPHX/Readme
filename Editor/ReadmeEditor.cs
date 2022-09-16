#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using Application = UnityEngine.Application;
using Object = UnityEngine.Object;
using TheArtOfDev.HtmlRenderer.PdfSharp;
using PdfSharp;
using PdfSharp.Pdf;
using HtmlAgilityPack;

namespace TP 
{
    [CustomEditor(typeof(Readme)), ExecuteInEditMode]
    public class ReadmeEditor : Editor
    {
        public static ReadmeEditor ActiveReadmeEditor;

        #region private vars
        private Readme readme;

        private bool initialized;
        private bool verbose;
        private bool liteEditor;

        // State
        private bool editing;
        private bool boldOn;
        private bool italicOn;
        private bool sourceOn;

        // OnInspectorGUI State
        private static bool showDebugSettings;
        private static bool showDebugButtons;
        private static bool showAdvancedOptions;
        private static bool showCursorPosition;
        private static bool showObjectIdPairs;
        private static bool showDebugInfo;

        // Editor Control/Styles
        private GUISkin skinDefault;
        private GUISkin skinStyle;
        private GUISkin skinSource;
        
        // Text area focus
        private Rect doneEditButtonRect;
        
        // Advanced Options
        private string objectIdPairListString;

        // Copy buffer fix
        private string previousCopyBuffer;

        private Event currentEvent;

        private ReadmeTextArea readmeTextArea;
        #endregion
        
        private ReadmeTextEditor textEditor => ReadmeTextEditor.Instance;

        public override void OnInspectorGUI()
        {
            currentEvent = new Event(Event.current);
            
            Readme readmeTarget = (Readme)target;
            if (readmeTarget != null)
            {
                readme = readmeTarget;
                ActiveReadmeEditor = this;
            }
            
            MonoScript monoScript = MonoScript.FromScriptableObject(this);
            string path = Path.GetDirectoryName(AssetDatabase.GetAssetPath(monoScript)) ?? "";
            string settingsPath = Path.Combine(path, "..", "Runtime", "Settings");
            readme.Initialize(settingsPath);
            readme.ConnectManager();
            readme.UpdateSettings();
            
            ReadmeUtil.SetIfNull(ref readmeTextArea, () => new ReadmeTextArea(this, readme, TextAreaObjectFields, OnTextAreaChange, "Click \"Edit\" to add your readme!"));

            liteEditor = readme.ActiveSettings.lite;
            UpdateGuiStyles(readme);
            UpdateTackIcon(readme);

            #region Editor GUI

            StopInvalidTextAreaEvents();
            UpdateStyleState();
            
            readmeTextArea.Draw(editing, sourceOn, readme.RichText);
            
            if (!editing)
            {
                if (!readme.readonlyMode || Readme.disableAllReadonlyMode)
                {
                    EditorGUILayout.Space();
                    if (GUILayout.Button("Edit"))
                    {
                        editing = true;
                        readmeTextArea.RepaintTextArea(focusText:true);
                    }

                    doneEditButtonRect = ReadmeUtil.GetLastRect(doneEditButtonRect);

                    if (IsPrefab(readme.gameObject))
                    {
                        if (GUILayout.Button("Export To PDF"))
                        {
                            ExportToPdf();
                        }
                    }
                }
            }
            else
            {
                EditorGUILayout.Space();
                if (GUILayout.Button("Done"))
                {
                    editing = false;
                    readmeTextArea.RepaintTextArea(focusText:true);
                    FreeVersionDialogue();
                }
                
                doneEditButtonRect = ReadmeUtil.GetLastRect(doneEditButtonRect);

                EditorGuiToolbar();
            }
            
            EditorGuiAdvancedDropdown();

            #endregion Editor GUI

            AfterOnInspectorGUI();
        }

        private void OnTextAreaChange(string newText, TextAreaObjectField[] newTextAreaObjectFields)
        {
            RichText = newText;
            TextAreaObjectFields = newTextAreaObjectFields;
            SetTargetDirty();
        }

        private void AfterOnInspectorGUI() 
        {
            CheckKeyboardShortCuts();

            textEditor.Update();
            readmeTextArea.Update();
        }
        
        private void EditorGuiToolbar()
        {
            float smallButtonWidth = EditorGUIUtility.singleLineHeight * 2;
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

            if (GUILayout.Button(new GUIContent("B", "Bold (alt+b)"), boldButtonStyle,
                    GUILayout.Width(smallButtonWidth)))
            {
                ToggleStyle("b");
            }

            GUIStyle italicizedButtonStyle = new GUIStyle(EditorStyles.toolbarButton);
            italicizedButtonStyle.fontStyle = FontStyle.Normal;
            if (italicOn)
            {
                italicizedButtonStyle.fontStyle = FontStyle.Italic;
            }

            if (GUILayout.Button(new GUIContent("I", "Italic (alt+i)"), italicizedButtonStyle,
                    GUILayout.Width(smallButtonWidth)))
            {
                ToggleStyle("i");
            }

            GUIStyle objButtonStyle = new GUIStyle(EditorStyles.toolbarButton);
            if (GUILayout.Button(new GUIContent("Obj", "Insert Object Field (alt+o)"), objButtonStyle,
                    GUILayout.Width(smallButtonWidth)))
            {
                readmeTextArea.AddObjectField();
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
                                        " <o=\"0000001\"></o>",
                    MessageType.Info);
            }
        }

        private void EditorGuiAdvancedDropdown()
        {
            float smallButtonWidth = EditorGUIUtility.singleLineHeight * 2;
            float buttonWidth = smallButtonWidth * 4;
            float textAreaWidth = readmeTextArea.GetTextAreaSize().x;

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
                textEditor.ApplyCursorBugFix = EditorGUILayout.Toggle(fixCursorBugTooltip, textEditor.ApplyCursorBugFix);
                verbose = EditorGUILayout.Toggle("Verbose", verbose);
                readme.useTackIcon = EditorGUILayout.Toggle("Use Tack Gizmo", readme.useTackIcon);
                Readme.neverUseTackIcon = EditorGUILayout.Toggle("Never Use Tack Gizmo", Readme.neverUseTackIcon);
                readme.readonlyMode = EditorGUILayout.Toggle("Readonly Mode", readme.readonlyMode);
                GUIContent disableAllReadonlyModeTooltip = new GUIContent(
                    "Disable All Readonly Mode",
                    "Global setting to enable editing without changing each readonly readme.");
                Readme.disableAllReadonlyMode =
                    EditorGUILayout.Toggle(disableAllReadonlyModeTooltip, Readme.disableAllReadonlyMode);

                if (GUILayout.Button("View Backups", GUILayout.Width(buttonWidth)))
                {
                    EditorUtility.RevealInFinder(Readme.PersistentLocation);
                }

                showCursorPosition = EditorGUILayout.Foldout(showCursorPosition, "Cursor Position");
                if (showCursorPosition)
                {
                    EditorGUI.indentLevel++;
                    string richTextWithCursor = RichText;
                    if (TextEditorActive && textEditor.SelectIndex <= RichText.Length)
                    {
                        richTextWithCursor = richTextWithCursor.Insert(Mathf.Max(textEditor.SelectIndex, textEditor.CursorIndex), "|");
                        if (textEditor.SelectIndex != textEditor.CursorIndex)
                        {
                            richTextWithCursor = richTextWithCursor.Insert(Mathf.Min(textEditor.SelectIndex, textEditor.CursorIndex), "|");
                        }
                    }

                    richTextWithCursor = richTextWithCursor.Replace("\n", " \\n\n");
                    float adjustedTextAreaHeight = readmeTextArea.CalcHeight(richTextWithCursor, textAreaWidth - 50);
                    EditorGUILayout.SelectableLabel(richTextWithCursor, GUILayout.Height(adjustedTextAreaHeight));
                    EditorGUI.indentLevel--;
                }

                showObjectIdPairs = EditorGUILayout.Foldout(showObjectIdPairs, "Master Object Field Dictionary");
                if (showObjectIdPairs)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.Toggle("Manager Connected", readme.managerConnected);
                    EditorGUILayout.LabelField("Object Id Pairs");
                    float objectDictHeight = readmeTextArea.CalcHeight(objectIdPairListString, textAreaWidth - 50);
                    EditorGUILayout.LabelField(objectIdPairListString, GUILayout.Height(objectDictHeight));
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Refresh Pairs", GUILayout.Width(buttonWidth)) || objectIdPairListString == null)
                    {
                        objectIdPairListString = ReadmeManager.GetObjectIdPairListString();
                        readmeTextArea.RepaintTextArea();
                    }

                    if (GUILayout.Button("Clear Pairs", GUILayout.Width(buttonWidth)))
                    {
                        ReadmeManager.Clear();
                        readmeTextArea.RepaintTextArea();
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
                    "mouseOverWindow: " + EditorWindow.mouseOverWindow + "\n" + 
                    "FocusedControl: " + GUI.GetNameOfFocusedControl() + "\n" +
                    "Event Type: " + Event.current.ToString() + "\n" +
                    "textAreaBounds: " + readmeTextArea.Bounds + "\n" +
                        "scroll: " + readmeTextArea.Scroll + "\n" +
                        "Calc Cursor Position: " + (Event.current.mousePosition) + "\n" +
                        "Text Editor Active: " + TextEditorActive + "\n" +
                        "cursorIndex: " + (!TextEditorActive ? "" : textEditor.CursorIndex.ToString()) + "\n" +
                        "selectIndex: " + (!TextEditorActive ? "" : textEditor.SelectIndex.ToString()) + "\n" +
                        "cursorIndex OnTag: " + readmeTextArea.IsOnTag(textEditor.CursorIndex) + "\n" +
                        "selectIndex OnTag: " + readmeTextArea.IsOnTag(textEditor.SelectIndex) + "\n" +
                        "TagsError: " + readmeTextArea.TagsError() + "\n" +
                        "Style Map Info: " + "\n" +
                        "\t<b> tags:" + (readme.StyleMaps.ContainsKey("b")
                            ? readme.StyleMaps["b"].FindAll(isStyle => isStyle).Count.ToString()
                            : "0") + "\n" +
                        "\t<i> tags:" + (readme.StyleMaps.ContainsKey("i")
                            ? readme.StyleMaps["i"].FindAll(isStyle => isStyle).Count.ToString()
                            : "0") + "\n" +
                        ""
                    , MessageType.Info);
                    
                    MessageType messageType = textEditor != null ? MessageType.Info : MessageType.Warning;
                    
                    EditorGUILayout.HelpBox(
                        "Toggle Bold: alt+b\n" +
                        "Toggle Italic: alt+i\n" +
                        "Add Object: alt+o\n" +
                        "Show Advanced Options: alt+a\n"
                        , MessageType.Info);
                    
                    if (textEditor != null)
                    {
                        EditorGUILayout.HelpBox(
                            "ControlIds" +
                            "\n\t" + "textAreaEmpty: " + readmeTextArea.GetControlId(readmeTextArea.EmptyName) +
                            "\n\t" + "textAreaReadonly: " + readmeTextArea.GetControlId(readmeTextArea.ReadonlyName) +
                            "\n\t" + "textAreaSource: " + readmeTextArea.GetControlId(readmeTextArea.SourceName) +
                            "\n\t" + "textAreaStyle: " + readmeTextArea.GetControlId(readmeTextArea.StyleName)
                            , MessageType.Info);
                    
                        EditorGUILayout.HelpBox(
                            "TEXT EDITOR VALUES" +
                            // "\n\t" + "text: " + textEditor.text +
                            // "\n\t" + "SelectedText: " + textEditor.SelectedText +
                            "\n\t" + "multiline: " + textEditor.multiline +
                            "\n\t" + "position: " + textEditor.position +
                            "\n\t" + "style: " + textEditor.style +
                            "\n\t" + "cursorIndex: " + textEditor.cursorIndex +
                            "\n\t" + "hasSelection: " + textEditor.hasSelection +
                            "\n\t" + "scrollOffset: " + textEditor.scrollOffset +
                            "\n\t" + "selectIndex: " + textEditor.selectIndex +
                            "\n\t" + "altCursorPosition: " + textEditor.altCursorPosition +
                            "\n\t" + "controlID: " + readmeTextArea.GetControlName(textEditor.controlID) +
                            "\n\t" + "controlID_Event: " + Event.current.GetTypeForControl(textEditor.controlID) +
                            "\n\t" + "doubleClickSnapping: " + textEditor.doubleClickSnapping +
                            "\n\t" + "graphicalCursorPos: " + textEditor.graphicalCursorPos +
                            "\n\t" + "isPasswordField: " + textEditor.isPasswordField +
                            "\n\t" + "isPasswordField: " + textEditor.isPasswordField +
                            "\n\t" + "keyboardOnScreen: " + textEditor.keyboardOnScreen +
                            "\n\t" + "graphicalSelectCursorPos: " + textEditor.graphicalSelectCursorPos +
                            "\n\t" + "hasHorizontalCursorPos: " + textEditor.hasHorizontalCursorPos
                            , MessageType.Info);
                    
                        EditorGUILayout.HelpBox(
                            "GUIUtility" +
                            "\n\t" + "hotControl: " + GUIUtility.hotControl +
                            "\n\t" + "keyboardControl: " + GUIUtility.keyboardControl +
                            "\n\t" + "GetStateObject: " +
                            GUIUtility.GetStateObject(typeof(TextEditor), textEditor.controlID) +
                            "\n\t" + "QueryStateObject: " +
                            GUIUtility.QueryStateObject(typeof(TextEditor), textEditor.controlID)
                            , MessageType.Info);
                    
                        EditorGUILayout.HelpBox(
                            "EditorGUIUtility : GUIUtility" +
                            "\n\t" + "textFieldHasSelection: " + EditorGUIUtility.textFieldHasSelection
                            , MessageType.Info);
                    
                        EditorGUILayout.HelpBox(
                            "GUI" +
                            "\n\t" + "GetNameOfFocusedControl: " + GUI.GetNameOfFocusedControl()
                            , MessageType.Info);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("No textEditor Found", MessageType.Warning);
                    }

                    EditorGUI.indentLevel--;
                }
                
                showDebugSettings = EditorGUILayout.Foldout(showDebugSettings, "Debug Backups/Settings");
                if (showDebugSettings)
                {
                    GUILayout.BeginHorizontal();

                    if (GUILayout.Button("Save to File", GUILayout.Width(buttonWidth)))
                    {
                        readme.Save();
                    }

                    if (GUILayout.Button("Load from File", GUILayout.Width(buttonWidth)))
                    {
                        readme.LoadLastSave();
                        readmeTextArea.RepaintTextArea();
                    }

                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();

                    if (GUILayout.Button("New Settings File", GUILayout.Width(buttonWidth)))
                    {
                        ReadmeSettings newSettings = new ReadmeSettings();
                        newSettings.SaveSettings();
                        readmeTextArea.RepaintTextArea();
                    }

                    if (GUILayout.Button("Reload Settings", GUILayout.Width(buttonWidth)))
                    {
                        readme.UpdateSettings(true, verbose);
                        readmeTextArea.RepaintTextArea();
                    }

                    Readme.overrideSettings = EditorGUILayout.ObjectField(Readme.overrideSettings, typeof(Object), false);

                    GUILayout.EndHorizontal();
                }

                showDebugButtons = EditorGUILayout.Foldout(showDebugButtons, "Debug Buttons");
                if (showDebugButtons)
                {
                    if (GUILayout.Button("TestHtmlAgilityPack", GUILayout.Width(buttonWidth)))
                    {
                        TestHtmlAgilityPack();
                    }

                    if (GUILayout.Button("RepaintTextArea", GUILayout.Width(buttonWidth)))
                    {
                        readmeTextArea.RepaintTextArea();
                    }

                    if (GUILayout.Button("GUI.FocusControl", GUILayout.Width(buttonWidth)))
                    {
                        GUI.FocusControl(readmeTextArea.ActiveName);
                    }

                    if (GUILayout.Button("OnGui", GUILayout.Width(buttonWidth)))
                    {
                        EditorUtility.SetDirty(readme.gameObject);
                    }

                    if (GUILayout.Button("SetDirty", GUILayout.Width(buttonWidth)))
                    {
                        EditorUtility.SetDirty(readme);
                    }

                    if (GUILayout.Button("Repaint", GUILayout.Width(buttonWidth)))
                    {
                        Repaint();
                    }

                    if (GUILayout.Button("RecordObject", GUILayout.Width(buttonWidth)))
                    {
                        Undo.RecordObject(readme, "Force update!");
                    }

                    if (GUILayout.Button("FocusTextInControl", GUILayout.Width(buttonWidth)))
                    {
                        EditorGUI.FocusTextInControl(readmeTextArea.ActiveName);
                    }

                    if (GUILayout.Button("Un-FocusTextInControl", GUILayout.Width(buttonWidth)))
                    {
                        EditorGUI.FocusTextInControl("");
                    }
                }

                EditorGUI.indentLevel--;
            }
        }

        private void UpdateGuiStyles(Readme readmeTarget)
        {
            skinDefault = GUI.skin;
            skinStyle = GUI.skin;
            skinSource = GUI.skin;
            skinStyle = skinStyle != null ? skinStyle : ReadmeUtil.GetSkin(ReadmeUtil.SKIN_STYLE, this);
            skinSource = skinSource != null ? skinSource : ReadmeUtil.GetSkin( ReadmeUtil.SKIN_SOURCE, this);
            
            RectOffset padding = new RectOffset(5, 5, 5, 5);
            RectOffset margin = new RectOffset(3, 3, 3, 3);

            readmeTextArea.UpdateGuiStyles(
                new GUIStyle(skinDefault.label)
                {
                    richText = true,
                    focused = { textColor = Color.gray },
                    normal = { textColor = Color.gray },
                    font = readmeTarget.font,
                    fontSize = readmeTarget.fontSize,
                    wordWrap = true,
                    padding = padding,
                    margin = margin
                },
                new GUIStyle(skinDefault.label)
                {
                    richText = true,
                    focused = { textColor = readmeTarget.fontColor },
                    normal = { textColor = readmeTarget.fontColor },
                    font = readmeTarget.font,
                    fontSize = readmeTarget.fontSize,
                    wordWrap = true,
                    padding = padding,
                    margin = margin
                },
                new GUIStyle(skinStyle.textArea)
                {
                    richText = true,
                    font = readmeTarget.font,
                    fontSize = readmeTarget.fontSize,
                    wordWrap = true,
                    padding = padding,
                    margin = margin
                },
                new GUIStyle(skinSource.textArea)
                {
                    richText = false,
                    wordWrap = true,
                    padding = padding,
                    margin = margin
                }
            );
        }

        private void UpdateTackIcon(Readme readmeTarget)
        {
            Object selectedObject = Selection.activeObject;
            if (selectedObject != null)
            {
                if (readme.useTackIcon && !Readme.neverUseTackIcon)
                {
                    Texture2D icon =
                        AssetDatabase.LoadAssetAtPath<Texture2D>(
                            "Assets/Packages/TP/Readme/Assets/Textures/readme_icon_256_256.png"); // TODO need to make relative path. Then can remove the below check for null
                    if (icon != null)
                    {
                        IconManager.SetIcon(selectedObject as GameObject, icon);
                        readme.iconBeingUsed = true;
                    }
                }
                else if (readme.iconBeingUsed)
                {
                    IconManager.RemoveIcon(selectedObject as GameObject);
                    readme.iconBeingUsed = false;
                }
            }
        }

        private void StopInvalidTextAreaEvents()
        {
            // Stop button clicks from being used by text
            if (readmeTextArea.InvalidClick)
            {
                currentEvent.Use();
            }
        }
        
        private void SetTargetDirty()
        {
            if (!Application.isPlaying)
            {
                Undo.RegisterCompleteObjectUndo(readme, "Readme edited");

                if (IsPrefab(readme.gameObject))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(readme.gameObject);
                }
            }
        }

        public bool LiteVersionPaywall(string feature = "This")
        {
            return false; // Disabling this in favor of less intrusive FreeVersionDialogue.
            
            // if (liteEditor)
            // {
            //     string title = "Paid Feature Only";
            //     string message = feature +
            //                      " is a paid feature. To use this feature please purchase a copy of Readme from the Unity Asset Store.";
            //     string ok = "Go to Asset Store";
            //     string cancel = "Nevermind";
            //     bool result = EditorUtility.DisplayDialog(title, message, ok, cancel);
            //
            //     if (result)
            //     {
            //         Application.OpenURL("https://assetstore.unity.com/packages/slug/152336");
            //     }
            // }
            //
            // return liteEditor;
        }

        private void FreeVersionDialogue()
        {
            if (liteEditor)
            {
                ReadmeSettings activeSettings = readme.ActiveSettings;
                
                DateTime lastPopupDate = DateTime.Parse(activeSettings.lastPopupDate);
                double daysSincePopup = (DateTime.Now - lastPopupDate).TotalDays;

                if (daysSincePopup > 7 || daysSincePopup < 0)
                {
                    activeSettings.lastPopupDate = DateTime.Now.ToString();
                    activeSettings.SaveSettings();
                    readme.UpdateSettings(force:true);
                    
                    string title = "Tiny PHX Games - Readme (Free Version)";
                    string message =
                        "Hello! Thank you for trying out Readme.\n\n" +
                        "This is the free version of Readme, and it can be used indefinitely. If you love it and can afford to to purchase a copy, every sale means a lot to me.\n\n" +
                        "Would you like to purchase the asset now?";
                    string ok = "Purchase";
                    string cancel = "Cancel";
                    bool result = EditorUtility.DisplayDialog(title, message, ok, cancel);

                    if (result)
                    {
                        Application.OpenURL("https://assetstore.unity.com/packages/slug/152336");
                    }
                }
            }
        }

        private void CheckKeyboardShortCuts()
        {
            if (ReadmeUtil.UnityInFocus)
            {
                //Alt + a for toggle advanced mode
                if (currentEvent.type == EventType.KeyDown && currentEvent.alt && currentEvent.keyCode == KeyCode.A)
                {
                    showAdvancedOptions = !showAdvancedOptions;
                    Event.current.Use();
                    readmeTextArea.RepaintTextArea();
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
                        readmeTextArea.AddObjectField();
                        Event.current.Use();
                    }
                }

                //Ctrl + v for paste
                if (currentEvent.type == EventType.KeyDown && currentEvent.control && currentEvent.keyCode == KeyCode.V)
                {
                    //TODO review why this is empty
                }
            }
        }

        private void SetFontColor(Color color)
        {
            if (color == readme.fontColor)
            {
                return;
            }

            if (LiteVersionPaywall("Setting the font color"))
            {
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

            if (LiteVersionPaywall("Setting the font style"))
            {
                return;
            }

            readme.font = font;
            readmeTextArea.RepaintTextArea(focusText:true);
        }

        private void SetFontSize(int size)
        {
            if (readme.fontSize != 0)
            {
                if (size == readme.fontSize)
                {
                    return;
                }

                if (LiteVersionPaywall("Setting the font size"))
                {
                    return;
                }
            }

            readme.fontSize = size;
            readmeTextArea.RepaintTextArea();
        }

        private void ToggleStyle(string tag)
        {
            if (LiteVersionPaywall("Rich text shortcuts"))
            {
                return;
            }

            if (readmeTextArea.TagsError())
            {
                Debug.LogWarning("Please fix any mismatched tags first!");
                return;
            }

            if (TextEditorActive)
            {
                int styleStartIndex = readme.GetPoorIndex(textEditor.StartIndex);
                int styleEndIndex = readme.GetPoorIndex(textEditor.EndIndex);
                int poorStyleLength = styleEndIndex - styleStartIndex;

                readme.ToggleStyle(tag, styleStartIndex, poorStyleLength);

                if (readmeTextArea.TagsError())
                {
                    readme.LoadLastSave(); //TODO review if there is better flow than this.
                    Debug.LogWarning("You can't do that!");
                }

                UpdateStyleState();

                int newCursorIndex = readmeTextArea.GetNearestPoorTextIndex(readme.GetRichIndex(styleStartIndex));
                int newSelectIndex = readmeTextArea.GetNearestPoorTextIndex(readme.GetRichIndex(styleEndIndex));

                readmeTextArea.RepaintTextArea(newCursorIndex, newSelectIndex, true);
            }
        }

        private void UpdateStyleState()
        {
            if (TextEditorActive)
            {
                int index = 0;
                int poorCursorIndex = readme.GetPoorIndex(textEditor.CursorIndex);
                int poorSelectIndex = readme.GetPoorIndex(textEditor.SelectIndex);

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

        private void ExportToPdf()
        {
            if (LiteVersionPaywall("Export to PDF"))
            {
                return;
            }

            // EditorGuiTextAreaObjectFields();
            string currentPath = AssetDatabase.GetAssetPath(readme);
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

        public void SelectAll()
        {
            textEditor?.SelectAll();
        }

        public void CopyRichText()
        {
            if (textEditor != null)
            {
                string textToCopy = textEditor.SelectedText.Length > 0 ? textEditor.SelectedText : readme.RichText;
                EditorGUIUtility.systemCopyBuffer = textToCopy;
                FixCopyBuffer();
            }
        }

        public void CopyPlainText()
        {
            CopyRichText();
            ForceCopyBufferToPoorText();
        }

        private void FixCopyBuffer()
        {
            if ((!(editing && sourceOn) && !readmeTextArea.TagsError()))
            {
                if (EditorGUIUtility.systemCopyBuffer != previousCopyBuffer && previousCopyBuffer != null)
                {
                    if (TextEditorActive)
                    {
                        List<string> tagPatterns = new List<string>
                        {
                            "<b>",
                            "<i>",
                        };

                        foreach (string tagPattern in tagPatterns)
                        {
                            int textStart = textEditor.StartIndex - tagPattern.Length;
                            int textLength = tagPattern.Length;

                            if (textStart >= 0 && RichText.Substring(textStart, textLength) == tagPattern)
                            {
                                EditorGUIUtility.systemCopyBuffer = RichText.Substring(textStart, textEditor.EndIndex - textStart);
                                break;
                            }
                        }
                    }
                }

                previousCopyBuffer = EditorGUIUtility.systemCopyBuffer;
            }
        }

        private void ForceCopyBufferToPoorText()
        {
            string newCopyBuffer = EditorGUIUtility.systemCopyBuffer;
            if (TextEditorActive && (!sourceOn && !readmeTextArea.TagsError()))
            {
                newCopyBuffer = Readme.MakePoorText(newCopyBuffer);
            }

            EditorGUIUtility.systemCopyBuffer = newCopyBuffer;
            previousCopyBuffer = newCopyBuffer;
        }

        private static bool IsPrefab(GameObject gameObject)
        {
            bool isPrefab = gameObject != null && (gameObject.scene.name == null ||
                                                   gameObject.gameObject != null &&
                                                   gameObject.gameObject.scene.name == null);
            return isPrefab;
        }

        private string RichText
        {
            get => readme.RichText;
            set => readme.RichText = value;
        }

        private string ActiveText => readme.RichText;

        private bool TextEditorActive => textEditor.TextEditorActive;
        
        // private bool TextEditorActive =>
        //     textEditor != null && activeTextAreaName != "" && GUI.GetNameOfFocusedControl() == activeTextAreaName;
        
        private TextAreaObjectField[] TextAreaObjectFields
        {
            get => readme.TextAreaObjectFields;
            set => readme.TextAreaObjectFields = value;
        }


        public void ToggleReadOnly()
        {
            readme.readonlyMode = !readme.readonlyMode;
        }

        public void ToggleScroll()
        {
            readmeTextArea.ScrollEnabled = !readmeTextArea.ScrollEnabled;
        }

        public void ToggleEdit()
        {
            editing = !editing;
        }
        
        private void TestHtmlAgilityPack()
        {
            if (RichText != null)
            {
                string html = RichTextToHtml(readme.RichText);
                
                HtmlDocument htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);
                foreach (var error in htmlDoc.ParseErrors)
                {
                    Debug.LogWarning(
                        "Code: " + error.Code + "\n" +
                        "Reason: " + error.Reason + "\n" +
                        "Line: " + error.Line + "\n" +
                        "LinePosition: " + error.LinePosition + "\n" +
                        "SourceText: " + error.SourceText + "\n" +
                        "StreamPosition: " + error.StreamPosition
                    );
                }
                Debug.Log(htmlDoc.Text);
                Debug.Log(htmlDoc.DocumentNode.InnerText);

                //List<bool> htmlTagMap = new List<bool>();
                foreach (HtmlNode node in htmlDoc.DocumentNode.Descendants())
                {
                    if (node.Name != "#text")
                    {
                        Debug.Log(string.Format("<{0}> Outer - line: {1} startline: {2} outerStart: {3} length: {4}",
                            node.Name, node.Line, node.LinePosition, node.OuterStartIndex, node.OuterHtml.Length));
                        Debug.Log(string.Format("<{0}> Inner - line: {1} startline: {2} innerStart: {3} length: {4}",
                            node.Name, node.Line, node.LinePosition, node.InnerStartIndex, node.InnerHtml.Length));
                        foreach (HtmlAttribute attribute in node.GetAttributes())
                        {
                            Debug.Log("\t" + attribute.Name + " " + attribute.Value);
                        }
                    }
                }
                
                Debug.Log(HtmlToRichText(htmlDoc.Text));
            }
        }

        private string RichTextToHtml(string richText)
        {
            // Replace elements followed by equal like "<size=" with attribute formatting "<size value="
            return Regex.Replace(richText, "<([a-zA-Z_0-9]+)=", "<$1 value=");
        }

        private string HtmlToRichText(string richText)
        {
            //Reverse RichTextToHtml
            return Regex.Replace(richText, "<([a-zA-Z_0-9]+) value=", "<$1=");
        }
    }
}

#endif