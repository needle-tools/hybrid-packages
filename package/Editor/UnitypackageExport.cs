using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Needle.PackageTools
{
    public static class UnitypackageExporter
    {
        // This does the same as "Assets/Export Package" but doesn't require the EditorPatching package.
        // [MenuItem("Assets/Export Package (with UPM support)")]
        // static void ExportSelected()
        // {
        //     var folders = Selection.objects.Where(x => x is DefaultAsset).Cast<DefaultAsset>();
        //     if (!folders.Any()) return;
        //
        //     ExportUnitypackage(folders.Select(x => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(x))), "Export.unitypackage");
        // }

        public static string ExportUnitypackage(IEnumerable<PackageInfo> packageInfo, string fileName)
        {
            var guids = packageInfo.Select(x => AssetDatabase.AssetPathToGUID("Packages/" + x.name));
            return ExportUnitypackage(guids, fileName);
        }

        public static string ExportUnitypackage(IEnumerable<string> rootGuids, string fileName)
        {
            if (!fileName.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError("File name must end with .unitypackage");
                return null;
            }
            
            var collection = new string[0];
            foreach(var guid in rootGuids) {
                collection = AssetDatabase.CollectAllChildren(guid, collection);
            }

            PackageUtility.ExportPackage(collection, fileName);
            // TODO return absolute export path
            return Application.dataPath + "/" + fileName;
        }
    }
}