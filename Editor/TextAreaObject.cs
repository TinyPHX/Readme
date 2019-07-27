using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

public class TextAreaObjectField
{
    private Rect fieldRect;
    private int objectId;
    private int index;
    private int length;
        
    private static readonly Color textBoxBackgroundColor = new Color(211f / 255, 211f / 255, 211f / 255);
    private static readonly Color selectedColor = new Color(0f / 255, 130f / 255, 255f / 255, .6f);

    public TextAreaObjectField(Rect fieldRect, int objectId, int index, int length)
    {
        this.fieldRect = fieldRect;
        this.objectId = objectId;
        this.index = index;
        this.length = length;
    }

    public void Draw(TextEditor textEditor = null)
    {
        EditorGUI.DrawRect(FieldRect, textBoxBackgroundColor);
        Object obj = EditorUtility.InstanceIDToObject(objectId);
        obj = EditorGUI.ObjectField(FieldRect, obj, typeof(Object), true);
        if (obj != null)
        {
            objectId = obj.GetInstanceID();
        }
        else
        {
            objectId = 0;
        }

        if (textEditor != null && IsSelected(textEditor))
        {
            EditorGUI.DrawRect(FieldRect, selectedColor);
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

    public int ObjectId { get { return objectId; } }
    public int Index { get { return index; } }

    public Rect FieldRect
    {
        get { return fieldRect; }
    }
}