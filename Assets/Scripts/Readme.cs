#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = System.Object;

namespace TP.Readme {
    
    [Serializable]
    public class ReadmeData
    {
        public string richText = "";
        public TextAreaObjectField[] textAreaObjectFields = new TextAreaObjectField[0];
        public List<ObjectIdPair> objectIdPairs;
    }
    
    [DisallowMultipleComponent, ExecuteInEditMode, HelpURL("https://forum.unity.com/threads/wip-a-readme-component.698477/")]
    public class Readme : MonoBehaviour
    {        
        public static readonly Color selectedColor = new Color(0f / 255, 130f / 255, 255f / 255, .6f);
        public static readonly Color lightBackgroundColor = new Color(211f / 255, 211f / 255, 211f / 255);
        public static readonly Color darkBackgroundColor = new Color(0.22f, 0.22f, 0.22f);
        public static readonly Color lightFontColor = new Color(0, 0, 0);
        public static readonly Color darkFontColor = new Color(0.82f, 0.82f, 0.82f);
        
        private bool initialized = false;
        
        //TODO add color rich text tag support and remove this.
        public Color fontColor = Color.black;
    
        private Dictionary<string, List<bool>> styleMaps = new Dictionary<string, List<bool>>();
        public List<bool> richTextTagMap;
    
        public Font font;
        public int fontSize = 0;
    
        //Editor Fields
        public bool iconBeingUsed = true;
        public bool useTackIcon = true;
        public static bool neverUseTackIcon = false;
        public bool readonlyMode = false;
        public static bool disableAllReadonlyMode = false;

        [SerializeField] private ReadmeSettings activeSettings;
        [SerializeField] private List<ReadmeSettings> allSettings = new List<ReadmeSettings> {};
        private bool settingsLoaded = false;
        private string fileNameLoaded = "";
        
        private static List<string> supportedTags = new List<string>() {"b", "i", "color", "size"};

        private string previousRichText = "";
        private string text = "";
        [SerializeField] private ReadmeData readmeData = new ReadmeData();
        private string lastSavedFileName = "";

        private bool managerConnected = false; //This should never be serialized

        public void Initialize()
        {
            if (!initialized)
            {
                initialized = true;

                fontColor = EditorGUIUtility.isProSkin ? darkFontColor : lightFontColor;
            }
        }
        
        public void ConnectManager()
        {
            bool isPrefab = gameObject != null && (gameObject.scene.name == null || gameObject.gameObject != null && gameObject.gameObject.scene.name == null);

            if (isPrefab)
            {
                //do nothing
            }
            
            if (!managerConnected)
            {
                managerConnected = true;
                ReadmeManager.AddReadme(this);
                ObjectIdPairs = ReadmeManager.ObjectIdPairs;
            }
        }
    
        public string RichText
        {
            get { return readmeData.richText; }
            set
            {
                previousRichText = readmeData.richText;
                
                if (value == null)
                {
                    readmeData.richText = "";
                }
                else if (value != readmeData.richText)
                {
//                    readmeData.richText = RemoveEmptyTags(value);
                    readmeData.richText = value;
                }
                
                text = MakePoorText(readmeData.richText);
                
                BuildRichTextTagMap();
                RebuildStyleMaps();
            }
        }
    
        public string HtmlText
        {
            get
            {
                string htmlText = readmeData.richText;
                
                //Line Returns
                htmlText = htmlText.Replace("\n", "<br>");

                //Font Size
                htmlText = Regex.Replace(htmlText, "<size=([0-9]*)>", "<span style=\"font-size:$1px>");
                htmlText = htmlText.Replace("</size>", "</span>");
                
                //Color
                htmlText = Regex.Replace(htmlText, "<color=\"#([0-9A-Fa-f]*)\">", "<span style=\"color:#$1>");
                htmlText = htmlText.Replace("</size>", "</span>");
                
                //Object
                ReadmeManager.RebuildObjectPairList();
                ObjectIdPairs.ForEach(pair =>
                {
                    string replacement = ReadmeManager.GetObjectString(pair.ObjectRef);
                    htmlText = Regex.Replace(htmlText, "<o=\"[0]*" + pair.Id + "\">", replacement);
                    htmlText = htmlText.Replace("</o>", "");
                    Debug.Log("pair: " + pair.Id);
                });

                if (String.IsNullOrEmpty(htmlText))
                {
                    htmlText = "<p></p>";
                }
                
                return htmlText;
            }
        }
    
