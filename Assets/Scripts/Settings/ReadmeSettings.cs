#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TP.Readme
{
    [Serializable]
    public struct ReadmeSettings
    {
        public string name;
        public string id;
        public string path;
        public string fileName;
        public bool redistributable;
        public bool lite;
        public float priority;
        public string type;
        
        public static string FILE_TAG = "Settings_";
        public static string DEFAULT_TYPE = "json";

        public ReadmeSettings(string path, string fileName = "New", bool redistributable = true, bool lite = true, int priority = 1000)
        {
            name = fileName;
            id = Guid.NewGuid().ToString();
            this.path = path;
            this.fileName = fileName;
            this.redistributable = redistributable;
            this.lite = lite;
            this.priority = priority;
            type = DEFAULT_TYPE;
        }

        private string FilePath
        {
            get { return path + "/" + FILE_TAG + fileName + "." + type; }
        }
        
        public void SaveSettings()
        {
            string jsonReadmeSettingsData = JsonUtility.ToJson(this, true);
            File.WriteAllText (FilePath, jsonReadmeSettingsData);
            Debug.Log("Settings saved to file: " + FilePath);
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
    }
}
#endif