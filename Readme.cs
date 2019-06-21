using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TP.Readme {
    
    [Serializable]
    public class ReadmeData
    {
        public string richText;
    }
    
    [ExecuteInEditMode]
    public class Readme : MonoBehaviour
    {
        //TODO add color rich text tag support and remove this.
        public Color fontColor = Color.black;
    
        private Dictionary<string, List<bool>> styleMaps = new Dictionary<string, List<bool>>();
        public List<bool> richTextTagMap;
    
        public Font font;
        public int fontSize = 0;
    
        public static bool advancedOptions = false;
        
        private static List<string> supportedTags = new List<string>() {"b", "i", "color"};
        
        private string text = "";
        [SerializeField] private ReadmeData readmeData;
        private string lastSavedFileName = "";
    
        public string RichText
        {
            get { return readmeData.richText; }
            set
            {
                if (value == null)
                {
                    readmeData.richText = "";
                }
                else
                {
                    readmeData.richText = value;
                }
                
                text = MakePoorText(readmeData.richText);
                BuildRichTextTagMap();
                RebuildStyleMaps();
            }
        }
    
        public string Text
        {
            get
            {
                if (String.IsNullOrEmpty(text) && !String.IsNullOrEmpty(RichText))
                {
                    text = MakePoorText(RichText);
                }
                
                return text;
            }
        }
    
        public Dictionary<string, List<bool>> StyleMaps
        {
            get { return styleMaps; }
        }
    
        public static List<string> SupportedTags
        {
            get { return supportedTags; }
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

        public bool IsStyle(string style, int index)
        {
            bool isStyle = false;

            if (StyleMaps.ContainsKey(style))
            {
                List<bool> styleMap = StyleMaps[style];
                if (styleMap.Count > index)
                {
                    isStyle =  styleMap[index];
                }
            }

            return isStyle;
        }
        
        public void ToggleStyle(string tag, int startIndex, int length)
        {
            RebuildStyleMaps();
            
            if (!supportedTags.Contains(tag))
            {
                Debug.LogWarning("The <" + tag + "> tag is not supported");
            }
            
            if (!StyleMaps.ContainsKey(tag))
            {
                styleMaps.Add(tag, Enumerable.Repeat(false, text.Length).ToList());
            }
            
            List<bool> styleMap = StyleMaps[tag];
            
            try
            {
                bool styleFound = false;
                bool nonStyleFound = false;
                foreach (bool isStyle in styleMap.GetRange(startIndex, length))
                {
                    styleFound |= isStyle;
                    nonStyleFound |= !isStyle;
                }
    
                bool styleMixed = styleFound && nonStyleFound;
                bool forceAllStyle = styleMixed;
    
                styleMaps[tag] = styleMap.Select((currentIsStyle, index) => {
                    bool newIsStyle = currentIsStyle;
                    if (index >= startIndex && index < startIndex + length)
                    {
                        newIsStyle = forceAllStyle || !currentIsStyle;
                    }
                    return newIsStyle;
                }).ToList();
                
                ApplyStyleMap(tag);
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
                LoadLastSave();
            }
        }
    
        public void ApplyStyleMap(string tag)
        {
            List<bool> styleMapCopy = StyleMaps[tag];
            
            RichText = RichText
                .Replace(Tag(tag), "")
                .Replace(EndTag(tag), "");
    
            // Iterate backwards through bold map so we don't have to rebuild richTextTagMap every time
            // we insert a new tag.
            string newRichText = RichText;
            for (int i = styleMapCopy.Count; i >= 0; i--)
            {
                bool currentIsStyle = i < styleMapCopy.Count ? styleMapCopy[i] : false;
                bool nextIsStyle = i > 0 ? styleMapCopy[i - 1] : false;
    
                if (!currentIsStyle && nextIsStyle)
                {
                    newRichText = newRichText.Insert(GetRichIndex(i), EndTag(tag));
                }
                else if (currentIsStyle && !nextIsStyle)
                {
                    newRichText = newRichText.Insert(GetRichIndex(i), Tag(tag));
                }
            }
    
            RichText = newRichText;
        }
    
        public void BuildRichTextTagMap()
        {
            richTextTagMap = Enumerable.Repeat(false, RichText.Length).ToList();
    
            for (int i = 0; i < RichText.Length; i++)
            {
                char character = RichText[i];
                if (character == '<')
                {
                    bool endTag = RichText.Length <= i + 1 ? false : RichText[i + 1] == '/';
                    int tagNameIndex = endTag ? i : i + 1;
                    int richCharStart = i;
                        
                    foreach (string tagName in SupportedTags)
                    {
                        string richTextTag = (endTag ? "</" : "<") + tagName + ">";
                            
                        if (RichText.Length >= richCharStart + richTextTag.Length && 
                            RichText.Substring(richCharStart, richTextTag.Length) == richTextTag)
                        {
                            for (int j = richCharStart; j < richCharStart + richTextTag.Length; j++)
                            {
                                richTextTagMap[j] = true;
                            }
                        }
                    }
                }
            }
        }
        
        public void RebuildStyleMaps()
        {
            Dictionary<string, List<bool>> updatedStyleMaps = new Dictionary<string, List<bool>>();
            
            foreach (string tag in supportedTags)
            {
                List<bool> styleMap = Enumerable.Repeat(false, text.Length).ToList();
    
                int lastTagIndex = 0;
                int maxTagCount = RichText.Split('<').Length - 1;
                for (int i = 0; i < maxTagCount; i++)
                {
                    int startOfStyle = RichText.IndexOf(Tag(tag), lastTagIndex);
                    if (startOfStyle == -1)
                    {
                        break;
                    }
                    lastTagIndex = startOfStyle + 1;
    
                    int endOfStyle = RichText.IndexOf(EndTag(tag), startOfStyle);
                    if (endOfStyle == -1)
                    {
                        break;
                    }
                
                    for (int styleIndex = startOfStyle; styleIndex < endOfStyle; styleIndex++)
                    {
                        int poorTextIndex = GetPoorIndex(styleIndex);
                        styleMap[poorTextIndex] = true;
                    }
                }
    
                updatedStyleMaps[tag] = styleMap;
            }
    
            styleMaps = updatedStyleMaps;
        }
        
        //TODO can optimize by building a rich to poor index map.
        public int GetPoorIndex(int richIndex)
        {
            int poorIndex = 0;
    
            for (int i = 0; i < richIndex; i++)
            {
                if (!richTextTagMap[i])
                {
                    poorIndex++;
                }
            }
    
            return poorIndex;
        }
        
        public int GetRichIndex(int poorIndex)
        {
            int richIndex = poorIndex;
    
            for (int i = 0; i < richIndex; i++)
            {
                if (richTextTagMap[i])
                {
                    richIndex++;
                }
            }
    
            return richIndex;
        }
        
        public void Save()
        {
            string jsonReadMeData = JsonUtility.ToJson(readmeData, true);
            string fileName = Application.persistentDataPath + "/Readme_" + gameObject.name + "_" + DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss") + ".json";
            File.WriteAllText (fileName, jsonReadMeData);
            lastSavedFileName = fileName;
            Debug.Log("Readme RichText saved to file: " + fileName);
        }
    
        public void LoadLastSave()
        {
            if (lastSavedFileName != "")
            {
                string fileToLoad = lastSavedFileName;
                
                //Save before loading just in case!
                Save();
                
                string json = File.ReadAllText(fileToLoad);
                RichText = JsonUtility.FromJson<ReadmeData>(json).richText;
            }
        }
    
        public bool IsStyle(string tag, int index, bool richIndex = false)
        {
            bool isStyle = false;
            if (richIndex)
            {
                index = GetPoorIndex(index);
            }
    
            if (StyleMaps.ContainsKey(tag))
            {
                isStyle = StyleMaps[tag][index];
            }
            
            return isStyle;
        }
        
        public string Tag(string tagName)
        {
            return "<" + tagName + ">";
        }
        
        public string EndTag(string tagName)
        {
            return "</" + tagName + ">";
        }
    }
}