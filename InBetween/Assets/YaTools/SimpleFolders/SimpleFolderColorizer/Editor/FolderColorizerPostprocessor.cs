using UnityEngine;
using UnityEditor;
namespace YaTools.SimpleFolders.SimpleFolderColorizer.Editor
{
    public class FolderColorizerPostprocessor : AssetPostprocessor
    {
        private static bool cleanupScheduled = false;

        static void OnPostprocessAllAssets(
            string[] importedAssets, 
            string[] deletedAssets, 
            string[] movedAssets, 
            string[] movedFromAssetPaths)
        {
            if(deletedAssets.Length == 0 && movedFromAssetPaths.Length == 0) return;

            if (cleanupScheduled) return;
            cleanupScheduled = true;

            EditorApplication.delayCall += () =>
            {
                cleanupScheduled = false;

                var settings = FolderColorSettings.Instance;
                if (settings == null) return;

                settings.CleanupOrphanedEntries();
            };            
        }
    }
}