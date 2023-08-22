using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using Assembly = System.Reflection.Assembly;
using Debug = UnityEngine.Debug;

// ReSharper disable InconsistentNaming

namespace Needle.HybridPackages
{
    using Ignore = Ignore.Ignore;

#if UNITY_2019_1_OR_NEWER
    internal class PackagePathValidationPatchProvider
    {
        [InitializeOnLoadMethod]
        static void OnGetPatches()
        {
            var harmony = new Harmony(nameof(PackagerPatchProvider));
            var asms = AppDomain.CurrentDomain.GetAssemblies();  
            var asm = asms.FirstOrDefault(x => x.FullName.StartsWith("asset-store-tools-editor"));
            if (asm == null) return;
            var type = asm.GetType("AssetStoreTools.Uploader.HybridPackageUploadWorkflowView");
            var method = type?.GetMethod("IsValidLocalPackage", (BindingFlags) (-1));
            if (method == null)
            {
                Debug.LogError($"Null method returned from {typeof(PackagePathValidationPatchProvider)}.IsValidLocalPackage. Please report a bug and note your Unity and package versions.");
                return;
            }

            try
            {
                harmony.Patch(method, new HarmonyMethod(AccessTools.Method(typeof(IsValidPatch), "Prefix")));
            }
            catch (Exception e)
            {
                Debug.LogError($"Method patching failed from {typeof(PackagePathValidationPatchProvider)}.IsValidLocalPackage. Please report a bug and note your Unity and package versions.\n{e}");
            }
        }

        private class IsValidPatch
        {
            private static bool Prefix(ref bool __result, string packageFolderPath, out string assetDatabasePackagePath)
            {
#if UNITY_2022_1_OR_NEWER
				var allPackages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
#else
                var allPackages = UnityEditor.PackageManager.PackageInfo.GetAll();
#endif
                var packageInfo = allPackages.FirstOrDefault(
                    x => x.resolvedPath.Replace("\\", "/") == packageFolderPath.Replace("\\", "/"));
                if (packageInfo != null)
                {
                    assetDatabasePackagePath = packageInfo.assetPath;
                    __result = true;
                    return false;
                }
                
                assetDatabasePackagePath = "";
                __result = false;
                return true;
            }
        }
    }
#endif
    
