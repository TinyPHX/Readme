#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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
        public string fileName;
        public bool redistributable;
        public bool lite;
        public float priority;
        public string type;
        public string lastPopupDate;
        
        public static string FILE_TAG = "Settings_";
        public static string DEFAULT_TYPE = "json";

        public ReadmeSettings(string fileName = "New", bool redistributable = true, bool lite = true, int priority = 1000, string skin = "Default")
        {
            name = fileName;
            id = Guid.NewGuid().ToString();
            this.skin = skin;
            this.fileName = fileName;
            this.redistributable = redistributable;
            this.lite = lite;
            this.priority = priority;
            type = DEFAULT_TYPE;
            lastPopupDate = DateTime.Parse("1/1/2000 12:00:00 AM").ToString();
        }

        private string FileName => FILE_TAG + fileName + "." + type;
        private string FilePath => Path.Combine(Readme.SaveLocation, FileName);

        public bool FileExists()
        {
            return File.Exists(FilePath);
        }

        public static void CreateDefaultSettings()
        {
            ReadmeSettings defaultSettings = new ReadmeSettings("Default", false, true, 7);
            if (!defaultSettings.FileExists())
            {
                defaultSettings.SaveSettings();
            }
        }

        public void SaveSettings()
        {
            string jsonReadmeSettingsData = JsonUtility.ToJson(this, true);
            File.WriteAllText (FilePath, jsonReadmeSettingsData);
            Debug.Log("Settings saved to file: " + FilePath);
        }

        public static List<ReadmeSettings> LoadAllSettings(string unityPath)
        {
            CreateDefaultSettings();

            List<ReadmeSettings> allSettings = new List<ReadmeSettings>() { };
            
            DirectoryInfo directoryInfo = new DirectoryInfo(unityPath);
            FileInfo[] fileInfos = directoryInfo.GetFiles();
            foreach (FileInfo fileInfo in fileInfos)
            {
                if (fileInfo.Extension == "." + DEFAULT_TYPE && fileInfo.Name.Substring(0, FILE_TAG.Length) == FILE_TAG)
                {
                    MoveSettings(unityPath, fileInfo.Name);
                }
            }
            
            directoryInfo = new DirectoryInfo(GetUserFolder());
            fileInfos = directoryInfo.GetFiles();
            foreach (FileInfo fileInfo in fileInfos)
            {
                if (fileInfo.Extension == "." + DEFAULT_TYPE && fileInfo.Name.Substring(0, FILE_TAG.Length) == FILE_TAG)
                {
                    allSettings.Add(LoadSettings(fileInfo.Name));
                }
            }

            return allSettings;
        }

        public static void MoveSettings(string unityPath, string fileName)
        {
            string unityFilePath = Path.GetFullPath(Path.Combine(unityPath, fileName));
            string userFilePath = Path.Combine(GetUserFolder(), fileName);
            
            if (File.Exists(unityFilePath))
            {
                string json = File.ReadAllText(unityFilePath);
                File.WriteAllText(userFilePath, json);
                
                if (File.Exists(userFilePath))
                {
                    File.Delete(unityFilePath);
                    if (File.Exists(unityFilePath + ".meta"))
                    {
                        File.Delete(unityFilePath + ".meta");
                    }
                }
            }
            
        }

        public static ReadmeSettings LoadSettings(string fileName)
        {
            ReadmeSettings loadedSettings = new ReadmeSettings();
            
            string userFilePath = Path.Combine(GetUserFolder(), fileName);
            
            if (File.Exists(userFilePath))
            {
                string json = File.ReadAllText(userFilePath);
                loadedSettings = JsonUtility.FromJson<ReadmeSettings>(json);
            }
            else
            {
                Debug.LogWarning("Settings file not found.");
            }

            return loadedSettings;
        }
        
        public static string GetUnityFolder(ScriptableObject script)
        {
            MonoScript monoScript = MonoScript.FromScriptableObject(script);
            string path = Path.GetDirectoryName(AssetDatabase.GetAssetPath(monoScript)) ?? "";
            path = Path.Combine(path, "..", "Runtime", "Settings");;
            return path;
        }

        public static string GetUserFolder()
        {
            return Readme.SaveLocation;
        }
    }
}
#endif