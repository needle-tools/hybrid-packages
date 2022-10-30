using System.Diagnostics;
using System.IO;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace Needle.HybridPackages
{
	public static class Zipper
	{
		public enum CompressionStrength
		{
			Fastest = 1,
			Fast    = 3,
			Normal  = 5,
			Maximum = 7,
			Ultra   = 9
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

			foreach (var newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
			{
				File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
			}
		}

		public static bool TryCreateTgz(string contentDirectory, string outputFilePath, CompressionStrength strength = CompressionStrength.Normal)
		{
			if (Zip(contentDirectory, outputFilePath, strength))
				return true;
			return false;
		}

		public static bool DebugLog = false;

		private static bool Zip(string directoryPath, string outputFilePath, CompressionStrength strength)
		{
			EditorUtility.DisplayProgressBar("Creating .unitypackage", "Packing " + outputFilePath, 0f);
			
			var zipper = Get7zPath();
			outputFilePath = Path.GetFullPath(outputFilePath);

			if (File.Exists(outputFilePath))
				File.Delete(outputFilePath);

			int RunWith(string args)
			{
				if (DebugLog)
					Debug.Log(zipper + " " + args);
				var processStartInfo = new ProcessStartInfo(zipper, args);
				// processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
				if (directoryPath != null)
					processStartInfo.WorkingDirectory = directoryPath;
				processStartInfo.UseShellExecute = DebugLog;
				processStartInfo.CreateNoWindow = !DebugLog;
				var process = Process.Start(processStartInfo);
				process?.WaitForExit();
				var res = process?.ExitCode;
				if (res.HasValue && res == 0) return res.Value;
				if (DebugLog)
					Debug.LogError("Result: " + res + " for " + processStartInfo);
				return res ?? -1;
			}

			// Command line syntax:
			// https://sevenzip.osdn.jp/chm/cmdline/syntax.htm
			
			// Compression switches:
			// https://sevenzip.osdn.jp/chm/cmdline/switches/method.htm
			
			var files = directoryPath + "/*"; // string.Join(" ", filenames.Select(f => "\"" + f.Replace("\\", "/") + "\"").ToArray());
			var tarPath = Path.GetDirectoryName(outputFilePath) + "/archtemp.tar";
			if (File.Exists(tarPath)) File.Delete(tarPath);
			var args_tar = $"a -ttar \"{tarPath}\" \"{files}\"";
			EditorUtility.DisplayProgressBar("Creating .unitypackage", "Packing .tar file for " + outputFilePath, 0.1f);
			var code = RunWith(args_tar);
			if (code == 0)
			{
				EditorUtility.DisplayProgressBar("Creating .unitypackage", "Packing .tar.gz file for " + outputFilePath + ", compression: " + strength, 0.4f);
				var args_tgz = $"a -tgzip \"{outputFilePath}\" \"{tarPath}\" -mx" + (int) strength;
				if (File.Exists(outputFilePath)) File.Delete(outputFilePath);
				code = RunWith(args_tgz);
				if (DebugLog)
					Debug.Log(code);
				var success = code == 0;
				if (success) File.Delete(tarPath);
			}

			EditorUtility.DisplayProgressBar("Creating .unitypackage", "Done", 1f);
			EditorUtility.ClearProgressBar();
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