using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.VirtualTexturing;
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

        [MenuItem("Test/Export package files")]
        private static void ExportTest()
        {
            var dir = @"C:\Users\wiessler\AppData\Local\Temp\test";
            AddToUnityPackage(@"C:\git\npm\development\PackagePlayground-2020.3\Assets\PackageToolsTesting\AssetReference\TestMaterial.mat", dir);
            AddToUnityPackage(@"C:\git\npm\development\editorpatching\modules\unity-demystify\package\Documentation~\beforeafter.jpg", dir);
            var package = dir + "/package.unitypackage";
            Zipper.TryCreateTgz(dir, package);
            EditorUtility.RevealInFinder(package);
        }

        public static void AddToUnityPackage(string pathToFileOrDirectory, string targetDir)
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

            var isFile = File.Exists(pathToFileOrDirectory);
            if (pathToFileOrDirectory.EndsWith(".meta")) return;
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
                        if (line.StartsWith("guid:"))
                        {
                            guid = line.Substring("guid:".Length).Trim();
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
                        guid = BitConverter.ToString(hash).Replace("-","");//(md5.Hash);
                    }
                }
            }

            if (guid == null) Debug.LogError("No guid for " + pathToFileOrDirectory);
            pathToFileOrDirectory = pathToFileOrDirectory.Replace("\\", "/");
            var projectPath = Path.GetFullPath(Application.dataPath + "/../").Replace("\\", "/");
            var relPath = pathToFileOrDirectory;
            if (relPath.StartsWith(projectPath)) relPath = relPath.Substring(projectPath.Length);

            var outDir = targetDir + "/" + guid;
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
            var pathNameFilePath = outDir + "/pathname";
            if(File.Exists(pathNameFilePath)) File.Delete(pathNameFilePath);
            File.WriteAllText(pathNameFilePath, relPath);
            if(isFile) File.Copy(pathToFileOrDirectory, outDir + "/asset", true);
            if (hasMetaFile) File.Copy(metaPath, outDir + "/asset.meta", true);

            var preview = AssetPreview.GetAssetPreviewFromGUID(guid);
            // var icon = AssetDatabase.GetCachedIcon(relPath) as Texture2D;
            // this is e.g. icon for material, not what we want
            // if (!icon) icon = InternalEditorUtility.GetIconForFile(relPath);
            if (preview)
            {
                var copy = new Texture2D(preview.width, preview.height, preview.format, false);//, preview.graphicsFormat, preview.mipmapCount, TextureCreationFlags.None);
                Graphics.CopyTexture(preview, 0, 0, copy, 0, 0);
                var bytes = copy.EncodeToPNG();
                if (bytes != null && bytes.Length > 0)
                {
                    File.WriteAllBytes(outDir + "/preview.png", bytes);
                }
            }
        }
    }
}