    internal class PackagerPatchProvider
    {
        [InitializeOnLoadMethod]
        static void OnGetPatches()
        {
            if (!Helpers.PatchingSupported()) return;
            
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

    internal class AssetStoreToolsPatchProvider
    {
        private const string outputSubFolder = "Temp"; // NB: this is Unity's "project's Temp folder" so you shouldn't change it
        private static AssetStoreUploadConfig currentUploadConfig;
        
        [InitializeOnLoadMethod]
        static void OnGetPatches()
        {
            // safeguard, should be prevented by OnWillEnablePatch
            if (Helpers.GetAssetStoreToolsAssembly() == null) return;
            if (!Helpers.PatchingSupported()) return;

            var harmony = new Harmony(nameof(AssetStoreToolsPatchProvider));
            PathValidationPatch.Patch(harmony);
            RootPathPatch.Patch(harmony);
            GetGUIDsPatch.Patch(harmony);
            PackagerExportPatch.Patch(harmony);
        }

        internal static void ExportPackageForConfig(AssetStoreUploadConfig uploadConfig)
        {
            var results = new List<string>();
            
            if (!uploadConfig.IsValid)
                throw new ArgumentException("The selected upload config " + uploadConfig + " is not valid.");
            
            currentUploadConfig = uploadConfig;
                            
            foreach (var path in uploadConfig.GetExportPaths())
                GetGUIDsPatch.AddChildrenToResults(results, path);

            var exportFilename = uploadConfig.GetExportFilename(outputSubFolder);
            PackagerExportPatch.ExportPackage(results.ToArray(), exportFilename);
        }

        private class PathValidationPatch
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

        private class RootPathPatch
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

        private class GetGUIDsPatch
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
            
            internal static void AddChildrenToResults(List<string> results, string assetPath)
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
            
            // ReSharper disable once UnusedMember.Local
            private static bool Prefix(ref string[] __result, object __instance, bool includeProjectSettings)
            {
                var m_LocalRootPath = __instance.GetType().GetField("m_LocalRootPath", (BindingFlags) (-1));
                if (m_LocalRootPath == null) return true;
                var localRootPath = m_LocalRootPath.GetValue(__instance) as string;

                if (string.IsNullOrEmpty(localRootPath) || localRootPath.Equals("/", StringComparison.Ordinal))
                    return true;

                List<string> results = new List<string>();

                // seems there's some weird path behaviour on 2018.4 with double //
                if (localRootPath.StartsWith("/", StringComparison.Ordinal) && !localRootPath.StartsWith("/.", StringComparison.Ordinal))
                    localRootPath = localRootPath.Substring("/".Length);
                
                currentUploadConfig = null;
                
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
                            currentUploadConfig = uploadConfig;
                            
                            foreach (var path in uploadConfig.GetExportPaths())
                            {
                                AddChildrenToResults(results, path);
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

                AddChildrenToResults(results, assetDbPath);
                
                __result = results.Distinct().ToArray();
                // Debug.Log("Included files: " + string.Join("\n", __result.Select(AssetDatabase.GUIDToAssetPath)));
                return false;
            }
        }

        // https://npm.github.io/publishing-pkgs-docs/publishing/the-npmignore-file.html
        private static readonly HashSet<string> defaultNpmIgnore = new HashSet<string>()
        {
            ".*.swp",
            "._*",
            ".DS_Store",
            ".git",
            ".hg",
            ".npmrc",
            ".lock-wscript",
            ".svn",
            ".wafpickle-*",
            "config.gypi",
            "CVS",
            "npm-debug.log",
        };

        private class PackagerExportPatch
        {
            public static void Patch(Harmony harmony)
            {
                var asm = Helpers.GetAssetStoreToolsAssembly();
                if (asm == null) return;
                var m = asm.GetType("Packager").GetMethod("ExportPackage", (BindingFlags) (-1));
                harmony.Patch(m, new HarmonyMethod(AccessTools.Method(typeof(PackagerExportPatch), "Prefix")));
            }

            internal static bool ExportPackage(string[] guids, string fileName)
            {
                var result = Prefix(guids, fileName, false);
                if (result)
                {
                    Debug.LogWarning("Couldn't export a Hybrid Package: no files selected that are in packages.");
                }
                return result;
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
                    var sw = new Stopwatch();
                    sw.Start();
                    
                    EditorUtility.DisplayProgressBar("Creating .unitypackage", "Collecting Packages", 0f);
                    Profiler.BeginSample("Collecting Packages");
                    
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
                    
                    var ignoreFiles = new List<(string dir, Ignore ignore)>();
                    
                    // collect npm and gitignore files in all subdirectories
                    void CollectIgnoreFiles(string directory)
                    {
                        Profiler.BeginSample(nameof(CollectIgnoreFiles));
                        ignoreFiles.Clear();

                        var di = new DirectoryInfo(directory);

                        void AddToIgnore(List<(string dir, Ignore ignore)> ignoreList, string searchPattern, SearchOption searchOption)
                        {
                            try
                            {
                                foreach (var file in di.GetFiles(searchPattern, searchOption))
                                {
                                    var ignore = new Ignore();
                                    ignore.Add(File.ReadAllLines(file.FullName).Where(x => !string.IsNullOrWhiteSpace(x.Trim()) && !x.TrimStart().StartsWith("#", StringComparison.Ordinal)));
                                    var fileDirectory = file.Directory;
                                    if(fileDirectory != null)
                                        ignoreFiles.Add((fileDirectory.FullName.Replace("\\", "/"), ignore));
                                }
                            }
                            catch (IOException)
                            {
                                // ignore
                            }
                        }
                        
                        // find all ignore files in subdirectories
                        AddToIgnore(ignoreFiles, ".gitignore", SearchOption.AllDirectories);
                        AddToIgnore(ignoreFiles, ".npmignore", SearchOption.AllDirectories);

                        var upwardsIgnoreFiles = new List<(string, Ignore)>();
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
                            var dirLength = ig.dir.Length + 1;
                            // check if the file is a sub file of the ignore file
                            // because we dont want to apply ignore patterns to external files
                            // e.g. a file in "stuff/material" should not be affected from "myFolder/.gitignore"
                            if (filePath.StartsWith(ig.dir))
                            {
                                var checkedPath = filePath.Substring(dirLength);
                                if (ig.ignore.IsIgnored(checkedPath)) 
                                {
                                    Debug.Log("<b>File will be ignored:</b> " + filePath + ", location of .ignore: " + ig.dir);
                                    Profiler.EndSample();
                                    return true;
                                }
                            }
                        }
                        
                        Profiler.EndSample();
                        return false;
                    }
                    
                    #endregion

                    int counter = 0;
                    int length = packageRoots.Count;
                    foreach (var root in packageRoots)
                    {
                        EditorUtility.DisplayProgressBar("Creating .unitypackage", "Collecting Files for Package: " + root, counter / (float) length);
                        counter++;
                        Profiler.BeginSample("Collecting Files for Package: " + root);
                        
                        var fullPath = Path.GetFullPath(root);
                        CollectIgnoreFiles(root);
                        
                        // include all hidden directories (end with ~)
                        foreach (var directory in new DirectoryInfo(root).GetDirectories("*", SearchOption.AllDirectories))
                        {
                            try
                            {
                                if(directory.Name.StartsWith(".", StringComparison.Ordinal))
                                    continue;
                                
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
                                        if(defaultNpmIgnore.Contains(file.Name))
                                            continue;
                                        
                                        if (file.Extension.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                                            continue;

                                        if (currentUploadConfig && currentUploadConfig.respectIgnoreFiles && IsIgnored(file.FullName)) continue;

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
                    
                    EditorUtility.DisplayProgressBar("Creating .unitypackage", "Start Packing to " + fileName, 0.2f);
                    Profiler.BeginSample("Create .unitypackage");
                    var dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/Unity/AssetStoreTools/Export";

                    var guidToFile = new Dictionary<string, string>();
                    foreach(var path in exportPaths.OrderByDescending(x => AssetDatabase.GetMainAssetTypeAtPath(x) != null))
                        UnitypackageExporter.AddToUnityPackage(path, dir, ref guidToFile);
                    
                    var compressionStrength = currentUploadConfig ? currentUploadConfig.compressionStrength : Zipper.CompressionStrength.Normal;
                    if (!Zipper.TryCreateTgz(dir, fileName, compressionStrength))
                    {
                        Profiler.EndSample();
                        throw new Exception("Failed creating .unitypackage " + fileName);
                    }
                    EditorUtility.RevealInFinder(fileName);
                    Directory.Delete(dir, true);
                    Profiler.EndSample();
                    EditorUtility.DisplayProgressBar("Creating .unitypackage", "Done", 1f);
                    EditorUtility.ClearProgressBar();

                    // Note: "filename" is actually "relative-folder + filename" (it's wrongly named), so we introduce "outputPreDir" as the (folder containing the folder with the file)
                    var outputPreDir = Path.GetDirectoryName( Application.dataPath );
                    Debug.Log("Created .unitypackage in " + (sw.ElapsedMilliseconds / 1000f).ToString("F2") + "s at: \""+outputPreDir+"/"+ fileName+"\"" );
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

        internal static bool PatchingSupported()
        {
            // Harmony is not supported on Apple Silicon right now; see
            // https://github.com/pardeike/Harmony/issues/424
            // blocked by https://github.com/MonoMod/MonoMod/issues/90
            var isAppleSilicon = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                                 && RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

            return !isAppleSilicon;
        }
    }
}