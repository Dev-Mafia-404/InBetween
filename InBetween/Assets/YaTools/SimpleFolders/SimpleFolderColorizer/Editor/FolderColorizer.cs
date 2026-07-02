using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace YaTools.SimpleFolders.SimpleFolderColorizer.Editor
{
    [InitializeOnLoad]
    public class FolderColorizer
    {
        private static Texture2D closedFolderTexture;
        private static Texture2D openedFolderTexture;
        private static Texture2D emptyFolderTexture;

        private static Dictionary<string, int> guidToInstanceId = new Dictionary<string, int>();
        private static HashSet<int> expandedSet = new HashSet<int>();
        private static Dictionary<string, bool> emptyFoldersCache = new Dictionary<string, bool>();

        private static readonly PropertyInfo expandedItemsProp = typeof(InternalEditorUtility).GetProperty("expandedProjectWindowItems", BindingFlags.Static | BindingFlags.Public);

        private static FolderColorSettings cachedSettings;

        static FolderColorizer()
        {
            EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
            EditorApplication.projectChanged += OnProjectChanged;            
        }

        private static void OnProjectChanged()
        {
            guidToInstanceId.Clear();
            emptyFoldersCache.Clear();
            cachedSettings = null;

            closedFolderTexture = null;
            openedFolderTexture = null;
            emptyFolderTexture = null;

            EditorApplication.RepaintProjectWindow();
        }

        private static void RefreshExpandedCache()
        {
            var items = expandedItemsProp?.GetValue(null) as int[];
            expandedSet = items != null ? new HashSet<int>(items) : new HashSet<int>();
        }

        private static void LoadTextures()
        {
            if (closedFolderTexture == null)
                closedFolderTexture = LoadTextureByName("SF_CustomFolder");

            if (openedFolderTexture == null)
                openedFolderTexture = LoadTextureByName("SF_CustomFolderOpened");

            if (emptyFolderTexture == null)
                emptyFolderTexture = LoadTextureByName("SF_CustomFolderEmpty");
        }

        private static Texture2D LoadTextureByName(string name)
        {
            string[] guids = AssetDatabase.FindAssets($"{name} t:Texture2D");

            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<Texture2D>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));

            return null;
        }

        private static int GetInstanceID(string guid, string path)
        {
            if (guidToInstanceId.TryGetValue(guid, out int id))
                return id;

            Object asset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
            if (asset != null)
            {
                guidToInstanceId[guid] = asset.GetInstanceID();
                return guidToInstanceId[guid];
            }
            return 0;
        }

        private static bool IsFolderExpanded(int instanceID) => expandedSet.Contains(instanceID);

        private static bool IsFolderEmpty(string guid, string path)
        {
            if (emptyFoldersCache.TryGetValue(guid, out bool isEmpty))
                return isEmpty;

            string fullPath = Path.GetFullPath(path);

            if (Directory.Exists(fullPath))
            {
                bool hasFiles = Directory.EnumerateFiles(fullPath).Any(f => !f.EndsWith(".meta"));
                bool hasDirs = Directory.EnumerateDirectories(fullPath).Any();
                isEmpty = !hasFiles && !hasDirs;
            }
            else
            {
                isEmpty = false;
            }

            emptyFoldersCache[guid] = isEmpty;
            return isEmpty;
        }

        private static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
        {
            if (Event.current.type != EventType.Repaint) return;

            RefreshExpandedCache();

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!AssetDatabase.IsValidFolder(path)) return;

            if (cachedSettings == null)
                cachedSettings = FolderColorSettings.Instance;
            if (cachedSettings == null) return;

            if (!cachedSettings.TryGetColor(guid, out Color folderColor)) return;

            LoadTextures();
            if (closedFolderTexture == null) return;

            bool isListView = selectionRect.height <= 20f;
            bool isListViewMain = selectionRect.x == 14;
            Texture2D textureToDraw = closedFolderTexture;

            if (IsFolderEmpty(guid, path) && emptyFolderTexture != null)
            {
                textureToDraw = emptyFolderTexture;
            }
            else if (openedFolderTexture != null)
            {
                int instanceID = GetInstanceID(guid, path);
                if (IsFolderExpanded(instanceID) && isListView && !isListViewMain)
                    textureToDraw = openedFolderTexture;
            }

            Rect iconRect = isListView ? new Rect(selectionRect.x, selectionRect.y, selectionRect.height, selectionRect.height) : new Rect(selectionRect.x, selectionRect.y, selectionRect.width, selectionRect.width);

            if (isListViewMain)
                iconRect.x += 3;

            GUI.color = folderColor;
            GUI.DrawTexture(iconRect, textureToDraw);
            GUI.color = Color.white;            
        }
    }
}