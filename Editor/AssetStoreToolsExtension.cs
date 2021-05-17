using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using Assembly = System.Reflection.Assembly;
using Debug = UnityEngine.Debug;

// ReSharper disable InconsistentNaming

namespace Needle.PackageTools
{
    public class PackagerPatchProvider
    {
        [InitializeOnLoadMethod]
        static void OnGetPatches()
        {
            var harmony = new Harmony(nameof(PackagerPatchProvider));
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.PackageExport");
            var method = type?.GetMethod("GetAssetItemsForExport", (BindingFlags) (-1));
            if(method == null) return;
            harmony.Patch(method, new HarmonyMethod(AccessTools.Method(typeof(PackagerPatch), "Prefix")));
        }
        
        private class PackagerPatch
        {
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

                var rootsAndChildGuids = resultingGuids.Union(guids).Distinct().ToList();
                if (includeDependencies)
                    rootsAndChildGuids = rootsAndChildGuids.Union(AssetDatabase
                        .GetDependencies(rootsAndChildGuids.Select(AssetDatabase.GUIDToAssetPath).ToArray(), true)
                        .Select(AssetDatabase.AssetPathToGUID))
                        .Distinct().ToList();

                __result = rootsAndChildGuids
                    .Select(x => new ExportPackageItem()
                    {
                        assetPath = AssetDatabase.GUIDToAssetPath(x),
                        enabledStatus = (int) PackageExportTreeView.EnabledState.All,
                        guid = x,
                        isFolder = AssetDatabase.IsValidFolder(AssetDatabase.GUIDToAssetPath(x))
                    })
                    .Where(x => !x.isFolder); // ignore folders, otherwise these seem to end up being separate assets and ignored on import
                
