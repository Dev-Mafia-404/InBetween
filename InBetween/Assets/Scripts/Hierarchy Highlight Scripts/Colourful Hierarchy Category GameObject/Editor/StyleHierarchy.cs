using MStudio;
using UnityEditor;
using UnityEngine;


namespace VS
{
    [InitializeOnLoad]
    public class StyleHierarchy
    {
        static string[] dataArray;//Find ColorPalette GUID
        static string path;//Get ColorPalette(ScriptableObject) path
        static ColorPalette colorPalette;

        // 0..1 : how "washed out" inactive items should look (higher = more faded)
        private const float InactiveFadeStrength = 0.55f;

        static StyleHierarchy()
        {
            dataArray = AssetDatabase.FindAssets("t:ColorPalette");

            if (dataArray.Length >= 1)
            {
                path = AssetDatabase.GUIDToAssetPath(dataArray[0]);
                colorPalette = AssetDatabase.LoadAssetAtPath<ColorPalette>(path);

                EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindow;
            }
        }

        private static void OnHierarchyWindow(int instanceID, Rect selectionRect)
        {
            if (dataArray.Length == 0) return;

            Object instance = EditorUtility.InstanceIDToObject(instanceID);
            if (instance == null) return;

            bool isInactive = instance is GameObject go && !go.activeInHierarchy;

            ColorDesign bestDesign = null;
            int bestKeyLength = -1;

            for (int i = 0; i < colorPalette.colorDesigns.Count; i++)
            {
                var design = colorPalette.colorDesigns[i];
                if (string.IsNullOrEmpty(design.keyChar)) continue;

                if (!instance.name.StartsWith(design.keyChar)) continue;

                int keyLen = design.keyChar.Length;
                if (keyLen > bestKeyLength)
                {
                    bestKeyLength = keyLen;
                    bestDesign = design;
                }
            }

            if (bestDesign == null) return;

            string newName = instance.name.Substring(bestDesign.keyChar.Length);

            // IMPORTANT: keep background opaque so Unity's default text can't show through.
            Color background = bestDesign.backgroundColor;
            Color text = bestDesign.textColor;

            // Approximate the hierarchy row background for "fading" via color blending (not alpha).
            // This keeps our highlight fully opaque.
            Color rowBase = EditorGUIUtility.isProSkin
                ? new Color32(56, 56, 56, 255)
                : new Color32(194, 194, 194, 255);

            if (isInactive)
            {
                background = Color.Lerp(background, rowBase, InactiveFadeStrength);
                text = Color.Lerp(text, rowBase, InactiveFadeStrength);
            }

            background.a = 1f;
            text.a = 1f;

            EditorGUI.DrawRect(selectionRect, background);

            var newStyle = new GUIStyle
            {
                alignment = bestDesign.textAlignment,
                fontStyle = bestDesign.fontStyle,
                normal = new GUIStyleState { textColor = text }
            };

            EditorGUI.LabelField(selectionRect, newName.ToUpper(), newStyle);
        }
    }
}