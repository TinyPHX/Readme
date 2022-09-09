using System.IO;
using UnityEngine;
using UnityEditor;
using TheArtOfDev.HtmlRenderer.PdfSharp;
using PdfSharp;
using PdfSharp.Pdf;

// using html to pdf libraries from: 
// - https://www.nuget.org/packages/PdfSharp/1.50.5147
// - https://github.com/ArthurHub/HTML-Renderer
//    - https://www.nuget.org/packages/HtmlRenderer.Core
//    - https://www.nuget.org/packages/HtmlRenderer.PdfSharp 

namespace TP
{
    public static class ReadmeMenuItems
    {
        [MenuItem("Assets/Create/Readme", false, 100)]
        public static void CreateReadmePrefab()
        {
            var path = Selection.activeObject == null ? "Assets" : AssetDatabase.GetAssetPath(Selection.activeObject.GetInstanceID());
            
            string absolutePath = EditorUtility.SaveFilePanel(
                "Save Readme",
                path,
                 "README",
                "prefab");

            if (absolutePath != "")
            {
                EditorApplication.ExecuteMenuItem("GameObject/Create Empty");
                
                GameObject tempReadmeGameObject = Selection.activeGameObject;
                if (tempReadmeGameObject)
                {
                    tempReadmeGameObject.AddComponent<Readme>();
                    tempReadmeGameObject.name = "Readme";
                }
                
                PrefabUtility.SaveAsPrefabAsset(tempReadmeGameObject, AbsolutePathToRelative(absolutePath));
                
                #if UNITY_EDITOR
                    GameObject.DestroyImmediate(tempReadmeGameObject);
                #else
                    GameObject.Destroy(tempReadmeGameObject);
                #endif
            }
        }
        
        [MenuItem("CONTEXT/Readme/Readme: Copy Plain Text", false, 200)]
        static void CopyPlainText()
        {
            ReadmeEditor.ActiveReadmeEditor.SelectAll();
            ReadmeEditor.ActiveReadmeEditor.CopyPlainText();
        }
        
        [MenuItem("CONTEXT/Readme/Readme: Copy Rich Text", false, 201)]
        static void CopyRichText()
        {
            ReadmeEditor.ActiveReadmeEditor.SelectAll();
            ReadmeEditor.ActiveReadmeEditor.CopyRichText();
        }
        
        [MenuItem("CONTEXT/Readme/Readme: Toggle Edit", false, 202)]
        static void ToggleEdit()
        {
            ReadmeEditor.ActiveReadmeEditor.ToggleEdit();
        }
        
        [MenuItem("CONTEXT/Readme/Readme: Toggle Read Only", false, 203)]
        static void ToggleReadOnly()
        {
            ReadmeEditor.ActiveReadmeEditor.ToggleReadOnly();
        }
        
        [MenuItem("CONTEXT/Readme/Readme: Toggle Scroll", false, 203)]
        static void ToggleScroll()
        {
            ReadmeEditor.ActiveReadmeEditor.ToggleScroll();
        }

        [MenuItem("GameObject/Readme", false, 20)]
        public static void CreateReadmeGameObject()
        {
            EditorApplication.ExecuteMenuItem("GameObject/Create Empty");
            if (Selection.activeGameObject)
            {
                Selection.activeGameObject.AddComponent<Readme>();
                Selection.activeGameObject.name = "Readme";
            }
        }

        private static string AbsolutePathToRelative(string absolutePath)
        {
            string relativePath = "Assets" + absolutePath.Substring(Application.dataPath.Length);

            return relativePath;
        }
    }
}