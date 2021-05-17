using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace Needle.PackageTools
{
	public static class Zipper
	{
		// [InitializeOnLoadMethod]
		[MenuItem("Tgz/Zip package")]
		private static void Zip()
		{
			// Client.GetCachedPackages()

			var target = @"C:\Users\wiessler\Downloads\tesgt\out\arsim.tgz";
			// if (TryCreateTgz(@"C:\Users\wiessler\Downloads\tesgt\archtemp", @"C:\git\npm\development\PackagePlayground-2020.3\Packages/test.tgz"))
			if(TryCreateUnityPackageForPackage(@"C:\git\npm\development\ar-simulation\package", "arsim", target))
			{
				Debug.Log("Did zip");
				// File.Move(target, Path.ChangeExtension(target, "unitypackage"));
			}
			else 
				Debug.Log("Did not zip");
		}

		public static bool TryCreateUnityPackageForPackage(string packageDirectory, string packageName, string targetPath)
		{
			var temp = Path.GetTempPath() + "/" + packageName;
			if (Directory.Exists(temp)) Directory.Delete(temp, true);
			Debug.Log(temp);
			CopyFilesRecursively(packageDirectory, temp);
			var res = TryCreateTgz(temp, targetPath);
			if (res) Directory.Delete(temp, true);
			return res;
		}
		
		private static void CopyFilesRecursively(string sourcePath, string targetPath)
		{
			foreach (var dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
			{
				Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
			}
			foreach (var newPath in Directory.GetFiles(sourcePath, "*.*",SearchOption.AllDirectories))
			{
				File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
			}
		}
		
		public static bool TryCreateTgz(string contentDirectory, string outputFilePath)
		{
			if (Zip(contentDirectory, Directory.GetFiles(contentDirectory, "*.*", SearchOption.AllDirectories), outputFilePath))
				return true;
			return false;
		}
		
		
		private static bool Zip(string directoryPath, IEnumerable<string> filenames, string outputFilePath)
		{
			var zipper = Get7zPath();
			outputFilePath = Path.GetFullPath(outputFilePath);
			
			if(File.Exists(outputFilePath))
				File.Delete(outputFilePath);

			int RunWith(string args)
			{
				Debug.Log(zipper + " " + args);
				var processStartInfo = new ProcessStartInfo(zipper, args);
				// processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
				if (directoryPath != null)
					processStartInfo.WorkingDirectory = directoryPath;
				var process = Process.Start(processStartInfo);
				process?.WaitForExit();
				var res = process?.ExitCode;
				if (res.HasValue && res == 0) return res.Value;
				Debug.LogError("Result: " + res + " for " + processStartInfo);
				return res ?? -1;
			}

			var files = directoryPath + "/*";// string.Join(" ", filenames.Select(f => "\"" + f.Replace("\\", "/") + "\"").ToArray());
			var tarPath = Path.GetDirectoryName(outputFilePath) + "/archtemp.tar";
			if (File.Exists(tarPath)) File.Delete(tarPath);
			var args_tar = $"a -t7z \"{tarPath}\" \"{files}\" -mx=9";
			var code = RunWith(args_tar);
			if (code == 0)
			{
				var args_tgz = $"a {outputFilePath} {tarPath}";
				code = RunWith(args_tgz);
				Debug.Log(code);
				var success = code == 0;
				if(success) File.Delete(tarPath);
			}
			return code == 0;
		}

        private static string Get7zPath()
        {
#if (UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX)
            string execFilename = "7za";
#else
            string execFilename = "7z.exe";
#endif
            string zipper = EditorApplication.applicationContentsPath + "/Tools/" + execFilename;
            if (!File.Exists(zipper))
                throw new FileNotFoundException("Could not find " + zipper);
            return zipper;
        }
	}
}