                // this just doesn't warn, but still does not include the right items
                // __result = PackageUtility.BuildExportPackageItemsList(resultingGuids, false);
                return false;
            }
        }
    }

    public class AssetStoreToolsPatchProvider
    {
        [InitializeOnLoadMethod]
        static void OnGetPatches()
        {
            // safeguard, should be prevented by OnWillEnablePatch
            if (Helpers.GetAssetStoreToolsAssembly() == null) return;

            var harmony = new Harmony(nameof(AssetStoreToolsPatchProvider));
            PathValidationPatch.Patch(harmony);
            RootPathPatch.Patch(harmony);
            GetGUIDsPatch.Patch(harmony);
            PackagerExportPatch.Patch(harmony);
        }

        public class PathValidationPatch
        {
            public static void Patch(Harmony harmony)
            {
                var asm = Helpers.GetAssetStoreToolsAssembly();
                if (asm == null) return;
                var m = asm.GetType("AssetStorePackageController").GetMethod("IsValidProjectFolder", (BindingFlags) (-1));
                harmony.Patch(m, new HarmonyMethod(AccessTools.Method(typeof(PathValidationPatch), "Prefix")));
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

        public class RootPathPatch
        {
            public static void Patch(Harmony harmony)
            {
                var asm = Helpers.GetAssetStoreToolsAssembly();
                if (asm == null) return;
                var m = asm.GetType("AssetStorePackageController").GetMethod("SetRootPath", (BindingFlags) (-1));
                harmony.Patch(m, new HarmonyMethod(AccessTools.Method(typeof(RootPathPatch), "Prefix")));
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
        public class GetGUIDsPatch
        {
            public static void Patch(Harmony harmony)
            {
                var asm = Helpers.GetAssetStoreToolsAssembly();
                if (asm == null) return;
                var m = asm.GetType("AssetStorePackageController").GetMethod("GetGUIDS", (BindingFlags) (-1));
                harmony.Patch(m, new HarmonyMethod(AccessTools.Method(typeof(GetGUIDsPatch), "Prefix")));
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

                if (string.IsNullOrEmpty(localRootPath) || localRootPath.Equals("/", StringComparison.Ordinal))
                    return true;

                List<string> results = new List<string>();

                if (Directory.Exists("Assets/" + localRootPath) && !localRootPath.StartsWith("/.."))
                {
                    // find the config inside this folder
                    var configs = AssetDatabase.FindAssets("t:AssetStoreUploadConfig", new [] {"Assets/" + localRootPath});
                    if(configs.Any())
                    {
                        var configPath = AssetDatabase.GUIDToAssetPath(configs.First());
                        var uploadConfig = AssetDatabase.LoadAssetAtPath<AssetStoreUploadConfig>(configPath);
                        Debug.Log("Upload Config detected. The selected path will be ignored, and the upload config will be used instead.", uploadConfig);
                        
                        if(uploadConfig)
                        {
                            if (!uploadConfig.IsValid)
                                throw new ArgumentException("The selected upload config at " + configPath + " is not valid.");
                            
                            foreach (var path in uploadConfig.GetExportPaths())
                            {
                                AddChildrenToResults(path);
                            }
                            
                            __result = results.ToArray();
                            // Debug.Log("Included files: " + string.Join("\n", __result.Select(AssetDatabase.GUIDToAssetPath))); 
                            return false;
                        }
                    }
                }
                
                if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID("Assets/" + localRootPath)))
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
                    
                    if (!Unsupported.IsDeveloperMode())
                    {
                        // sanitize: do not allow uploading packages that are in the Library
                        var libraryRoot = Path.GetFullPath(Application.dataPath + "/../Library");
                        if (Path.GetFullPath(assetDbPath).StartsWith(libraryRoot, StringComparison.Ordinal))
                            throw new ArgumentException("You're trying to export a package from your Libary folder. This is not allowed. Only local/embedded packages can be exported.");

                        // sanitize: do not allow re-uploading of Unity-scoped packages
                        if (packageInfo.name.StartsWith("com.unity.", StringComparison.OrdinalIgnoreCase))
                            throw new ArgumentException("You're trying to export a package from the Unity registry. This is not allowed.");
                    }

                    if (includeProjectSettings)
                    {
                        Helpers.LogWarning("You're exporting a package - please note that project settings won't be included!");
                    }
                }

                void AddChildrenToResults(string assetPath)
                {
                    if (File.Exists(assetPath))
                    {
                        results.Add(AssetDatabase.AssetPathToGUID(assetPath));
                        return;
                    }
                    
                    string[] collection = new string[0];
                    var children = AssetDatabase.CollectAllChildren(AssetDatabase.AssetPathToGUID(assetPath), collection);

                    if (!children.Any())
                        throw new NullReferenceException(Helpers.LogPrefix + "Seems you're trying to export something that's not in your AssetDatabase: " + assetPath + " - this can't be exported as .unitypackage.");

                    results.AddRange(children);
                }

                AddChildrenToResults(assetDbPath);
                
                __result = results.Distinct().ToArray();
                // Debug.Log("Included files: " + string.Join("\n", __result.Select(AssetDatabase.GUIDToAssetPath)));
                return false;
            }
        }
        
        public class PackagerExportPatch
        {
            public static void Patch(Harmony harmony)
            {
                var asm = Helpers.GetAssetStoreToolsAssembly();
                if (asm == null) return;
                var m = asm.GetType("Packager").GetMethod("ExportPackage", (BindingFlags) (-1));
                harmony.Patch(m, new HarmonyMethod(AccessTools.Method(typeof(PackagerExportPatch), "Prefix")));
            }

            // ReSharper disable once UnusedMember.Local
            // ReSharper disable once UnusedParameter.Local
            // ReSharper disable once RedundantAssignment
            private static bool Prefix(string[] guids, string fileName, bool needsPackageManagerManifest)
            {
                // we want to patch this if there's packages in here
                var anyFileInPackages = guids.Select(AssetDatabase.GUIDToAssetPath).Any(x => x.StartsWith("Packages/", StringComparison.Ordinal));

                if (anyFileInPackages && needsPackageManagerManifest)
                    throw new ArgumentException("When exporting Hybrid Packages, please don't enable the \"Include Dependencies\" option. Specify dependencies via package.json.");

                // custom package export - constructing .unitypackage while respecting AssetDatabase,
                // hidden top-level folders (end with ~) and .npmignore/.gitignore
                if (anyFileInPackages)
                {
                    Profiler.BeginSample("Collect Package Roots");
                    
                    // get all packages we want to export
                    var packageRoots = new HashSet<string>();
                    
                    // the resulting project-relative paths (not absolute paths)
                    var exportPaths = new HashSet<string>();

                    // all the currently selected files from AssetDB should be exported anyways
                    var assetDatabasePaths = guids.Select(AssetDatabase.GUIDToAssetPath).ToList();
                    foreach (var p in assetDatabasePaths)
                        exportPaths.Add(p);

                    foreach (var path0 in assetDatabasePaths)
                    {
                        var path = path0;
                        if (!path.StartsWith("Packages/", StringComparison.Ordinal)) continue;
                        
                        path = path.Substring("Packages/".Length);
                        var indexOfSlash = path.IndexOf("/", StringComparison.Ordinal);
                        var packageName = path.Substring(0, indexOfSlash);
                        path = "Packages/" + packageName;
                        
                        if(!packageRoots.Contains(path))
                            packageRoots.Add(path);
                    }
                    Profiler.EndSample();

                    #region Handle file ignore
                    
                    var ignoreFiles = new List<(string dir, string content)>();
                    
                    // collect npm and gitignore files in all subdirectories
                    void CollectIgnoreFiles(string directory)
                    {
                        Profiler.BeginSample(nameof(CollectIgnoreFiles));
                        ignoreFiles.Clear();

                        var di = new DirectoryInfo(directory);

                        void AddToIgnore(List<(string dir, string content)> ignoreList, string searchPattern, SearchOption searchOption)
                        {
                            try
                            {
                                foreach (var file in di.GetFiles(searchPattern, searchOption))
                                    ignoreFiles.Add((file.Directory!.FullName.Replace("\\", "/"), File.ReadAllText(file.FullName)));
                            }
                            catch (IOException)
                            {
                                // ignore
                            }
                        }
                        
                        // find all ignore files in subdirectories
                        AddToIgnore(ignoreFiles, ".gitignore", SearchOption.AllDirectories);
                        AddToIgnore(ignoreFiles, ".npmignore", SearchOption.AllDirectories);

                        var upwardsIgnoreFiles = new List<(string, string)>();
                        bool folderIsInsideGitRepository = false;

                        try
                        {
                            // find ignore files up to directory root or until a .git folder is found
                            while (di.Parent != null)
                            {
                                di = di.Parent;

                                AddToIgnore(upwardsIgnoreFiles, ".gitignore", SearchOption.TopDirectoryOnly);
                                AddToIgnore(upwardsIgnoreFiles, ".npmignore", SearchOption.TopDirectoryOnly);

                                // we should stop at a git repo (.git folder) or a submodule (.git file)
                                if (di.GetDirectories(".git", SearchOption.TopDirectoryOnly).Any() ||
                                    di.GetFiles(".git", SearchOption.TopDirectoryOnly).Any())
                                {
                                    folderIsInsideGitRepository = true;
                                    break;
                                }
                            }
                        }
                        catch (IOException)
                        {
                            folderIsInsideGitRepository = false;
                            upwardsIgnoreFiles.Clear();
                        }

                        // if we found any upwards git folder we add those ignore files to our list here, otherwise
                        // let's assume this isn't inside a git repo and we shouldn't look at those.
                        if (folderIsInsideGitRepository)
                            ignoreFiles.AddRange(upwardsIgnoreFiles);
                        
                        Profiler.EndSample();
                    }
                    
                    bool IsIgnored(string filePath)
                    {
                        Profiler.BeginSample(nameof(IsIgnored));
                        
                        filePath = filePath.Replace("\\", "/");
                        foreach (var ig in ignoreFiles)
                        {
                            // check if the file is a sub file of the ignore file
                            // because we dont want to apply ignore patterns to external files
                            // e.g. a file in "stuff/material" should not be affected from "myFolder/.gitignore"
                            if (filePath.StartsWith(ig.dir))
                            {
                                using (var reader = new StringReader(ig.content))
                                {
                                    var line = reader.ReadLine();
                                    if (string.IsNullOrEmpty(line)) continue;
                                    if (Regex.Match(filePath, line).Success)
                                    {
                                        Debug.Log("<b>IGNORE</b> " + filePath);
                                        Profiler.EndSample();
                                        return true;
                                    }
                                }
                            }
                        }
                        
                        Profiler.EndSample();
                        return false;
                    }
                    
                    #endregion
                    
                    foreach (var root in packageRoots)
                    {
                        Profiler.BeginSample("Collect Files in Package Root: " + root);
                        
                        var fullPath = Path.GetFullPath(root);
                        CollectIgnoreFiles(root);
                        
                        // include all hidden directories (end with ~)
                        foreach (var directory in new DirectoryInfo(root).GetDirectories("*", SearchOption.AllDirectories))
                        {
                            try
                            {
                                // this is a hidden folder. We want to include it in our export to catch
                                // - Samples~
                                // - Documentation~
                                // - Templates~
                                // and so on.
                                if (directory.Name.EndsWith("~", StringComparison.Ordinal))
                                {
                                    // add all files in this directory
                                    foreach (var file in directory.GetFiles("*", SearchOption.AllDirectories))
                                    {
                                        if (file.Extension.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                                            continue;

                                        if (IsIgnored(file.FullName)) continue;

                                        var projectRelativePath = file.FullName.Replace(fullPath, root);
                                        exportPaths.Add(projectRelativePath);
                                    }
                                }
                            }
                            catch (IOException)
                            {
                                // 
                            }
                        }
                        
                        Profiler.EndSample();
                    }
                    
                    // Debug.Log("<b>" + fileName + "</b>" + "\n" + string.Join("\n", exportPaths));
                    
                    Profiler.BeginSample("Create .unitypackage");
                    var dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/Unity/AssetStoreTools/Export";
                    foreach(var path in exportPaths)
                        UnitypackageExporter.AddToUnityPackage(path, dir);
                    if (!Zipper.TryCreateTgz(dir, fileName))
                    {
                        Profiler.EndSample();
                        throw new Exception("Failed creating .unitypackage " + fileName);
                    }
                    EditorUtility.RevealInFinder(fileName);
                    Directory.Delete(dir, true);
                    Profiler.EndSample();
                    return false;
                }
                
                // Debug.Log("<b>" + fileName + "</b>" + "\n" + string.Join("\n", guids.Select(AssetDatabase.GUIDToAssetPath)));
                return true;
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