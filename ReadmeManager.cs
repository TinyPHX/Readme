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
            return string.Join("\n", ObjectIdPairs.Select(x => x.Id + " = " + GetObjectString(x.ObjectRef)).ToArray());
        }

        public static string GetObjectString(Object obj)
        {
            string value = obj.ToString();
            
            if (obj.GetType() == typeof(UnityEditor.MonoScript))
            {
                UnityEditor.MonoScript monoScriptObject = obj as UnityEditor.MonoScript;
                value = "(UnityEngine.MonoScript)";
                if (monoScriptObject != null)
                {
                    value = monoScriptObject.name + " " + value;
                }
            }
            else if (value.Length > 100)
            {
                value = value.Substring(0, 100);
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

        private static void AddObjectIdPair(Object obj, int objId)
        {
            Object foundObject;
            
            if (ObjectDict.TryGetValue(objId, out foundObject))
            {
                if (foundObject != obj)
                {
                    Debug.LogWarning("Duplicate object key detected. Object ignored: " + obj);
                }

                return;
            }


            if (obj == null)
            {
                Debug.LogWarning("Cannot add null object to objectIdPairs.");
                return;
            }
            
            ObjectIdPair objectIdPair = new ObjectIdPair(objId, obj);
            ObjectIdPairs.Add(objectIdPair);
            AddObjectIdPairToDicts(objectIdPair);
        }

        private static void AddObjectIdPairToDicts(ObjectIdPair objectIdPair)
        {
            ObjectDict.Add(objectIdPair.Id, objectIdPair.ObjectRef);
            IdDict.Add(objectIdPair.ObjectRef, objectIdPair.Id);
        }

        public static void Clear()
        {
            ObjectIdPairs.Clear();
            IdDict.Clear();
            ObjectDict.Clear();
        }
        
        public static Object GetObjectFromId(int id)
        {
            Object objFound = null;
            if (!ObjectDict.TryGetValue(id, out objFound))
            {
                Debug.LogWarning("Problem finding object in dictionary");
            }

            return objFound;
        }
        
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
            return ObjectIdPairs.Count + 1;
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