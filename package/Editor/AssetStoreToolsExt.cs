using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using needle.EditorPatching;

// ReSharper disable InconsistentNaming

namespace Needle.PackageTools
{
    public class PackagerPatchProvider : EditorPatchProvider
    {
        public override bool ActiveByDefault => true;
        public override string Description => "Allows to export files from `Packages` via `Assets/Export Package`";
        public override string DisplayName => "UPM support for `Assets/Export Package`";
        
        protected override void OnGetPatches(List<EditorPatch> patches)
        {
            patches.Add(new PackagerPatch());
        }
        
        private class PackagerPatch : EditorPatch
        {
            protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
            {
                var m = typeof(EditorWindow).Assembly.GetType("UnityEditor.PackageExport").GetMethod("GetAssetItemsForExport", (BindingFlags) (-1));
                targetMethods.Add(m);
                return Task.CompletedTask;
            }
            
            // ReSharper disable once UnusedMember.Local
            // ReSharper disable once RedundantAssignment
            private static bool Prefix(ref IEnumerable<ExportPackageItem> __result, ICollection<string> guids, bool includeDependencies)
            {
                // check if this is an attempt to export from Packages, otherwise use original method
                var someGuidsAreInsidePackages = guids
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Any(x => !string.IsNullOrEmpty(x) && x.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase));
                if (!someGuidsAreInsidePackages)
                    return true;
                
                string[] resultingGuids = new string[0];
                foreach(var guid in guids)
                    resultingGuids = AssetDatabase.CollectAllChildren(guid, resultingGuids);

                if (includeDependencies)
                {
                    Debug.Log("[AssetStoreToolsEx] You're exporting a package. Dependency packages need to be manually selected. IncludeDependencies will be ignored.");
                }
                
                __result = resultingGuids.Distinct().Select(x => new ExportPackageItem()
                {
                    assetPath = AssetDatabase.GUIDToAssetPath(x),
                    enabledStatus = (int) PackageExportTreeView.EnabledState.All,
                    guid = x,
                    isFolder = AssetDatabase.GetMainAssetTypeAtPath(AssetDatabase.GUIDToAssetPath(x)) == typeof(DefaultAsset)
                });
                
                // this just doesn't warn, but still does not include the right items
                // __result = PackageUtility.BuildExportPackageItemsList(resultingGuids, false);
                return false;
            }
        }

    }
    
    public class AssetStoreToolsPatchProvider : EditorPatchProvider
    {
        public override bool ActiveByDefault => true;
        public override string Description => "Allows to export and upload .unitypackage files that contain Packages data to AssetStore";
        public override string DisplayName => "UPM support for AssetStoreTools";

        protected override void OnGetPatches(List<EditorPatch> patches)
        {
            patches.Add(new PathValidationPatch());
            patches.Add(new RootPathPatch());
            patches.Add(new GetGUIDsPatch());
        }
        
        public class PathValidationPatch : EditorPatch
        {
            protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
            {
                var m = typeof(HelpWindow).Assembly.GetType("AssetStorePackageController").GetMethod("IsValidProjectFolder", (BindingFlags) (-1));            
                targetMethods.Add(m);
                return Task.CompletedTask;
            }

            // ReSharper disable once UnusedMember.Local
            // ReSharper disable once UnusedParameter.Local
            // ReSharper disable once RedundantAssignment
            private static bool Prefix(ref bool __result, string directory)
            {
                __result = true;
                return false;
            }
        }
        
        public class RootPathPatch : EditorPatch
        {
            protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
            {
                var m = typeof(HelpWindow).Assembly.GetType("AssetStorePackageController").GetMethod("SetRootPath", (BindingFlags) (-1));            
                targetMethods.Add(m);
                return Task.CompletedTask;
            }

            // ReSharper disable once UnusedMember.Local
            private static bool Prefix(object __instance, string path)
            {
                if (__instance == null) return true;
                // Debug.Log("Trying to call SetRootPath with " + path);
                
                var m_UnsavedChanges = __instance.GetType().GetField("m_UnsavedChanges", (BindingFlags) (-1));
                if (m_UnsavedChanges == null) return true;
                m_UnsavedChanges.SetValue(__instance, true);
                
                // project-relative path:
                var directoryName = Path.GetDirectoryName(Application.dataPath);
                if (string.IsNullOrEmpty(directoryName)) return true;
                
                var relative = new Uri(directoryName).MakeRelativeUri(new Uri(path)).ToString();
                
                var m_LocalRootPath = __instance.GetType().GetField("m_LocalRootPath", (BindingFlags) (-1));
                if (m_LocalRootPath == null) return true;
                m_LocalRootPath.SetValue(__instance, "/" + relative);
                
                var m_MainAssets = __instance.GetType().GetField("m_MainAssets", (BindingFlags) (-1));
                if (m_MainAssets == null) return true;
                
                var mainAssets = m_MainAssets.GetValue(__instance) as List<string>;
                if (mainAssets == null) return true;
                mainAssets.Clear();
                return false;
            }
        }
        
        // private string[] GetGUIDS(bool includeProjectSettings)
        public class GetGUIDsPatch : EditorPatch
        {
            protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
            {
                var m = typeof(HelpWindow).Assembly.GetType("AssetStorePackageController").GetMethod("GetGUIDS", (BindingFlags) (-1));            
                targetMethods.Add(m); 
                return Task.CompletedTask;
            }

            // ReSharper disable once UnusedMember.Local
            private static bool Prefix(ref string[] __result, object __instance, bool includeProjectSettings)
            {
                var m_LocalRootPath = __instance.GetType().GetField("m_LocalRootPath", (BindingFlags) (-1));
                if (m_LocalRootPath == null) return true;
                var localRootPath = m_LocalRootPath.GetValue(__instance) as string;
                
                // Debug.Log("GetGUIDs for " + localRootPath);
                
                // localRootPath is now project-relative, so not a package folder...
                // we need to figure out if it's a proper package folder here, or already convert the path way earlier
                // quick hack: we'll just check if any project package has this as resolved path
                UnityEditor.PackageManager.PackageInfo packageInfo = null;
                var packageInfos = AssetDatabase
                    .FindAssets("package").Where(x => AssetDatabase.GUIDToAssetPath(x).EndsWith("package.json"))
                    .Select(x => UnityEditor.PackageManager.PackageInfo.FindForAssetPath(AssetDatabase.GUIDToAssetPath(x))).Distinct();
                foreach (var x in packageInfos)
                {
                    var localFullPath = Path.GetFullPath(Application.dataPath + localRootPath);
                    var packageFullPath = Path.GetFullPath(x.resolvedPath);
                    // Debug.Log("Checking paths: " + localFullPath + " <== " + packageFullPath);
                    
                    if (localFullPath == packageFullPath) {
                        packageInfo = x;
                        break;
                    }
                }

                var assetDbPath = localRootPath;
                if (packageInfo != null)
                {
                    assetDbPath = "Packages/" + packageInfo.name;
                    if (includeProjectSettings)
                    {
                        Debug.LogWarning("[AssetStoreToolsEx] You're exporting a package - please note that project settings won't be included!");
                    }
                }
                
                string[] collection = new string[0];
                var children = AssetDatabase.CollectAllChildren(AssetDatabase.AssetPathToGUID(assetDbPath), collection);

                if (!children.Any())
                    throw new NullReferenceException("[AssetStoreToolsEx] " + "Something went wrong, this folder can't be exported as .unitypackage.");

                __result = children;
                return false;
            }
        }
    }
}