#if UNITY_EDITOR

using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace TP.Readme
{
    [Serializable]
    public class TextAreaObjectField
    {
        [SerializeField] private string name;
        [SerializeField] private int objectId;
        [SerializeField] private Object objectRef;
        [SerializeField] private Rect fieldRect;
        [SerializeField] private int index;
        [SerializeField] private int length;
        
        private static Color textBoxBackgroundColor;
        private static readonly Color selectedColor = new Color(0f / 255, 130f / 255, 255f / 255, .6f);

        public TextAreaObjectField(Rect fieldRect, int objectId, int index, int length)
        {
            this.fieldRect = fieldRect;
            this.index = index;
            this.length = length;
            
            ObjectId = objectId;
            ObjectRef = GetObjectFromId();
            
            name = (ObjectRef ? ObjectRef.name : "null") + " (" + ObjectId + ")";
        }

        public int GetIdFromObject()
        {
            return ReadmeManager.GetIdFromObject(ObjectRef);
        }

        public Object GetObjectFromId(bool autoSync = true)
        {
            return ReadmeManager.GetObjectFromId(ObjectId, autoSync);
        }

        public override bool Equals(object other)
        {
            TextAreaObjectField otherTextAreaObject = other as TextAreaObjectField;

            return this.fieldRect == otherTextAreaObject.fieldRect &&
                   this.index == otherTextAreaObject.index &&
                   this.length == otherTextAreaObject.length &&
                   this.objectId == otherTextAreaObject.ObjectId &&
                   this.objectRef == otherTextAreaObject.ObjectRef;
        }

        public void Draw(TextEditor textEditor = null, int yOffset = 0)
        {
            Rect fieldBounds = FieldRect;
            if (yOffset != 0)
            {
                fieldBounds.y += yOffset;
            }

            Rect rectBounds = fieldBounds;
            rectBounds.y += 1;
            rectBounds.height -= 1;

            textBoxBackgroundColor = EditorGUIUtility.isProSkin ? Readme.darkBackgroundColor : Readme.lightBackgroundColor;

            EditorGUI.DrawRect(rectBounds, textBoxBackgroundColor);
            Object obj = EditorGUI.ObjectField(fieldBounds, ObjectRef, typeof(Object), true);
            
            if (IdInSync && ObjectRef != obj)
            {
                ObjectRef = obj;
                UpdateId();
            }

            if (textEditor != null && IsSelected(textEditor))
            {
                EditorGUI.DrawRect(rectBounds, selectedColor);
            }
        }

        public bool IsSelected(TextEditor textEditor)
        {
            bool isSelected = false;

            if (Mathf.Min(textEditor.selectIndex, textEditor.cursorIndex) <= index &&
                Mathf.Max(textEditor.selectIndex, textEditor.cursorIndex) >= (index + length))
            {
                isSelected = true;
            }

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

        public int Index
        {
            get { return index; }
        }
    }
}

#endif