using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YaTools.SimpleFolders.SimpleFolderColorizer.Editor
{
    public class FolderColorWindow : EditorWindow
    {
        [SerializeField] private VisualTreeAsset uiAsset;
        [SerializeField] private VisualTreeAsset colorButtonTemplate;

        private VisualElement templatesContainer;
        private Toggle subfoldersToggle;
        private FolderColorSettings settings;
        private bool isManagingPresets = false;

        [MenuItem("Tools/YaTools/Simple Folder Colorizer")]
        [MenuItem("Assets/YaTools/Simple Folder Colorizer")]
        public static void ShowWindow()
        {
            var window = GetWindow<FolderColorWindow>(true, "Simple Folder Colorizer", true);            
            window.minSize = new Vector2(300, 300);            
            window.ShowUtility();
        }

        public void CreateGUI()
        {
            settings = FolderColorSettings.Instance;
            uiAsset.CloneTree(rootVisualElement);

            templatesContainer = rootVisualElement.Q<VisualElement>("color-templates");
            subfoldersToggle = rootVisualElement.Q<Toggle>();

            Button btnReset = rootVisualElement.Q<Button>("color-remove-button");
            btnReset.clicked += ResetSelectedFolders;
            
            Button btnAddColor = rootVisualElement.Q<Button>("color-add-button");            
            btnAddColor.clicked += () =>
            {
                UnityEditor.PopupWindow.Show(btnAddColor.worldBound, new AddColorPopup(color =>
                {
                    if (!settings.customPresets.Contains(color))                    
                        settings.customPresets.Add(color);
                    
                    ApplyColorToSelectedFolders(color);
                    RefreshColorTemplates();
                }));
            };

            Button btnManage = rootVisualElement.Q<Button>("manage-presets-button");
            btnManage.clicked += () =>
            {
                isManagingPresets = !isManagingPresets;
                btnManage.text = isManagingPresets ? "Done" : "Manage Presets...";
                btnManage.style.backgroundColor = isManagingPresets ? new Color(0.25f, 0.45f, 0.7f, 1f) : StyleKeyword.Null;
                RefreshColorTemplates();
            };

            RefreshColorTemplates();
        }

        private void RefreshColorTemplates()
        {
            templatesContainer.Clear();

            for(int i = 0; i < settings.customPresets.Count; i++)
            {
                Color presetColor = settings.customPresets[i];
                int capturedIndex = i;

                var btnElement = colorButtonTemplate.Instantiate();
                var colorBtn = btnElement.Q<Button>("color-button");                
                colorBtn.style.backgroundColor = presetColor;
                colorBtn.tooltip = $"#{ColorUtility.ToHtmlStringRGB(presetColor)}";
                
                Color hoverColor = new Color(Mathf.Min(presetColor.r * 1.2f, 1f), Mathf.Min(presetColor.g * 1.2f, 1f), Mathf.Min(presetColor.b * 1.2f, 1f), presetColor.a);
                colorBtn.RegisterCallback<PointerEnterEvent>(evt => colorBtn.style.backgroundColor = hoverColor);
                colorBtn.RegisterCallback<PointerLeaveEvent>(evt => colorBtn.style.backgroundColor = presetColor);                                

                if (isManagingPresets)
                {
                    colorBtn.text = "×";                    

                    float brightness = 0.299f * presetColor.r + 0.587f * presetColor.g + 0.114f * presetColor.b;
                    colorBtn.style.color = brightness > 0.5f ? Color.black : Color.white;

                    colorBtn.clicked += () =>
                    {
                        settings.customPresets.RemoveAt(capturedIndex);
                        settings.Save();
                        RefreshColorTemplates();
                    };
                }
                else
                {                                        
                    colorBtn.clicked += () => ApplyColorToSelectedFolders(presetColor);
                }

                templatesContainer.Add(btnElement);
            }
        }

        private void ApplyColorToSelectedFolders(Color color)
        {
            bool applyToSubfolders = subfoldersToggle.value;

            foreach (var guid in Selection.assetGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!AssetDatabase.IsValidFolder(path)) continue;

                settings.SetFolderColor(guid, color);

                if (applyToSubfolders)
                    ProcessSubfoldersRecursive(path, subGuid => settings.SetFolderColor(subGuid, color));
            }

            settings.Save();
        }

        private void ResetSelectedFolders()
        {
            bool applyToSubfolders = subfoldersToggle.value;

            foreach(var guid in Selection.assetGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!AssetDatabase.IsValidFolder(path)) continue;

                settings.RemoveFolderColor(guid);

                if (applyToSubfolders)
                    ProcessSubfoldersRecursive(path, subGuid => settings.RemoveFolderColor(subGuid));
            }

            settings.Save();
        }

        private void ProcessSubfoldersRecursive(string parentPath, Action<string> processGuid)
        {
            foreach (string subfolderPath in AssetDatabase.GetSubFolders(parentPath))
            {
                processGuid(AssetDatabase.AssetPathToGUID(subfolderPath));
                ProcessSubfoldersRecursive(subfolderPath, processGuid);
            }
        }
    }
}