        public List<ObjectIdPair> ObjectIdPairs
        {
            get
            {
                if (readmeData.objectIdPairs == null)
                {
                    ConnectManager();
                }
                
                return readmeData.objectIdPairs;
            }
            private set { readmeData.objectIdPairs = value; }
        }

        public string PreviousRichText
        {
            get { return previousRichText; }
        }
        
        public TextAreaObjectField[] TextAreaObjectFields
        {
            get { return readmeData.textAreaObjectFields; }
            set { readmeData.textAreaObjectFields = value; }
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

        public ReadmeSettings ActiveSettings
        {
            get { return activeSettings; }
        }

        public void UpdateSettings(string directory, bool force = false, bool verbose = false)
        {
            if (!settingsLoaded || force)
            {
                allSettings.Clear();
                bool settingsFound = false;
                
                DirectoryInfo directoryInfo = new DirectoryInfo(directory);
                FileInfo[] fileInfos = directoryInfo.GetFiles();
                foreach (FileInfo fileInfo in fileInfos)
                {
                    if (fileInfo.Extension == "." + ReadmeSettings.DEFAULT_TYPE && 
                        fileInfo.Name.Substring(0, ReadmeSettings.FILE_TAG.Length) == ReadmeSettings.FILE_TAG)
                    {
                        allSettings.Add(ReadmeSettings.LoadSettings(directory, fileInfo.Name));
                        settingsFound = true;
                    }
                }

                if (settingsFound)
                {
                    activeSettings = allSettings.OrderBy(setting => setting.priority).FirstOrDefault();
                    settingsLoaded = true;

                    if (!readonlyMode && ActiveSettings.redistributable)
                    {
                        readonlyMode = true;
                    }

                    if (verbose)
                    {
                        Debug.Log("Settings loaded: " + activeSettings.name);
                    }
                }
            }
        }
        
        public string RemoveEmptyTags(string input)
        {
            string output = input
                .Replace("<b></b>", "")
                .Replace("<i></i>", "");

            return output;
        }
    
        public static string MakePoorText(string richText)
        {
            return richText
                .Replace("<b>", "")
                .Replace("</b>", "")
                .Replace("<i>", "")
                .Replace("</i>", "");
            
            //  <color=#00ffffff>
            //  <size=20>
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
                Debug.LogError("README: An exception occured. Attempting to load last autosave.");
                LoadLastSave();
            }
        }

        public void ApplyStyleMap(string tag)
        {
            List<bool> styleMapCopy = StyleMaps[tag];
            
            RichText = RichText
                .Replace(Tag(tag), "")
                .Replace(EndTag(tag), "");
    
            // Iterate backwards through style map so we don't have to rebuild richTextTagMap every time
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
            List<string> tagPatterns = new List<string>
            {
                "<b>",
                "</b>",
                "<i>",
                "</i>",
                "<color=\"?[#,0-9,A-F,a-f]*\"?>", 
                "</color>",
                "<size=\"?[0-9]*\"?>", 
                "</size>",
                "<o=\"[-,a-zA-Z0-9]*\">", 
                "</o> "
            };
            
            richTextTagMap = Enumerable.Repeat(false, RichText.Length).ToList();

            foreach (string tagPattern in tagPatterns)
            {
                foreach (Match match in Regex.Matches(RichText, tagPattern, RegexOptions.None))
                {
                    richTextTagMap.RemoveRange(match.Index, match.Length);
                    richTextTagMap.InsertRange(match.Index, Enumerable.Repeat(true, match.Length).ToList());
                }
            }
        }
        
        public void RebuildStyleMaps()
        {
            if (text.Length == 0)
            {
                Dictionary<string, List<bool>> updatedStyleMaps = new Dictionary<string, List<bool>>();

                foreach (string tag in supportedTags)
                {
                    List<bool> styleMap = Enumerable.Repeat(false, text.Length + 1).ToList();

                    updatedStyleMaps[tag] = styleMap;
                }
            }
            else
            {
                Dictionary<string, List<bool>> updatedStyleMaps = new Dictionary<string, List<bool>>();

                foreach (string tag in supportedTags)
                {
                    List<bool> styleMap = Enumerable.Repeat(false, text.Length + 1).ToList();

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
        }
        
        //TODO can optimize by building a rich to poor index map.
        public int GetPoorIndex(int richIndex)
        {
            if (richIndex > RichText.Length) { richIndex = RichText.Length; }
            
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
                if (i < richTextTagMap.Count && richTextTagMap[i])
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

#endif