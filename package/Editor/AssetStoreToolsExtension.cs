using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using needle.EditorPatching;
using UnityEditor.Compilation;
using Assembly = System.Reflection.Assembly;

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

                // if (includeDependencies)
                //     Helpers.Log("You're exporting a package. If your package has dependencies and you want to export them, they need to be manually selected.");

                var rootsAndChildGuids = resultingGuids.Union(guids).Distinct();
                if (includeDependencies)
                    rootsAndChildGuids = rootsAndChildGuids.Union(AssetDatabase
                        .GetDependencies(rootsAndChildGuids.Select(AssetDatabase.GUIDToAssetPath).ToArray(), true)
                        .Select(AssetDatabase.AssetPathToGUID))
                        .Distinct();

                __result = rootsAndChildGuids
                    .Select(x => new ExportPackageItem()
                    {
                        assetPath = AssetDatabase.GUIDToAssetPath(x),
                        enabledStatus = (int) PackageExportTreeView.EnabledState.All,
                        guid = x,
                        isFolder = AssetDatabase.GetMainAssetTypeAtPath(AssetDatabase.GUIDToAssetPath(x)) == typeof(DefaultAsset)
                    })
                    .Where(x => !x.isFolder); // ignore folders, otherwise these seem to end up being separate assets and ignored on import
                
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
            if (Helpers.GetAssetStoreToolsAssembly() == null) return;
            
            patches.Add(new PathValidationPatch());
            patches.Add(new RootPathPatch());
            patches.Add(new GetGUIDsPatch());
        }

        public class PathValidationPatch : EditorPatch
        {
            protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
            {
                var asm = Helpers.GetAssetStoreToolsAssembly();
                if (asm == null) return Task.CompletedTask;
                var m = asm.GetType("AssetStorePackageController").GetMethod("IsValidProjectFolder", (BindingFlags) (-1));
                targetMethods.Add(m);
                return Task.CompletedTask;
            }

            // ReSharper disable once UnusedMember.Local
            // ReSharper disable once UnusedParameter.Local
            // ReSharper disable once RedundantAssignment
            private static bool Prefix(ref bool __result, string directory)
            {
                if (Path.GetFullPath(directory).Replace("\\", "/").StartsWith(Application.dataPath, StringComparison.Ordinal))
                    return true;

                __result = true;
                return false;
            }
        }

        public class RootPathPatch : EditorPatch
        {
            protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
            {
                var asm = Helpers.GetAssetStoreToolsAssembly();
                if (asm == null) return Task.CompletedTask;
                var m = asm.GetType("AssetStorePackageController").GetMethod("SetRootPath", (BindingFlags) (-1));
                targetMethods.Add(m);
                return Task.CompletedTask;
            }

            // ReSharper disable once UnusedMember.Local
            private static bool Prefix(object __instance, string path)
            {
                if (__instance == null) return true;
                // Helpers.Log(path);
                if (path.StartsWith(Application.dataPath, StringComparison.Ordinal))
                    return true;

                var m_UnsavedChanges = __instance.GetType().GetField("m_UnsavedChanges", (BindingFlags) (-1));
                if (m_UnsavedChanges == null) return true;
                m_UnsavedChanges.SetValue(__instance, true);

                // project-relative path:
                var directoryName = Application.dataPath;
                if (string.IsNullOrEmpty(directoryName)) return true;

                var relative = "/../" + new Uri(directoryName).MakeRelativeUri(new Uri(path));
                // Helpers.Log(directoryName + " + " + path + " = " + relative);

                var m_LocalRootPath = __instance.GetType().GetField("m_LocalRootPath", (BindingFlags) (-1));
                if (m_LocalRootPath == null) return true;
                m_LocalRootPath.SetValue(__instance, relative);

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
                var asm = Helpers.GetAssetStoreToolsAssembly();
                if (asm == null) return Task.CompletedTask;
                var m = asm.GetType("AssetStorePackageController").GetMethod("GetGUIDS", (BindingFlags) (-1));
                targetMethods.Add(m);
                return Task.CompletedTask;
            }

            
            class PackageInfoMock
            {
                public string name;
                public string resolvedPath;
            }
            
            // ReSharper disable once UnusedMember.Local
            private static bool Prefix(ref string[] __result, object __instance, bool includeProjectSettings)
            {
                var m_LocalRootPath = __instance.GetType().GetField("m_LocalRootPath", (BindingFlags) (-1));
                if (m_LocalRootPath == null) return true;
                var localRootPath = m_LocalRootPath.GetValue(__instance) as string;

                // Helpers.Log(localRootPath + ", " + Application.dataPath);

                if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID("Assets/" + localRootPath)))
                    return true;

                if (string.IsNullOrEmpty(localRootPath) || localRootPath.Equals("/", StringComparison.Ordinal))
                    return true;

                
                // localRootPath is now project-relative, so not a package folder...
                // We need to figure out if it's a proper package folder here, or already convert the path way earlier
                // For now, we'll just check if any project package has this as resolved path
                PackageInfoMock packageInfo = null;
                var packageInfos = AssetDatabase
                    .FindAssets("package")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(x => x.StartsWith("Packages/") && x.EndsWith("/package.json"))
                    .Select(x =>
                    {
                        if (string.IsNullOrEmpty(x)) return null;
                        var parts = x.Split('/');
                        if (parts.Length != 3) return null;
                        
                        return new PackageInfoMock()
                        {
                            name = parts[1],
                            resolvedPath = Path.GetDirectoryName(Path.GetFullPath(x))?.Replace("\\", "/")
                        };
                    })
                    .Where(x => x != null)
                    .Distinct();

                foreach (var x in packageInfos)
                {
                    var localFullPath = Path.GetFullPath(Application.dataPath + localRootPath);
                    var packageFullPath = Path.GetFullPath(x.resolvedPath);
                    // Debug.Log("Checking paths: " + localFullPath + " <== " + packageFullPath);

                    if (localFullPath == packageFullPath)
                    {
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
                        Helpers.LogWarning("You're exporting a package - please note that project settings won't be included!");
                    }
                }

                string[] collection = new string[0];
                var children = AssetDatabase.CollectAllChildren(AssetDatabase.AssetPathToGUID(assetDbPath), collection);

                if (!children.Any())
                    throw new NullReferenceException(Helpers.LogPrefix + "Seems you're trying to export something that's not in your AssetDatabase: " + assetDbPath + " - this can't be exported as .unitypackage.");

                __result = children;
                return false;
            }
        }
    }
    internal static class Helpers
    {
        internal static string LogPrefix => "<b>[AssetStoreToolsExtension]</b> ";
        
        internal static void Log(object obj)
        {
            Debug.Log(LogPrefix + obj);
        }
        
        internal static void LogWarning(object obj)
        {
            Debug.LogWarning(LogPrefix + obj);
        }

        internal static Assembly GetAssetStoreToolsAssembly()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var assembly = assemblies.FirstOrDefault(x => x.FullName == @"AssetStoreTools, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
            return assembly;
        }
    }
}