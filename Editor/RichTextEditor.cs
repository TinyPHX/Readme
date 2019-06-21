using UnityEngine;
using UnityEditor;
using System.Reflection;

namespace TP.Readme
{
    public class RichTextEditor
    {
        private TextEditor textEditor;
        private string poorText;
        
        private bool fixCursorBug = true;
        private string textAreaName = "";
        private Rect textAreaRect;
        private int previousSelctIndex = -1;
        private bool selectIndexChanged = false;
        private int previousCursorIndex = -1;
        private bool cursorIndexChanged = false;
        
        public RichTextEditor()
        {
            textEditor = typeof(EditorGUI)
                .GetField("activeEditor", BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null) as TextEditor;

        }

        public void setTextArea(string textAreaName, Rect textAreaRect)
        {
            this.textAreaName = textAreaName;
            this.textAreaRect = textAreaRect;
        }
        
        public string MakePoorText(string richText)
        {
            return richText
                .Replace("<b>", "")
                .Replace("</b>", "")
                .Replace("<i>", "")
                .Replace("</i>", "");
            
            //  <color=#00ffffff>
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
    
        public int GetPoorSelectIndex(string richText, int richSelectIndex)
        {
            int poorSelectIndex = richSelectIndex;
    
            for (int i = 0; i < richSelectIndex; i++)
            {
                char character = richText[i];
                if (character == '<' || character == '/')
                {
                    foreach (string tag in Readme.SupportedTags)
                    {
                        if (richText.Length >= richSelectIndex + tag.Length && richText.Substring(richSelectIndex, tag.Length) == tag)
                        {
                            int richCharCount = 3;
                            if (character == '/')
                            {
                                richCharCount += 1;
                            }
    
                            poorSelectIndex -= richCharCount;
                        }
                    }
                }
            }
    
            return poorSelectIndex;
        }
    
        public int GetRichSelectIndex(string richText, int selectIndex)
        {
            int richSelectIndex = selectIndex;
            
            for (int i = 0; i < richSelectIndex; i++)
            {
                char character = richText[i];
                if (character == '<' || character == '/')
                {
                    foreach (string tag in Readme.SupportedTags)
                    {
                        if (richText.Length >= richSelectIndex + tag.Length && richText.Substring(richSelectIndex, tag.Length) == tag)
                        {
                            int richCharCount = 3;
                            if (character == '/')
                            {
                                richCharCount += 1;
                            }
    
                            richSelectIndex += richCharCount;
                            i += 3;
    
                            break;
                        }
                    }
                }
            }
    
            return richSelectIndex;
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
        
        public int cursorIndex
        {
            get
            {
                return textEditor.cursorIndex;
            }
        }
        
        public int selectIndex
        {
            get { return textEditor.selectIndex }
        }
        
        public string text
        {
            get
            {
                return textEditor.text;
            }
        }

        public string poorText
        {
            get
            {
                return textEditor.text;
            }
        }

        public Rect TextAreaRect
        {
            get { return textAreaRect; }
        }

        public bool FixCursorBug
        {
            get { return fixCursorBug; }
            set { fixCursorBug = value; }
        }
    }
}