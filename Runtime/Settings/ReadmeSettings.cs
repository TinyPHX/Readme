#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

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
        public string lastPopupDate
        {
            get => LastPopupDate.Instance.value;
            set => LastPopupDate.Instance.value = value;
        }

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
        }

        private string FileName => FILE_TAG + fileName + "." + type;
        private string FilePath => Path.Combine(Readme.SettingsLocation, FileName);
        private static string PersistentFileName => FILE_TAG + "PopUpDate" + "." + DEFAULT_TYPE;
        private static string PersistentFilePath => Path.Combine(Readme.PersistentLocation, PersistentFileName);

        public bool FileExists()
        {
            return File.Exists(FilePath);
        }

        public static bool PersistantFileExists()
        {
            return File.Exists(PersistentFilePath);
        }

        public static void CreateDefaultSettings()
        {
            ReadmeSettings defaultSettings = new ReadmeSettings("Default", false, true, 7);
            if (!defaultSettings.FileExists() || !PersistantFileExists())
            {
                defaultSettings.SaveSettings();
            }
        }

        public void SaveSettings()
        {
            string jsonReadmeSettingsData = JsonUtility.ToJson(this, true);
            File.WriteAllText (FilePath, jsonReadmeSettingsData);
            Debug.Log("Settings saved to file: " + FilePath);
            
            string jsonLastPupUpDateData = JsonUtility.ToJson(LastPopupDate.Instance, true);
            File.WriteAllText (PersistentFilePath, jsonLastPupUpDateData);
            Debug.Log("Persistent settings saved to file: " + PersistentFilePath);
        }

        public static List<ReadmeSettings> LoadAllSettings()
        {
            CreateDefaultSettings();

            List<ReadmeSettings> allSettings = new List<ReadmeSettings>() { };
            
            string tmpPopupDate = LoadLastPopUpDate().value;
            
            DirectoryInfo directoryInfo = new DirectoryInfo(Readme.SettingsLocation);
            FileInfo[] fileInfos = directoryInfo.GetFiles();
            foreach (FileInfo fileInfo in fileInfos)
            {
                if (fileInfo.Extension == "." + DEFAULT_TYPE && fileInfo.Name.Substring(0, FILE_TAG.Length) == FILE_TAG)
                {
                    ReadmeSettings loadedSettings = LoadSettings(fileInfo.Name);
                    loadedSettings.lastPopupDate = tmpPopupDate;
                    allSettings.Add(loadedSettings);
                }
            }

            return allSettings;
        }

        public static void MoveSettings(string unityPath, string fileName)
        {
            string unityFilePath = Path.GetFullPath(Path.Combine(unityPath, fileName));
            string userFilePath = Path.Combine(Readme.SettingsLocation, fileName);
            
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
            
            string filePath = Path.Combine(Readme.SettingsLocation, fileName);
            
            if (File.Exists(filePath))
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

        public static LastPopupDate LoadLastPopUpDate()
        {
            LastPopupDate loadedPopupDate = new LastPopupDate();
            if (File.Exists(PersistentFilePath))
            {
                string json = File.ReadAllText(PersistentFilePath);
                loadedPopupDate = JsonUtility.FromJson<LastPopupDate>(json);
            }
            else
            {
                Debug.LogWarning("Settings file not found.");
            }

            return loadedPopupDate;
        }
    }
    
    [Serializable]
    public class LastPopupDate
    {
        [SerializeField] public string value = DateTime.Parse("1/1/2000 12:00:00 AM").ToString(CultureInfo.InvariantCulture);

        private static LastPopupDate instance;
        public static LastPopupDate Instance {
            get
            {
                if (instance == null)
                {
                    instance = new LastPopupDate();
                }

                return instance;
            }
        }
    }
}
#endif