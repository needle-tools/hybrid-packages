using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Needle.PackageTools;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class PackageExport
{
    [Serializable]
    class PackageData
    {
        public string name = "com.my.fake.package";
        public string version = "1.0.0";
        public string displayName = "Fake Package";
    }

    string GetPackageName([CallerMemberName] string memberName = "")
    {
        return memberName + ".unitypackage";
    }

    private string TemporaryPackageDirectory => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/Unity/AssetStoreTools/Export";
    private string LocalPackageDirectory => "Packages/com.my.fake.package/";
    
    void PreparePackage(out string localDataDirectory)
    {
        Directory.CreateDirectory(LocalPackageDirectory);
        File.WriteAllText(LocalPackageDirectory + "package.json", JsonUtility.ToJson(new PackageData(), true));

        localDataDirectory = LocalPackageDirectory + "data/";
        Directory.CreateDirectory(localDataDirectory);
    }

    void CreatePackage(string temporaryPackageDir, string outputFileName)
    {
        if (!Zipper.TryCreateTgz(temporaryPackageDir, outputFileName))
        {
            UnityEngine.Profiling.Profiler.EndSample();
            throw new Exception("Failed creating .unitypackage " + outputFileName);
        }
    }

    [TearDown]
    public void TearDown()
    {
        Debug.Log("Tear Down Called");
        Directory.Delete(TemporaryPackageDirectory, true);
        Directory.Delete(LocalPackageDirectory, true);
    }
    
    // A UnityTest behaves like a coroutine in Play Mode. In Edit Mode you can use
    // `yield return null;` to skip a frame.
    [UnityTest]
    public IEnumerator FilesHaveSameExistingGuids_ShouldFail()
    {
        var fileName = GetPackageName();
        PreparePackage(out var dataDir);
        
        var guidToFile = new Dictionary<string, string>();

        var guid = Guid.NewGuid();

        try
        {
            for (int i = 0; i < 2; i++)
            {
                var file = $"{dataDir}file_{i}.txt";
                // unique content
                File.WriteAllText(file, $"content:{i}");
                // create meta
                File.WriteAllText(file + ".meta", "guid: " + guid.ToString("N"));
                UnitypackageExporter.AddToUnityPackage(file, TemporaryPackageDirectory, ref guidToFile);
            }
        }
        catch (ArgumentException)
        {
            Assert.Pass("Exception was thrown when adding duplicate GUID");
        }
        
        Assert.Fail("We shouldn't get here!");
        
        CreatePackage(TemporaryPackageDirectory, fileName);
        
		yield return null;
    }

    [UnityTest]
    public IEnumerator FilesHaveSameContent()
    {
        var fileName = GetPackageName();
        PreparePackage(out var dataDir);
        
        var guidToFile = new Dictionary<string, string>();
        
        for (int i = 0; i < 23; i++)
        {
            var file = $"{dataDir}file_{i}.txt";
            File.WriteAllText(file, $"content:{i % 4}"); // some files have the same content
            UnitypackageExporter.AddToUnityPackage(file, TemporaryPackageDirectory, ref guidToFile);
        }
        
        CreatePackage(TemporaryPackageDirectory, fileName);
        
        yield return null;
    }
    
    [UnityTest]
    public IEnumerator GeneratedGuidConflictsWithAssetDatabaseGuid()
    {
        var fileName = GetPackageName();
        PreparePackage(out var dataDir);
        
        var guidToFile = new Dictionary<string, string>();

        var existingFile = "Packages/com.needle.upm-in-unitypackage/Tests/Data/ImageWithGuidConflict.png";
        var newFile = $"{dataDir}file_with_content_collision.txt";
        File.WriteAllText(newFile, $"content:very-colliding-content"); // produces GUID 58147484c7ca7af8bf7d69ec3916dc90

        UnitypackageExporter.AddToUnityPackage(existingFile, TemporaryPackageDirectory, ref guidToFile);
        UnitypackageExporter.AddToUnityPackage(newFile, TemporaryPackageDirectory, ref guidToFile);
        
        Debug.Log(string.Join("\n", guidToFile.Select(x => x.Key + ": " + x.Value)));

        CreatePackage(TemporaryPackageDirectory, fileName);
        
        yield return null;
    }
    
        
    [UnityTest]
    public IEnumerator AssetDatabaseGuidAddedAfterGeneratedGuid_ShouldFail()
    {
        var fileName = GetPackageName();
        PreparePackage(out var dataDir);
        
        var guidToFile = new Dictionary<string, string>();

        var existingFile = "Packages/com.needle.upm-in-unitypackage/Tests/Data/ImageWithGuidConflict.png";
        var newFile = $"{dataDir}file_with_content_collision.txt";
        File.WriteAllText(newFile, $"content:very-colliding-content"); // produces GUID 58147484c7ca7af8bf7d69ec3916dc90

        UnitypackageExporter.AddToUnityPackage(newFile, TemporaryPackageDirectory, ref guidToFile);

        try
        {
            UnitypackageExporter.AddToUnityPackage(existingFile, TemporaryPackageDirectory, ref guidToFile);
        }
        catch (ArgumentException)
        {
            Assert.Pass("Exception was thrown when adding duplicate GUID");
        }
        
        Assert.Fail("We shouldn't get here!");
        
        Debug.Log(string.Join("\n", guidToFile.Select(x => x.Key + ": " + x.Value)));
        
        CreatePackage(TemporaryPackageDirectory, fileName);
        
        yield return null;
    }

    [UnityTest]
    public IEnumerator MultipleIdenticalFilePaths_ShouldFail()
    {
        var fileName = GetPackageName();
        PreparePackage(out var dataDir);
        
        var guidToFile = new Dictionary<string, string>();
            
        var newFile = $"{dataDir}file_with_content_collision.txt";
        File.WriteAllText(newFile, $"content:very-colliding-content"); // produces GUID 58147484c7ca7af8bf7d69ec3916dc90
        
        UnitypackageExporter.AddToUnityPackage(newFile, TemporaryPackageDirectory, ref guidToFile);
        try
        {
            UnitypackageExporter.AddToUnityPackage(newFile, TemporaryPackageDirectory, ref guidToFile);
        }
        catch(ArgumentException)
        {
            Assert.Pass("Exception was thrown when adding duplicate paths");
        }
        
        Assert.Fail("We shouldn't get here!");
        
        CreatePackage(TemporaryPackageDirectory, fileName);
        
        yield return null;
    }
}
