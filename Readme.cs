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
    }
}