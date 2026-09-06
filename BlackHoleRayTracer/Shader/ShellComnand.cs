using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BlackHoleRayTracer.Shader;

public static class ShellCommand
{
  public static int RunCommand(string command)
  {
    string shell;
    string shellArgs;

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
      shell = "cmd.exe";
      shellArgs = $"/c \"{command}\"";
    }
    else
    {
      shell = "/bin/sh";
      shellArgs = $"-c \"{command.Replace("\"", "\\\"")}\"";
    }

    var psi = new ProcessStartInfo
    {
      FileName = shell,
      Arguments = shellArgs,
      UseShellExecute = false,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      CreateNoWindow = true
    };

    Console.WriteLine("Running command:");
    Console.WriteLine("  " + command);
    Console.WriteLine();

    using var process = new Process { StartInfo = psi };

    process.OutputDataReceived += (_, e) =>
    {
      if (e.Data != null) Console.Out.WriteLine(e.Data);
    };
    process.ErrorDataReceived += (_, e) =>
    {
      if (e.Data != null) Console.Error.WriteLine(e.Data);
    };

    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    process.WaitForExit();

    return process.ExitCode;
  }
}
