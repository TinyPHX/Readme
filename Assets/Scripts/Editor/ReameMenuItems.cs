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

namespace TP.Readme
{
    public static class ReameMenuItems
    {
        [MenuItem("Assets/Create/Readme", false, 100)]
        public static void CreateReadmePrefab()
        {
            var path = "";
            var obj = Selection.activeObject;
            
            if (obj == null)
            {
                path = "Assets";
            }
            else
            {
                path = AssetDatabase.GetAssetPath(obj.GetInstanceID());
            }
            
            EditorApplication.ExecuteMenuItem("GameObject/Create Empty");
            GameObject tempReadmeGameObject = Selection.activeGameObject;
            if (tempReadmeGameObject)
            {
                tempReadmeGameObject.AddComponent<Readme>();
                tempReadmeGameObject.name = "Readme";
            }
            
            string absolutePath = EditorUtility.SaveFilePanel(
                "Save Readme",
                path,
                 "Readme.prefab",
                "prefab");
            
            PrefabUtility.CreatePrefab(AbsolutePathToRelative(absolutePath), tempReadmeGameObject);
            #if UNITY_EDITOR
                GameObject.DestroyImmediate(tempReadmeGameObject);
            #else
                GameObject.Destroy(tempReadmeGameObject);
            #endif

        }

        [MenuItem("GameObject/Readme", false, 20)]
        public static void CreateReadmeGameObject()
        {
            EditorApplication.ExecuteMenuItem("GameObject/Create Empty");
            GameObject gameObject = Selection.activeGameObject;
            if (gameObject)
            {
                gameObject.AddComponent<Readme>();
                gameObject.name = "Readme";
            }
        }

        private static string AbsolutePathToRelative(string absolutePath)
        {
            string relativePath = "Assets" + absolutePath.Substring(Application.dataPath.Length);

            return relativePath;
        }
    }
}