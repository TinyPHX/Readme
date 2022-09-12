using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TP
{
    public static class ReadmeUtil
    {
        #region Gui Skins
        
        public static readonly string SKIN_STYLE = "Style";
        public static readonly string SKIN_SOURCE = "Source";

        public static GUISkin GetSkin(string fileName, ScriptableObject script)
        {
            string GetSkinsPath()
            {
                MonoScript monoScript = MonoScript.FromScriptableObject(script);
                string skinsPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(monoScript)) ?? "";
                skinsPath = Path.Combine(skinsPath, "..", "Runtime", "Skins");
                return skinsPath;
            }
            
            string path = GetSkinsPath();
            GUISkin guiSkin = default;
            
            string file = fileName + ".guiskin";
            string filePath = Path.Combine(path, file);
            if (File.Exists(Path.GetFullPath(filePath)))
            {
                guiSkin = (GUISkin)AssetDatabase.LoadAssetAtPath(filePath, typeof(GUISkin));
            }
            else
            {
                Debug.LogWarning("GetSkin file not found.");
            }

            return guiSkin;
        }
        
        #endregion
        
        public static bool UnityInFocus => UnityEditorInternal.InternalEditorUtility.isApplicationActive;
        
        public static void FocusEditorWindow(string windowTitle)
        {
            EditorWindow inspectorWindow = GetEditorWindow(windowTitle);
            if (inspectorWindow != default(EditorWindow))
            {
                inspectorWindow.Focus();
            }

            EditorWindow GetEditorWindow(string editorWindowTitle)
            {
                EditorWindow[] allWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();
                EditorWindow editorWindow = allWindows.SingleOrDefault(window => window.titleContent.text == editorWindowTitle);

                return editorWindow;
            }
        }
        
        public static string GetFixedLengthId(string id, int length = 7)
        {
            string fixedLengthId = id;
            bool isNegative = id[0] == '-';
            string prepend = "";

            if (isNegative)
            {
                prepend = "-";
                fixedLengthId = id.Substring(1, id.Length - 1);
            }

            while (fixedLengthId.Length + prepend.Length < length)
            {
                fixedLengthId = "0" + fixedLengthId;
            }

            fixedLengthId = prepend + fixedLengthId;

            return fixedLengthId;
        }

        public static Rect GetLastRect(Rect defaultRect = default, Vector2 offset = default)
        {
            Rect lastRect = defaultRect;
                
            if (Event.current.type == EventType.Repaint) // GetLastRect returns dummy values except on repaint. 
            {
                lastRect = new Rect(GUILayoutUtility.GetLastRect());
                lastRect.position += offset;
            }

            return lastRect;
        }
        
        // Replacement for null coalescing operator for older versions of unity.
        public delegate T GetObjectDelegate<out T>();
        public static T SetIfNull<T>(ref T obj, GetObjectDelegate<T> getNewValue)
        {
            if (obj == null) { obj = getNewValue(); }
            return obj;
        }
    }
}