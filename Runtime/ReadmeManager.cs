#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace TP
{
    [Serializable]
    public struct ObjectIdPair
    {
        [field: SerializeField] public string Name { get; }
        [field: SerializeField] public int Id { get; }
        [field: SerializeField] public Object ObjectRef { get; }

        public ObjectIdPair(int id, Object objectRef) : this()
        {
            Name = ReadmeManager.GetObjectString(objectRef) + " (" + id + ")";
            Id = id;
            ObjectRef = objectRef;
        }
    }
    
    public class ReadmeManager : ISerializationCallbackReceiver
    {
        private static List<Readme> readmes = new List<Readme>();
        private static List<ObjectIdPair> objectIdPairs;
        private static Dictionary<int, Object> objectDict;
        private static Dictionary<Object, int> idDict;
        private static List<int> missingIds;
        
        private static ReadmeManager instance;
        private static ReadmeManager Initialize()
        {
            if (instance == null)
            {
                instance = new ReadmeManager();
            }

            return instance;
        }

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
            Initialize();
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

        private static List<int> MissingIds
        {
            get 
            { 
                if (missingIds == null)
                {
                    missingIds = new List<int>();
                }
                return missingIds; 
            }
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
        
        public static void AddObjectIdPair(Object obj, int objId)
        {
            if (ObjectDict.TryGetValue(objId, out Object foundObject))
            {
                if (foundObject == null && obj != null)
                {
                    RemoveObjectIdPair(foundObject, objId); // Basically we'll replace it with what we have.
                }
                else 
                {
                    if (foundObject != obj)
                    {
                        Debug.LogWarning("Duplicate ids detected. Object ignored: " + obj);
                    }
                    
                    return;
                }
            }
            
            if (obj != null && IdDict.TryGetValue(obj, out int foundId))
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
            
            // Debug.Log("Object Id (" + objectIdPair + ") Pair Added." + 
            //           " ObjectIdPairs.Count=" + ObjectIdPairs.Count + 
            //           " IdDict.Count=" + IdDict.Count + 
            //           " ObjectDict.Count=" + ObjectDict.Count);
        }

        private static void RemoveObjectIdPair(Object obj, int objId)
        {
            ObjectIdPair toRemove = new ObjectIdPair(objId, obj);
            ObjectIdPairs.RemoveAll(item => item.ObjectRef == toRemove.ObjectRef && item.Id == toRemove.Id);
            RemoveObjectIdPairFromDicts(toRemove);
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

        private static void RemoveObjectIdPairFromDicts(ObjectIdPair objectIdPair)
        {
            objectDict.Remove(objectIdPair.Id);

            if (objectIdPair.ObjectRef != null)
            {
                IdDict.Remove(objectIdPair.ObjectRef);
            }
        }


        public static void Clear()
        {
            ObjectIdPairs.Clear();
            IdDict.Clear();
            ObjectDict.Clear();

            // Debug.Log("Object Id Pair Cleared." + 
            //           " ObjectIdPairs.Count=" + ObjectIdPairs.Count + 
            //           " IdDict.Count=" + IdDict.Count + 
            //           " ObjectDict.Count=" + ObjectDict.Count);
        }
        
        public static Object GetObjectFromId(int id, bool autoSync = true)
        {
            bool found = ObjectDict.TryGetValue(id, out var objFound);
            bool missing = MissingIds.Contains(id);
            
            if (found && missing)
            {
                missing = false;
                missingIds.Remove(id);
            }
            
            if (id != 0 && !missing)
            {
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
                        missingIds.Add(id);
                        Debug.LogWarning("Problem finding object with id: " + id + ".");
                    }
                }
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

        // Could optimize by using GUIDs. Then we wouldn't need to iterate keys. 
        private static int GenerateId()
        {
            //int largestId = 0;
            //if (objectDict != null && objectDict.Count > 0)
            //{
            //    largestId = objectDict.Keys.Max();
            //}
            //return largestId + 1;
            return Random.Range(1, 9999999);
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