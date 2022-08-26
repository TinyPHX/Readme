#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TP
{
    [Serializable]
    public struct ReadmeSettings
    {
        public string name;
        public string id;
        public string skin;
        public string path;
        public string fileName;
        public bool redistributable;
        public bool lite;
        public float priority;
        public string type;
        public string lastPopupDate;
        
        public static string FILE_TAG = "Settings_";
        public static string DEFAULT_TYPE = "json";

        public ReadmeSettings(string path, string fileName = "New", bool redistributable = true, bool lite = true, int priority = 1000, string skin = "Default")
        {
            name = fileName;
            id = Guid.NewGuid().ToString();
            this.skin = skin;
            this.path = path;
            this.fileName = fileName;
            this.redistributable = redistributable;
            this.lite = lite;
            this.priority = priority;
            type = DEFAULT_TYPE;
            lastPopupDate = DateTime.Parse("1/1/2000 12:00:00 AM").ToString();
        }

        private string FileName => FILE_TAG + fileName + "." + type;
        private string FilePath => path + "/" + FileName;

        public void SaveSettings(string path)
        {
            // string this.path +=  "/" + FILE_TAG + fileName + "." + type;
            string filePath = Path.Combine(path, FileName);
            
            string jsonReadmeSettingsData = JsonUtility.ToJson(this, true);
            File.WriteAllText (filePath, jsonReadmeSettingsData);
            Debug.Log("Settings saved to file: " + filePath);
        }

        public static ReadmeSettings LoadSettings(string path, string fileName)
        {
            ReadmeSettings loadedSettings = new ReadmeSettings();
            
            string file = fileName;
            string filePath = Path.Combine(path, file);
            if (File.Exists(Path.GetFullPath(filePath)))
            {
                string json = File.ReadAllText(filePath);
                loadedSettings = JsonUtility.FromJson<ReadmeSettings>(json);
            }
            else
            {
                Debug.LogWarning("Settings file not found.");
            }

            return loadedSettings;
        }
        
        public static string GetFolder(ScriptableObject script)
        {
            MonoScript monoScript = MonoScript.FromScriptableObject(script);
            string path = Path.GetDirectoryName(AssetDatabase.GetAssetPath(monoScript)) ?? "";
            path = Path.Combine(path, "..");
            path = Path.Combine(path, "Settings");
            path = path.Replace("\\Editor\\..", "");
            return path;
        }
        
        // public static string GetFolder(Object obj)
        // {
        //     // MonoScript monoScript = MonoScript.FromScriptableObject(script);
        //     string path = Path.GetDirectoryName(AssetDatabase.GetAssetPath(obj)) ?? "";
        //     path = Path.Combine(path, "..");
        //     path = Path.Combine(path, "Settings");
        //     path = path.Replace("\\Editor\\..", "");
        //     return path;
        // }
    }
}
#endif