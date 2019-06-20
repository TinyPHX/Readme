using System;
using System.IO;
using UnityEngine;

namespace TP.Readme {
    [Serializable]
    public class ReadmeData
    {
        public string text;
    }
    
    [ExecuteInEditMode]
    public class Readme : MonoBehaviour
    {   
        [SerializeField] private ReadmeData readmeData;
        private string lastSavedFileName = "";
        public Color fontColor = Color.black;
    
        public string Text
        {
            get { return readmeData.text; }
            set
            {
                if (value == null)
                {
                    readmeData.text = "";
                }
                else
                {
                    readmeData.text = value;
                }
            }
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
                Text = JsonUtility.FromJson<ReadmeData>(json).text;
            }
        }
    }
}