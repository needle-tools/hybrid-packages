using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Needle.PackageTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class PackageExport
{
    // A Test behaves as an ordinary method
    [Test]
    public void PackageExportSimple()
    {
        // Use the Assert class to test conditions
    }

    [System.Serializable]
    class PackageData
    {
        public string name = "com.my.fake.package";
        public string version = "1.0.0";
        public string displayName = "Fake Package";
    }
    
    // A UnityTest behaves like a coroutine in Play Mode. In Edit Mode you can use
    // `yield return null;` to skip a frame.
    [UnityTest]
    public IEnumerator PackageExportAndImport()
    {
		var dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/Unity/AssetStoreTools/Export";
        var fileName = "tempPackage.unitypackage";

        // create fake package
        var root = "Packages/com.my.fake.package/";
        Directory.CreateDirectory(root);
        File.WriteAllText(root + "package.json", JsonUtility.ToJson(new PackageData(), true));

        var dataDir = root + "data/";
        Directory.CreateDirectory(dataDir);
        
        // create many fake files without meta information
        for (int i = 0; i < 10000; i++)
        {
            var file = $"{dataDir}file_{i}.txt";
            File.WriteAllText(file, $"content:{i}");
            UnitypackageExporter.AddToUnityPackage(file, dir);
        }
        
        // create fake meta ?
        
        
        // add temp files
		
 		if (!Zipper.TryCreateTgz(dir, fileName))
        {
            UnityEngine.Profiling.Profiler.EndSample();
            throw new Exception("Failed creating .unitypackage " + fileName);
        }
        
        EditorUtility.RevealInFinder(fileName);
        Directory.Delete(dir, true);
		yield return null;
    }
}
