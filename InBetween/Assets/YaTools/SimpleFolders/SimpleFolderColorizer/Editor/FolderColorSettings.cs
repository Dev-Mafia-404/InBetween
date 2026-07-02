using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace YaTools.SimpleFolders.SimpleFolderColorizer.Editor
{
    [Serializable]
    public class FolderColorEntry
    {
        public string guid;
        public Color color;
    }

    [CreateAssetMenu(fileName = "FolderColorSettings", menuName = "YaTools/Simple Folder Colorizer/Folder Colorizer Settings")]
    public class FolderColorSettings : ScriptableObject
    {
        public List<FolderColorEntry> folderColors = new List<FolderColorEntry>();        
        public List<Color> customPresets = new List<Color>();
        private Dictionary<string, Color> colorMap = new Dictionary<string, Color>();
        private Dictionary<string, int> entryIndexMap = new Dictionary<string, int>();


        private static FolderColorSettings instance;
        public static FolderColorSettings Instance
        {
            get
            {
                if (instance != null) return instance;

                if (!UnityEditorInternal.InternalEditorUtility.CurrentThreadIsMainThread())
                    return null;

                string[] guids = AssetDatabase.FindAssets("t:FolderColorSettings");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    instance = AssetDatabase.LoadAssetAtPath<FolderColorSettings>(path);
                }
                else
                {
                    instance = CreateInstance<FolderColorSettings>();
                    instance.customPresets = new List<Color>()
                    {
                        GetColor("#FF7B7B"), // Soft Red
                        GetColor("#FF9B7B"), // Coral            
                        GetColor("#FFD07B"), // Amber
                        GetColor("#F5F07B"), // Soft Yellow            
                        GetColor("#9DF0A0"), // Mint
                        GetColor("#7BD4B4"), // Teal            
                        GetColor("#7BB8F0"), // Sky Blue
                        GetColor("#7B8FF0"), // Cornflower            
                        GetColor("#B07BF0"), // Lavender
                        GetColor("#E87BF0"), // Orchid
                        GetColor("#F07BB8"), // Pink            
                        GetColor("#F0F0F0"), // Almost White
                        GetColor("#A0A0A0"), // Mid Gray
                        GetColor("#606060"), // Dark Gray
                        GetColor("#2A2A2A"), // Almost Black                        
                    };

                    string folderPath = "Assets";
                    string[] scriptGuids = AssetDatabase.FindAssets("FolderColorSettings t:MonoScript");

                    if(scriptGuids.Length > 0)
                    {
                        string scriptPath = AssetDatabase.GUIDToAssetPath(scriptGuids[0]);
                        folderPath = System.IO.Path.GetDirectoryName(scriptPath).Replace('\\', '/');
                    }

                    string defaultPath = folderPath + "/FolderColorSettings.asset";

                    AssetDatabase.CreateAsset(instance, defaultPath);
                    AssetDatabase.SaveAssets();
                }

                return instance;
            }
        }

        private void OnEnable() => RebuildMaps();

        private void OnDisable() => instance = null;

        private void RebuildMaps()
        {
            colorMap = new Dictionary<string, Color>(folderColors.Count);
            entryIndexMap = new Dictionary<string, int>(folderColors.Count);

            for (int i = 0; i < folderColors.Count; i++)
            {
                colorMap[folderColors[i].guid] = folderColors[i].color;
                entryIndexMap[folderColors[i].guid] = i;
            }
        }

        public bool TryGetColor(string guid, out Color color) => colorMap.TryGetValue(guid, out color);

        public void SetFolderColor(string guid, Color color) 
        {
            colorMap[guid] = color;

            if (entryIndexMap.TryGetValue(guid, out int index))
            {                
                folderColors[index].color = color;
            }
            else
            {             
                entryIndexMap[guid] = folderColors.Count;
                folderColors.Add(new FolderColorEntry { guid = guid, color = color });
            }
        }

        public void RemoveFolderColor(string guid)
        {
            if (!entryIndexMap.TryGetValue(guid, out int index)) return;

            colorMap.Remove(guid);
            folderColors.RemoveAt(index);

            RebuildMaps();
        }

        public void Save()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            EditorApplication.RepaintProjectWindow();
        }

        private static Color GetColor(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color color))
                return color;
            return Color.white;
        }

        public void CleanupOrphanedEntries()
        {
            bool changed = false;

            for (int i = folderColors.Count -1; i >= 0; i--)
            {
                string path = AssetDatabase.GUIDToAssetPath(folderColors[i].guid);
                if(string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path))
                {
                    colorMap.Remove(folderColors[i].guid);
                    entryIndexMap.Remove(folderColors[i].guid);
                    folderColors.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed)
            {
                RebuildMaps();
                Save();
            }                
        }
    }
}
