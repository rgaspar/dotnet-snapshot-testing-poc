using System.Runtime.CompilerServices;

namespace TestPOC.TestInfrastructure.IO;

/// <summary>
/// Path resolver for snapshot files. Uses [CallerFilePath] so relative paths
/// resolve against the calling test file's directory, and absolute-looking paths
/// (leading '/') resolve against the calling project's root.
/// </summary>
public static class ProjectFileLookup
{
	public static string ReadAllText(string relativePath, [CallerFilePath] string baseFilePath = null!)
	{
		return File.ReadAllText(GetProjectPathInternal(relativePath, baseFilePath));
	}

	public static bool Exists(string relativePath, [CallerFilePath] string baseFilePath = null!)
	{
		return File.Exists(GetProjectPathInternal(relativePath, baseFilePath));
	}

	public static void WriteAllText(string relativePath, string content, [CallerFilePath] string baseFilePath = null!)
	{
		var path = GetProjectPathInternal(relativePath, baseFilePath);
		var dir = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
		{
			Directory.CreateDirectory(dir);
		}

		File.WriteAllText(path, content);
	}

	public static string GetProjectPath(string relativePath, [CallerFilePath] string baseFilePath = null!)
	{
		return GetProjectPathInternal(relativePath, baseFilePath);
	}

	private static string GetProjectPathInternal(string relativePath, string baseFilePath)
	{
		if (relativePath.StartsWith('/') || relativePath.StartsWith('\\'))
		{
			var projectDirectory = FindProjectDirectory(baseFilePath);
			return Path.Combine(projectDirectory, relativePath.TrimStart('/', '\\'));
		}

		var callerDir = Path.GetDirectoryName(baseFilePath)!;
		return Path.Combine(callerDir, relativePath);
	}

	private static string FindProjectDirectory(string baseFilePath)
	{
		var dir = new DirectoryInfo(Path.GetDirectoryName(baseFilePath)!);
		while (dir is not null && dir.GetFiles("*.csproj").Length == 0)
		{
			dir = dir.Parent;
		}

		return dir?.FullName ?? throw new InvalidOperationException($"No .csproj found walking up from {baseFilePath}");
	}
}
