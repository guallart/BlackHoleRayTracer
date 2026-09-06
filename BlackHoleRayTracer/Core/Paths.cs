using BlackHoleRayTracer.Configuration;

using System.Runtime.CompilerServices;

namespace BlackHoleRayTracer.Core;

public static class Paths
{
  private static string? _framesDirectory;
  public static string FramesDirectory
  {
    get
    {
      if (_framesDirectory == null)
      {
        _framesDirectory = EnsureDirectory(Config.Output.FramesDirectory);
        DeleteAllFilesInDirectory(_framesDirectory);
      }

      return _framesDirectory;
    }
  }
  
  private static string? _outputDirectory;
  public static string OutputDirectory
  {
    get
    {
      _outputDirectory ??= EnsureDirectory(Config.Output.OutputDirectory);
      return _outputDirectory;
    }
  }

  private static string? _frameExtension;
  public static string FrameExtension
  {
    get
    {
      _frameExtension ??= Config.Output.FrameExtension.ToString().ToLower();
      return _frameExtension;
    }
  }

  private static string? _outputPath;
  public static string OutputPath
  {
    get
    {
      if (_outputPath == null)
      {
        bool isImageMode = Config.Mode == RunMode.Image;
        string outExtension = isImageMode ? FrameExtension : "mp4";
        string outFileName = $"{Config.Output.FileName}.{outExtension}";
        _outputPath = Path.Combine(OutputDirectory, outFileName);
      }

      return _outputPath;
    }
  }

  private static string? _solutionDirectory;
  private static string SolutionDirectory
  {
    get
    {
      _solutionDirectory ??= GetSolutionDirectory();
      return _solutionDirectory;
    }
  }

  public static string GetSolutionDirectory([CallerFilePath] string sourceFilePath = "")
  {
    var directory = new DirectoryInfo(Path.GetDirectoryName(sourceFilePath)!);

    while (directory != null && directory.GetFiles("*.slnx").Length == 0 && directory.GetFiles("*.sln").Length == 0)
    {
      directory = directory.Parent;
    }

    if (directory == null)
      throw new FileNotFoundException("Could not find any .sln file.");

    return directory.FullName;
  }

  /// <summary>
  /// Turns a configured path into an absolute one. Relative values are interpreted against the
  /// solution root, never against the current working directory, so output lands in the same
  /// place whether the program is started from the IDE, from <c>dotnet run</c> or from a shell.
  /// Absolute values are returned untouched.
  /// </summary>
  public static string Resolve(string configuredPath) =>
      Path.IsPathRooted(configuredPath)
          ? Path.GetFullPath(configuredPath)
          : Path.GetFullPath(Path.Combine(SolutionDirectory, configuredPath));

  /// <summary>Resolves a directory and creates it if missing. Returns the absolute path.</summary>
  public static string EnsureDirectory(string configuredPath)
  {
    string directory = Resolve(configuredPath);
    Directory.CreateDirectory(directory);
    return directory;
  }

  /// <summary>
  /// Deletes all files in the specified directory.
  /// </summary>
  public static void DeleteAllFilesInDirectory(string directoryPath)
  {
    if (!Directory.Exists(directoryPath))
      return;

    DirectoryInfo di = new DirectoryInfo(directoryPath);

    foreach (FileInfo file in di.GetFiles())
      file.Delete();
  }
}