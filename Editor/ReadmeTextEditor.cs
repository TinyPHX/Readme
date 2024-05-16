using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TP
{
    public class ReadmeTextEditor
    {
        private static ReadmeTextEditor instance;
        public static ReadmeTextEditor Instance => ReadmeUtil.SetIfNull(ref instance, () => new ReadmeTextEditor());

        private TextEditor textEditor;
        public TextEditor TextEditor => ReadmeUtil.SetIfNull(ref textEditor, () => GetPrivateTextEditor);
        public bool HasTextEditor => TextEditor != null;
        
        private bool selectIndexChanged;
        private bool cursorIndexChanged;
        private bool editorSelectIndexChanged;
        private bool editorCursorIndexChanged;
        private int currentCursorIndex = -1;
        private int currentSelectIndex = -1;
        private bool richTextChanged;
        private bool mouseCaptured;
        private readonly Stack tempCursorIndex = new Stack();
        private readonly Stack tempSelectIndex = new Stack();
        
        private Action<int> onCursorChangedCallback;

        public bool ApplyCursorBugFix { get; set; } = true;
        
        private ReadmeTextEditor()
        {
            textEditor = TextEditor;
        }

        private ReadmeTextArea readmeTextArea;
        public void RegisterTextArea(ReadmeTextArea readmeTextArea)
        {
            this.readmeTextArea = readmeTextArea;
        }

        private bool HasTextArea => readmeTextArea != null;

        private bool TextAreaActive => readmeTextArea != null && (readmeTextArea.HasTextEditorFocus || readmeTextArea.HasGuiFocus);

        private TextEditor GetPrivateTextEditor =>
            typeof(EditorGUI)
                .GetField("activeEditor", BindingFlags.Static | BindingFlags.NonPublic)
                ?.GetValue(null) as TextEditor;
        
        private Event currentEvent => Event.current != null ? new Event(Event.current) : default;

        public void Update()
        {
            FixCursorBug();
        }
        
        public bool CursorChanged => selectIndexChanged || cursorIndexChanged;
        public bool InternalCursorChanged => currentSelectIndex != SelectIndex || currentCursorIndex != CursorIndex;

        public void SetText(string text)
        {
            this.text = text;
        }

        public void SetCursors((int, int) cursors)
        {
            if (textEditor != null)
            {
                (int cursorIndex, int selectIndex) = cursors;
                if (cursorIndex != -1)
                {
                    textEditor.cursorIndex = cursorIndex;
                }

                if (selectIndex != -1)
                {
                    textEditor.selectIndex = selectIndex;
                }
            }
        }

        public (int, int)  GetCursors()
        {
            return textEditor == null ? (-1, -1) : (textEditor.cursorIndex, textEditor.selectIndex);
        }
        
        public void BeforeTextAreaChange(ReadmeTextArea readmeTextArea)
        {
            if (!readmeTextArea.TagsError() && !readmeTextArea.SourceOn)
            {
                if (currentEvent != null && currentEvent.type == EventType.KeyDown &&
                    new KeyCode[] { KeyCode.Backspace, KeyCode.Delete }.Contains(currentEvent.keyCode) &&
                    CursorIndex == SelectIndex)
                {
                    int direction = currentEvent.keyCode == KeyCode.Backspace ? -1 : 0;
                    int charIndex = CursorIndex + direction;
                    string objTag = direction == 0 ? " <o=" : "</o> ";
                    int objTagStart = direction == 0 ? charIndex : charIndex - 4;
                    int objTagLength = objTag.Length;
                    bool objectField = objTagStart > 0 &&
                                       objTagStart + objTagLength <= readmeTextArea.Text.Length &&
                                       readmeTextArea.Text.Substring(objTagStart, objTagLength) == objTag;

                    if (objectField)
                    {
                        int nextPoorIndex = readmeTextArea.GetNearestPoorTextIndex(charIndex + (direction == 0 ? 1 : 0), direction);
                        bool poorCharFound = (nextPoorIndex - charIndex) * (direction == 0 ? 1 : -1) > 0;

                        if (!poorCharFound)
                        {
                            nextPoorIndex = 0;
                        }

                        SelectIndex = nextPoorIndex;
                        EndIndex -= 1;
                        Event.current.Use();
                    }
                    else
                    {
                        if (charIndex < 0 || CursorIndex > readmeTextArea.Text.Length)
                        {
                            int newIndex = readmeTextArea.GetNearestPoorTextIndex(charIndex - direction);
                            CursorIndex = newIndex;
                            SelectIndex = newIndex;
                            Event.current.Use();
                        }
                        else if (readmeTextArea.IsInTag(charIndex))
                        {
                            CursorIndex += direction == 1 ? 1 : -1;
                            SelectIndex += direction == 1 ? 1 : -1;
                        
                            BeforeTextAreaChange(readmeTextArea);
                        }
                    }
                }
            }
        }

        public void AfterTextAreaChange(ReadmeTextArea readmeTextArea)
        {
            if (!readmeTextArea.TagsError() && !readmeTextArea.SourceOn && currentEvent != null)
            {
                int direction = currentEvent.keyCode == KeyCode.Backspace ? -1 : 0;
                CursorIndex = readmeTextArea.GetNearestPoorTextIndex(CursorIndex, -direction);
                SelectIndex = readmeTextArea.GetNearestPoorTextIndex(SelectIndex, -direction);
            }
        }
        
        private void FixCursorBug()
        {
            if (ApplyCursorBugFix && TextEditorActive && readmeTextArea.RichTextDisplayed)
            {
                editorSelectIndexChanged = currentSelectIndex != SelectIndex;
                editorCursorIndexChanged = currentCursorIndex != CursorIndex;

                if (!AllTextSelected())
                {
                    FixMouseCursor();
                }

                FixArrowCursor();
            }

            richTextChanged = false;
        }

        private void FixMouseCursor()
        {
            EventType[] mouseEvents = new EventType[] { EventType.MouseDown, EventType.MouseDrag, EventType.MouseUp };
            bool mouseEvent = currentEvent != null && mouseEvents.Contains(currentEvent.type);

            if (currentEvent != null && currentEvent.type == EventType.MouseDown && readmeTextArea.Contains(currentEvent.mousePosition))
            {
                mouseCaptured = true;
            }

            if (mouseCaptured && mouseEvent && Event.current.clickCount <= 1)
            {
                int rawMousePositionIndex = MousePositionToIndex;
                if (rawMousePositionIndex != -1)
                {
                    int mousePositionIndex = readmeTextArea.GetNearestPoorTextIndex(rawMousePositionIndex);

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

            if (currentEvent != null && currentEvent.type == EventType.MouseUp)
            {
                mouseCaptured = false;
            }
        }

        private void FixArrowCursor()
        {
            KeyCode[] arrowKeys = new KeyCode[] { KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.RightArrow, KeyCode.LeftArrow };
            bool isKeyboard = Event.current != null && arrowKeys.Contains(Event.current.keyCode);

            bool isDoubleClick = Event.current != null && Event.current.clickCount == 2;
            bool clickInTextArea = currentEvent != null && readmeTextArea.Contains(currentEvent.mousePosition);
            if (currentEvent != null && (isKeyboard || isDoubleClick || richTextChanged || AllTextSelected()))
            {
                int direction = isDoubleClick ? 1 : 0;

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
                    CursorIndex = readmeTextArea.GetNearestPoorTextIndex(CursorIndex, direction);
                    SelectIndex = readmeTextArea.GetNearestPoorTextIndex(SelectIndex, direction);
                    cursorIndexChanged = false;
                    selectIndexChanged = false;
                }

                // Fixes double clicks that end with cursor within RichText tag.  
                if (isDoubleClick && clickInTextArea && text.Length > 0)
                {
                    int mouseIndex = MousePositionToIndex;
                    char characterClicked = text[Mathf.Clamp(mouseIndex, 0, text.Length - 1)];
                    if (!char.IsWhiteSpace(characterClicked)) // Dont fix word select if clicked character is a a space
                    {
                        SelectIndex = mouseIndex;
                        CursorIndex = mouseIndex;
                        SelectIndex = readmeTextArea.GetNearestPoorTextIndex(WordStartIndex, -1);
                        CursorIndex = readmeTextArea.GetNearestPoorTextIndex(WordEndIndex, 1);
                    }
                }
            }
        }

        public bool AllTextSelected(string text = "", int cursorIndex = -1, int selectIndex = -1)
        {
            int startIndex = -1;
            int endIndex = -1;

            bool defaultIndex = cursorIndex == -1 || selectIndex == -1;

            startIndex = defaultIndex ? StartIndex : Mathf.Min(cursorIndex, selectIndex);
            endIndex = defaultIndex ? EndIndex : Mathf.Max(cursorIndex, selectIndex);

            return TextEditorActive && (startIndex == 0 && endIndex == text.Length);
        }

        public Rect GetRect(int startIndex, int endIndex, string text = null)
        {
            ReadmeUtil.SetIfNull(ref text, () => this.text);
            Rect textEditorRect = textEditor?.position ?? new Rect();

            float padding = 1;
            int fontSize = 12; // TODO get size from size map

            Vector2 startPositionIndex1 = GetGraphicalCursorPosition(startIndex, text);
            Vector2 startPositionIndex2 = GetGraphicalCursorPosition(startIndex + 1, text);
            Vector2 startPosition;

            if (startPositionIndex1.y != startPositionIndex2.y && startIndex != endIndex)
            {
                startPosition = startPositionIndex2 + new Vector2(padding, 0);
            }
            else
            {
                startPosition = startPositionIndex1 + new Vector2(padding, 0);
            }

            Vector2 endPosition = GetGraphicalCursorPosition(endIndex, text) + new Vector2(-padding, 0);
            float height = readmeTextArea.CalcHeight(fontSize: fontSize) - 10;

            if (startPosition.y != endPosition.y)
            {
                endPosition.x = textEditorRect.xMax - 20;
            }

            endPosition.y += height;

            Vector2 size = endPosition - startPosition;

            Rect rect = new Rect(startPosition, size);

            return rect;
        }

        private Vector2 GetGraphicalCursorPosition(int cursorIndex = -1, string text = null)
        {
            ReadmeUtil.SetIfNull(ref text, () => this.text);
            
            Vector2 graphicalCursorPosition = Vector2.zero;
            
            if (HasTextEditor && cursorIndex == -1)
            {
                cursorIndex = CursorIndex;
            }

            if (HasTextArea && cursorIndex != -1)
            {
                graphicalCursorPosition = readmeTextArea.GetCursorPixelPosition(cursorIndex, text);
            }

            return graphicalCursorPosition;
        }

        private int MousePositionToIndex => PositionToIndex(currentEvent.mousePosition);

        public int PositionToIndex(Vector2 position)
        {
            int index = -1;
            SaveCursorIndex();

            Vector2 goalPosition = position + readmeTextArea.Scroll;

            float cursorYOffset = readmeTextArea.lineHeight;

            textEditor.cursorIndex = 0;
            textEditor.selectIndex = 0;
            int maxAttempts = text.Length;
            textEditor.cursorIndex = readmeTextArea.GetNearestPoorTextIndex(CursorIndex);
            Vector2 currentGraphicalPosition = GetGraphicalCursorPosition();
            int attempts = 0;
            for (int currentIndex = CursorIndex; index == -1; currentIndex = CursorIndex)
            {
                attempts++;
                if (attempts > maxAttempts)
                {
                    break;
                }

                // TODO: Check for end of word wrapped line.
                bool isEndOfLine = text.Length <= currentIndex || text[currentIndex] == '\n';

                if (currentGraphicalPosition.y < goalPosition.y - cursorYOffset)
                {
                    textEditor.MoveRight();
                    textEditor.cursorIndex = readmeTextArea.GetNearestPoorTextIndex(CursorIndex);
                    textEditor.selectIndex = readmeTextArea.GetNearestPoorTextIndex(CursorIndex);
                }
                else if (currentGraphicalPosition.x < goalPosition.x && !isEndOfLine)
                {
                    textEditor.MoveRight();
                    textEditor.cursorIndex = readmeTextArea.GetNearestPoorTextIndex(CursorIndex);
                    textEditor.selectIndex = readmeTextArea.GetNearestPoorTextIndex(CursorIndex);

                    if (GetGraphicalCursorPosition().x < currentGraphicalPosition.x)
                    {
                        index = CursorIndex;
                    }
                }
                else
                {
                    index = CursorIndex;
                }

                if (CursorIndex == text.Length)
                {
                    index = CursorIndex;
                }

                currentGraphicalPosition = GetGraphicalCursorPosition();
            }

            LoadCursorIndex();

            return index;
        }

        private int WordStartIndex
        {
            get
            {
                SaveCursorIndex();

                textEditor.MoveWordLeft();
                int wordStartIndex = SelectIndex;

                LoadCursorIndex();

                return wordStartIndex;
            }
        }

        private int WordEndIndex
        {
            get
            {
                SaveCursorIndex();

                textEditor.MoveWordRight();
                int wordStartIndex = SelectIndex;

                LoadCursorIndex();

                return wordStartIndex;
            }
        }

        public int StartIndex
        {
            get => Math.Min(CursorIndex, SelectIndex);
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

        public int EndIndex
        {
            get => Math.Max(CursorIndex, SelectIndex);
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

        public int CursorIndex
        {
            get => textEditor?.cursorIndex ?? 0;
            private set
            {
                if (textEditor != null)
                {
                    if (currentCursorIndex != value)
                    {
                        cursorIndexChanged = true;
                        currentCursorIndex = value;
                    }

                    textEditor.cursorIndex = value;
                };
            }
        }

        public int SelectIndex
        {
            get => textEditor?.selectIndex ?? 0;
            private set
            {
                if (textEditor != null)
                {
                    if (currentSelectIndex != value)
                    {
                        selectIndexChanged = true;
                        currentSelectIndex = value;
                    }

                    textEditor.selectIndex = value;
                };
            }
        }

        private void SaveCursorIndex()
        {
            tempCursorIndex.Push(CursorIndex);
            tempSelectIndex.Push(SelectIndex);   
        }

        private void LoadCursorIndex()
        {
            textEditor.cursorIndex = (int)tempCursorIndex.Pop();
            textEditor.selectIndex = (int)tempSelectIndex.Pop();
        }

        public bool TextEditorActive => HasTextEditor && TextAreaActive;
        
        #region Passthrough to public TextEditor interface
        
        public int controlID { get => TextEditor?.controlID ?? -1; set { if(HasTextEditor) TextEditor.controlID = value; }}
        public string text { get => TextEditor.text; set { if(HasTextEditor) TextEditor.text = value; }}
        public int cursorIndex  { get => TextEditor.cursorIndex; set { if(HasTextEditor) TextEditor.cursorIndex = value; }} 
        public int selectIndex { get => TextEditor.selectIndex; set { if(HasTextEditor) TextEditor.selectIndex = value; }}
        public Vector2 scrollOffset { get => TextEditor.scrollOffset; set { if(HasTextEditor) TextEditor.scrollOffset = value; }}
        public bool multiline { get => TextEditor.multiline; set { if(HasTextEditor) TextEditor.multiline = value; }}
        public Rect position { get => TextEditor.position; set { if(HasTextEditor) TextEditor.position = value; }}
        public GUIStyle style { get => TextEditor.style; set { if(HasTextEditor) TextEditor.style = value; }}
        public int altCursorPosition { get => TextEditor.altCursorPosition; set { if(HasTextEditor) TextEditor.altCursorPosition = value; }}
        public Vector2 graphicalSelectCursorPos { get => TextEditor.graphicalSelectCursorPos; set { if(HasTextEditor) TextEditor.graphicalSelectCursorPos = value; }}
        public Vector2 graphicalCursorPos { get => TextEditor.graphicalCursorPos; set { if(HasTextEditor) TextEditor.graphicalCursorPos = value; }}
        public TextEditor.DblClickSnapping doubleClickSnapping { get => TextEditor.doubleClickSnapping; set { if(HasTextEditor) TextEditor.doubleClickSnapping = value; }}
        public bool isPasswordField { get => TextEditor.isPasswordField; set { if(HasTextEditor) TextEditor.isPasswordField = value; }}
        public TouchScreenKeyboard keyboardOnScreen { get => TextEditor.keyboardOnScreen; set { if(HasTextEditor) TextEditor.keyboardOnScreen = value; }}
        public bool hasHorizontalCursorPos { get => TextEditor.hasHorizontalCursorPos; set { if(HasTextEditor) TextEditor.hasHorizontalCursorPos = value; }}
        
        public string SelectedText { get => TextEditor.SelectedText; }
        public bool hasSelection { get => TextEditor.hasSelection; }

        public void MoveRight() => TextEditor.MoveRight();
        public void MoveLeft() => TextEditor.MoveLeft();
        public void MoveWordRight() => TextEditor.MoveWordRight();
        public void MoveWordLeft() => TextEditor.MoveWordLeft();

        public void SelectAll() => textEditor.SelectAll();

        #endregion
    }
}