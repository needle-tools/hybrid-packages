using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Needle.HybridPackages
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
        
#if HAVE_AS_TOOLS_PACKAGE
        [InitializeOnLoadMethod]
        static void SetKeyword()
        {
            const string define = "UNITY_ASTOOLS_EXPERIMENTAL";
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.activeBuildTargetGroup).Split(';');
            if (!defines.Contains(define))
            {
                var newDefines = new string[defines.Length + 1];
                Array.Copy(defines, newDefines, defines.Length);
                newDefines[newDefines.Length - 1] = define;
                PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.activeBuildTargetGroup, string.Join(";", newDefines));
                AssetDatabase.Refresh();
            }
        }
#endif

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

        // [MenuItem("Test/Export package files")]
        // private static void ExportTest()
        // {
        //     var dir = @"C:\Users\wiessler\AppData\Local\Temp\test";
        //     AddToUnityPackage(@"C:\git\npm\development\PackagePlayground-2020.3\Assets\PackageToolsTesting\AssetReference\TestMaterial.mat", dir);
        //     AddToUnityPackage(@"C:\git\npm\development\editorpatching\modules\unity-demystify\package\Documentation~\beforeafter.jpg", dir);
        //     var package = dir + "/package.unitypackage";
        //     Zipper.TryCreateTgz(dir, package);
        //     EditorUtility.RevealInFinder(package);
        // }

        // [MenuItem("Test/Scan Folder for preview.png")]
        // private static void ScanFolder()
        // {
        //     var path = EditorUtility.OpenFolderPanel("Select unpacked unitypackage root", "", "");
        //     if (!string.IsNullOrEmpty(path))
        //     {
        //         var di = new DirectoryInfo(path);
        //         
        //         var distinctExtensionsWithPreview = di.GetDirectories("*", SearchOption.TopDirectoryOnly)
        //             .Where(x => x.GetFiles("preview.png").Any())
        //             .Select(x => File.ReadAllText(x.GetFiles("pathname").First().FullName))
        //             .Select(Path.GetExtension)
        //             .Distinct();
        //         
        //         var distinctExtensionsWithoutPreview = di.GetDirectories("*", SearchOption.TopDirectoryOnly)
        //             .Where(x => !x.GetFiles("preview.png").Any())
        //             .Select(x => File.ReadAllText(x.GetFiles("pathname").First().FullName))
        //             .Select(Path.GetExtension)
        //             .Distinct();
        //         
        //         Debug.Log("Extensions with preview.png files:\n" + string.Join("\n", distinctExtensionsWithPreview));
        //         Debug.Log("Extensions without preview.png files:\n" + string.Join("\n", distinctExtensionsWithoutPreview));
        //     }
        // }

        private static bool IncludePreviewImages = true;
        
        // TODO figure out a better way / find someone who knows which assets actually get preview.png generated
        // derived experimentally for now by exporting a giant .unitypackage from an entire project and checking what happens.
        // reason for a block list and not an allow list: ScriptedImporters can be for anything.
        private static string[] extensionsWithoutThumbnails = new string[]
        {
            ".shader",
            ".cs",
            ".anim",
            ".unity",
            ".asset",
            ".playable",
            ".lighting",
            ".usdc",
            ".usda",
            ".usd",
            ".usdz",
            ".asmdef",
            ".asmref",
            ".bundle",
            ".controller",
            ".dll",
            ".md",
            ".xml",
            ".json",
            ".txt",
            ".manifest",
            ".jslib",
            ".java",
            ".ttf",
            ".exp",
            ".usdz",
            ".mtl",
            ".fbx",
            ".fbm",
            ".pdb",
            ".usd",
            ".prefab",
            ".assbin",
            ".mm",
            ".iobj",
            ".lib",
            ".overrideController",
            ".so",
            ".plist",
            ".zip",
            ".7z",
            ".unitypackage",
            ".ipdb",
            ".url",
            ".shadergraph"
        };
        
        public static void AddToUnityPackage(string pathToFileOrDirectory, string targetDir, ref Dictionary<string, string> guidToFile)
        {
            if (!File.Exists(pathToFileOrDirectory) && !Directory.Exists(pathToFileOrDirectory))
            {
                Debug.LogError("File " + pathToFileOrDirectory + " does not exist");
                return;
            }
            if (Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            if (guidToFile.ContainsValue(pathToFileOrDirectory))
            {
                throw new ArgumentException($"Duplicate Path exported! {pathToFileOrDirectory}");
            }
                
            var isFile = File.Exists(pathToFileOrDirectory);
            if (pathToFileOrDirectory.EndsWith(".meta", StringComparison.Ordinal)) return;
            var metaPath = pathToFileOrDirectory + ".meta";
            var guid = default(string);
            var hasMetaFile = File.Exists(metaPath);
            if (hasMetaFile)
            {
                using (var reader = new StreamReader(metaPath))
                {
                    var line = reader.ReadLine();
                    while(line != null)
                    {
                        if (line.StartsWith("guid:", StringComparison.Ordinal))
                        {
                            guid = line.Substring("guid:".Length).Trim();
                            if (guidToFile.ContainsKey(guid))
                                throw new ArgumentException($"Duplicate GUID in AssetDatabase ({guid})?! Existing: {guidToFile[guid]}, new: {pathToFileOrDirectory}");
                            guidToFile.Add(guid, pathToFileOrDirectory);
                            break;
                        }
                        line = reader.ReadLine();
                    }
                }
            }
            else
            {
                if (File.Exists(pathToFileOrDirectory))
                {
                    var bytes = File.ReadAllBytes(pathToFileOrDirectory);
                    using (var md5 = MD5.Create()) {
                        md5.TransformFinalBlock(bytes, 0, bytes.Length);
                        var hash = md5.Hash;
                        // guid = BitConverter.ToString(hash).Replace("-","");//(md5.Hash);
                        
                        guid = new Guid(hash).ToString("N");
                        int maxTries = 200;
                        while (guidToFile.ContainsKey(guid))
                        {
                            Debug.LogWarning($"GUID collision ({guid}), Existing: {guidToFile[guid]}, new: {pathToFileOrDirectory}");
                            hash[0]++;
                            guid = new Guid(hash).ToString("N");
                            maxTries--;
                            if (maxTries < 0)
                            {
                                throw new ArgumentException($"GUID collision on file: {pathToFileOrDirectory} with GUID {guid} even after 200 attempts of finding a new one. Aborting.");
                            }
                        }
                        guidToFile.Add(guid, pathToFileOrDirectory);
                    }
                }
            }

            if (guid == null) Debug.LogError("No guid for " + pathToFileOrDirectory);
            pathToFileOrDirectory = pathToFileOrDirectory.Replace("\\", "/");
            var projectPath = Path.GetFullPath(Application.dataPath + "/../").Replace("\\", "/");
            var relPath = pathToFileOrDirectory;
            if (relPath.StartsWith(projectPath, StringComparison.Ordinal)) relPath = relPath.Substring(projectPath.Length);

            var outDir = targetDir + "/" + guid;
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
            var pathNameFilePath = outDir + "/pathname";
            if(File.Exists(pathNameFilePath)) File.Delete(pathNameFilePath);
            File.WriteAllText(pathNameFilePath, relPath);
            if(isFile) File.Copy(pathToFileOrDirectory, outDir + "/asset", true);
            if (hasMetaFile) File.Copy(metaPath, outDir + "/asset.meta", true);

            if(IncludePreviewImages && isFile && !extensionsWithoutThumbnails.Contains(Path.GetExtension(pathToFileOrDirectory).ToLowerInvariant()))
            {
#if UNITY_2020_1_OR_NEWER
                var preview = AssetPreview.GetAssetPreviewFromGUID(guid);
#else
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var asset = string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                var preview = asset ? AssetPreview.GetAssetPreview(asset) : default;
#endif
                if (preview)
                {
                    var thumbnailWidth = Mathf.Min(preview.width, 128);
                    var thumbnailHeight = Mathf.Min(preview.height, 128);
                    var rt = RenderTexture.GetTemporary(thumbnailWidth, thumbnailHeight, 0, RenderTextureFormat.Default, RenderTextureReadWrite.sRGB);
                
                    var copy = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false);//, preview.graphicsFormat, preview.mipmapCount, TextureCreationFlags.None);
                
                    RenderTexture.active = rt;
                    GL.Clear(true, true, new Color(0, 0, 0, 0));
                    Graphics.Blit(preview, rt);
                    copy.ReadPixels(new Rect(0, 0, copy.width, copy.height), 0, 0, false);
                    copy.Apply();
                    RenderTexture.active = null;
                    
                    var bytes = copy.EncodeToPNG();
                    if (bytes != null && bytes.Length > 0)
                    {
                        File.WriteAllBytes(outDir + "/preview.png", bytes);
                    }
                    
                    RenderTexture.ReleaseTemporary(rt);
                }
            }
        }
    }
}