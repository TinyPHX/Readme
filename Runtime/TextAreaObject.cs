#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace TP
{
    [Serializable]
    public class TextAreaObjectField
    {
        [SerializeField] private string name;
        [SerializeField] private int objectId;
        [SerializeField] private Object objectRef;
        [SerializeField] private Rect fieldRect;
        [SerializeField] private Rect absoluteFieldRect;
        [SerializeField] private int index;
        [SerializeField] private int length;

        private Action<TextAreaObjectField> OnChange;
        
        private static Color textBoxBackgroundColor;
        private static readonly Color selectedColor = new Color(0f / 255, 130f / 255, 255f / 255, .6f);

        public TextAreaObjectField(Rect fieldRect, int objectId, int index, int length, Action<TextAreaObjectField> onChangeCallback)
        {
            this.fieldRect = fieldRect;
            this.index = index;
            this.length = length;
            
            ObjectId = objectId;
            ObjectRef = GetObjectFromId();
            
            name = (ObjectRef ? ObjectRef.name : "null") + " (" + ObjectId + ")";

            OnChange = onChangeCallback;
        }

        public int GetIdFromObject()
        {
            return ReadmeManager.GetIdFromObject(ObjectRef);
        }

        public Object GetObjectFromId(bool autoSync = true)
        {
            return ReadmeManager.GetObjectFromId(ObjectId, autoSync);
        }

        public static bool AllFieldsEqual(IEnumerable<TextAreaObjectField> listA, IEnumerable<TextAreaObjectField> listB)
        {
            return listA.OrderBy(item => item.Index).SequenceEqual(listB.OrderBy(item => item.Index), new AllFieldsEqualComparer());
        }

        public static bool BaseFieldsEqual(IEnumerable<TextAreaObjectField> listA, IEnumerable<TextAreaObjectField> listB)
        {
            return listA.OrderBy(item => item.Index).SequenceEqual(listB.OrderBy(item => item.Index), new BaseFieldsEqualComparer());
        }
        
        private class AllFieldsEqualComparer : IEqualityComparer<TextAreaObjectField>
        {
            public bool Equals(TextAreaObjectField a, TextAreaObjectField b)
            {
                if (a == null || b == null) { return a == null && b == null; }
                return a.fieldRect == b.fieldRect && a.index == b.index && a.length == b.length && a.objectId == b.ObjectId && a.objectRef == b.ObjectRef;
            }
            
            public int GetHashCode(TextAreaObjectField a)
            {
                return a.fieldRect.GetHashCode() ^ a.index.GetHashCode() ^ a.length.GetHashCode() ^ a.objectId.GetHashCode() ^ a.objectRef.GetHashCode();
            }
        }
        
        private class BaseFieldsEqualComparer : IEqualityComparer<TextAreaObjectField>
        {
            public bool Equals(TextAreaObjectField a, TextAreaObjectField b)
            {
                if (a == null || b == null) { return a == null && b == null; }
                return a.index == b.index && a.length == b.length && a.objectId == b.ObjectId && a.objectRef == b.ObjectRef;
            }
            
            public int GetHashCode(TextAreaObjectField a)
            {
                return a.index.GetHashCode() ^ a.length.GetHashCode() ^ a.objectId.GetHashCode() ^ a.objectRef.GetHashCode();
            }
        }

        public void Draw(TextEditor textEditor = null, Vector2 offset = default, Rect bounds = default)
        {
            absoluteFieldRect = FieldRect;
            absoluteFieldRect.position += offset;

            textBoxBackgroundColor = EditorGUIUtility.isProSkin ? Readme.darkBackgroundColor : Readme.lightBackgroundColor;
            
            // Only draw if in bounds
            if (bounds != default)
            {
                absoluteFieldRect.yMin += Mathf.Min(Mathf.Max(bounds.yMin - absoluteFieldRect.yMin, 0), absoluteFieldRect.height);
                absoluteFieldRect.yMax -= Mathf.Min(Mathf.Max(absoluteFieldRect.yMax - bounds.yMax, 0), absoluteFieldRect.height);
                if (absoluteFieldRect.height <= 0)
                {
                    Rect offscreen = new Rect(99999, 99999, 0, 0);
                    absoluteFieldRect = offscreen;
                }
            }
            
            EditorGUI.DrawRect(absoluteFieldRect, textBoxBackgroundColor);
            Object obj = EditorGUI.ObjectField(absoluteFieldRect, ObjectRef, typeof(Object), true);

            if (IdInSync && ObjectRef != obj)
            {
                ObjectRef = obj;
                UpdateId();
                OnChange(this);
            }

            if (textEditor != null && IsSelected(textEditor))
            {
                EditorGUI.DrawRect(absoluteFieldRect, selectedColor);
            }
        }

        public bool IsSelected(TextEditor textEditor)
        {
            bool isSelected = 
                textEditor.controlID != 0 &&
                Mathf.Min(textEditor.selectIndex, textEditor.cursorIndex) <= index &&
                Mathf.Max(textEditor.selectIndex, textEditor.cursorIndex) >= (index + length);

            return isSelected;
        }
        
        public void UpdateId()
        {
            ObjectId = ObjectRef == null ? 0 : GetIdFromObject();
        }

        public bool IdInSync
        {
            get { return (ObjectId == 0 && ObjectRef == null) || GetObjectFromId(false) == ObjectRef; }
        }
        
        public int ObjectId
        {
            get { return objectId; }
            private set { objectId = value; }
        }

        public Object ObjectRef
        {
            get { return objectRef; }
            private set { objectRef = value; }
        }

        public Rect FieldRect
        {
            get { return fieldRect; }
        }

        public Rect AbsoluteFieldRect
        {
            get { return absoluteFieldRect; }
        }

        public int Index
        {
            get { return index; }
        }

        public int IdIndex
        {
            get { return index+4; } // +4 for the characters <o="
        }
    }
}

#endif