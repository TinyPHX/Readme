#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TP.Readme
{
    
    [Serializable]
    public struct ObjectIdPair
    {
        [SerializeField] private string name;
        [SerializeField] private int id;
        [SerializeField] private Object objectRef;
        
        public ObjectIdPair(int id, Object objectRef) : this()
        {
            name = ReadmeManager.GetObjectString(objectRef) + " (" + id + ")";
            this.id = id;
            this.objectRef = objectRef;
        }

        public int Id
        {
            get { return id; }
        }

        public Object ObjectRef
        {
            get { return objectRef; }
        }
    }
    
    public class ReadmeManager : ISerializationCallbackReceiver
    {
        private static List<Readme> readmes = new List<Readme>();
        private static List<ObjectIdPair> objectIdPairs;
        private static Dictionary<int, Object> objectDict;
        private static Dictionary<Object, int> idDict;

        public static string GetObjectIdPairListString()
        {
            return string.Join("\n", ObjectIdPairs.OrderBy(x => x.Id).Select(x => x.Id + " = " + GetObjectString(x.ObjectRef)).ToArray());
        }

        public static string GetObjectString(Object obj)
        {
            string value = "None";

            if (obj != null)
            {
                value = obj.ToString();

                if (obj.GetType() == typeof(UnityEditor.MonoScript))
                {
                    UnityEditor.MonoScript monoScriptObject = obj as UnityEditor.MonoScript;
                    value = "(UnityEngine.MonoScript)";
                    if (monoScriptObject != null)
                    {
                        value = monoScriptObject.name + " " + value;
                    }
                }
                else if (value.Length > 40)
                {
                    value = value.Substring(0, 40).Replace("\n", " ") + "...";
                }
            }

            return value;
        }
        
        public static void AddReadme(Readme readme)
        {
            if (!readmes.Contains(readme))
            {
                readmes.Add(readme);
            }
        }

        public static List<ObjectIdPair> ObjectIdPairs
        {
            get
            {
                if (objectIdPairs == null)
                {
                    Readme readmeWithObjectIdPairs = readmes.FirstOrDefault(item => item != null && item.ObjectIdPairs != null);

                    if (readmeWithObjectIdPairs != null)
                    {
                        ObjectIdPairs = readmeWithObjectIdPairs.ObjectIdPairs;
                    }
                    else
                    {
                        ObjectIdPairs = new List<ObjectIdPair>();
                    }
                }

                return objectIdPairs;
            }
            private set
            {
                objectIdPairs = value;
                SyncDictsWithList();
            }
        }

        public static Dictionary<int, Object> ObjectDict
        {
            get
            {
                if (objectDict == null)
                {
                    SyncDictsWithList();
                }

                return objectDict;
            }
            set { objectDict = value; }
        }

        public static Dictionary<Object, int> IdDict
        {
            get 
            { 
                if (idDict == null)
                {
                    SyncDictsWithList();
                }
                return idDict; 
            }
            set { idDict = value; }
        }

        private static void SyncDictsWithList()
        {
            ObjectDict = new Dictionary<int, Object>();
            IdDict = new Dictionary<Object, int>();

            foreach (ObjectIdPair objectIdPair in ObjectIdPairs)
            {
                AddObjectIdPairToDicts(objectIdPair);
            }
        }

        public static void RebuildObjectPairList()
        {
            Clear();
            
            foreach (Readme readme in readmes)
            {
                foreach (TextAreaObjectField textAreaObjectField in readme.TextAreaObjectFields)
                {
                    AddObjectIdPair(textAreaObjectField.ObjectRef, textAreaObjectField.ObjectId);
                }
            }
        }
        

        private static void AddObjectIdPair(Object obj, int objId)
        {
            Object foundObject;
            int foundId;
            
            if (ObjectDict.TryGetValue(objId, out foundObject))
            {
                if (foundObject != obj)
                {
                    Debug.LogWarning("Duplicate ids detected. Object ignored: " + obj);
                }

                return;
            }
            
            if (obj != null && IdDict.TryGetValue(obj, out foundId))
            {
                if (foundId != objId)
                {
                    Debug.LogWarning("Duplicate objects detected. Object ignored: " + obj);
                }

                return;
            }

            ObjectIdPair objectIdPair = new ObjectIdPair(objId, obj);
            ObjectIdPairs.Add(objectIdPair);
            AddObjectIdPairToDicts(objectIdPair);
        }

        private static void AddObjectIdPairToDicts(ObjectIdPair objectIdPair)
        {
            if (!objectDict.ContainsKey(objectIdPair.Id))
            {
                ObjectDict.Add(objectIdPair.Id, objectIdPair.ObjectRef);
            }

            if (objectIdPair.ObjectRef != null && !IdDict.ContainsKey(objectIdPair.ObjectRef))
            {
                IdDict.Add(objectIdPair.ObjectRef, objectIdPair.Id);
            }
        }

        public static void Clear()
        {
            ObjectIdPairs.Clear();
            IdDict.Clear();
            ObjectDict.Clear();
        }
        
        public static Object GetObjectFromId(int id, bool autoSync = true)
        {
            Object objFound = null;
            bool found;
            if (id != 0)
            {
                found = ObjectDict.TryGetValue(id, out objFound);

                if (autoSync)
                {
                    if (!found)
                    {
                        SyncDictsWithList();
                        found = ObjectDict.TryGetValue(id, out objFound);
                    }

                    if (!found)
                    {
                        RebuildObjectPairList();
                        found = ObjectDict.TryGetValue(id, out objFound);
                    }

                    if (!found)
                    {
                        Debug.LogWarning("Problem finding object with id: " + id + ".");
                    }
                }
            }

            return objFound;
        }
        

//        private static bool IsPrefab(GameObject gameObject)
//        {
//            bool isPrefab = gameObject != null && (gameObject.scene.name == null || gameObject.gameObject != null && gameObject.gameObject.scene.name == null);
//
//            return isPrefab;
//        }
        
        public static int GetIdFromObject(Object obj)
        {
            if (obj == null)
            {
                Debug.LogWarning("Attempted to get id for null object.");
                return 0;
            }
            
            int objId;
            if (!IdDict.TryGetValue(obj, out objId))
            {
                objId = GenerateId();
            }

            AddObjectIdPair(obj, objId);

            return objId;
        }

        private static int GenerateId()
        {
            int largestId = 0;
            if (objectDict != null && objectDict.Count > 0)
            {
                largestId = objectDict.Keys.Max();
            }
            return largestId + 1;
        }

        public void OnBeforeSerialize()
        {
            objectIdPairs = null;
        }

        public void OnAfterDeserialize()
        {
            objectIdPairs = ObjectIdPairs;
        }
    }
}
#endif