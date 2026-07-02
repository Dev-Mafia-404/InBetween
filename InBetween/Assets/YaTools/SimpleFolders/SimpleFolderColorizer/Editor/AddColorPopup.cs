using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace YaTools.SimpleFolders.SimpleFolderColorizer.Editor
{
    public class AddColorPopup : PopupWindowContent
    {
        private Color selectedColor = Color.white;
        private Action<Color> onConfirm;

        public AddColorPopup(Action<Color> onConfirm) => this.onConfirm = onConfirm;

        public override Vector2 GetWindowSize() => new Vector2(300, 130);

        public override VisualElement CreateGUI()
        {
            var root = new VisualElement();
            root.style.paddingTop = 14;
            root.style.paddingLeft = 14;
            root.style.paddingRight = 14;
            root.style.paddingBottom = 14;

            var label = new Label("New Preset Color");
            label.style.fontSize = 13;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 10;

            var colorField = new ColorField { value = selectedColor };
            colorField.showAlpha = false;
            colorField.style.height = 32;
            colorField.RegisterValueChangedCallback(e => selectedColor = e.newValue);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.FlexEnd;
            row.style.marginTop = 12;

            var btnCancel = new Button(() => editorWindow.Close()) { text = "Cancel" };
            btnCancel.style.height = 28;
            btnCancel.style.paddingLeft = 14;
            btnCancel.style.paddingRight = 14;
            btnCancel.style.marginRight = 6;

            var btnAdd = new Button(() => { onConfirm?.Invoke(selectedColor); editorWindow.Close(); })
            { text = "Add Preset" };
            btnAdd.style.height = 28;
            btnAdd.style.paddingLeft = 14;
            btnAdd.style.paddingRight = 14;

            row.Add(btnCancel);
            row.Add(btnAdd);
            root.Add(label);
            root.Add(colorField);
            root.Add(row);
            return root;
        }
    }
}