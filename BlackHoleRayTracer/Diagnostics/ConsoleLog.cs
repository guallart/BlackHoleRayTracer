using BlackHoleRayTracer.Configuration;
using BlackHoleRayTracer.Core;
using BlackHoleRayTracer.Rays;

using System.Diagnostics;

using static BlackHoleRayTracer.Configuration.Config;

namespace BlackHoleRayTracer.Diagnostics;

public static class ConsoleLog
{
  private static TextWriter _originalStdout;
  private static TextWriter _originalStderr;
  private static StreamWriter _stdoutLog;
  private static StreamWriter _stderrLog;
  private static TeeTextWriter _teeOut;
  private static TeeTextWriter _teeErr;

  public static void Setup()
  {
    string logDir = Paths.EnsureDirectory(Config.Output.OutputDirectory);
    string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");

    _stdoutLog = new StreamWriter(Path.Combine(logDir, $"stdout_{ts}.log")) { AutoFlush = true };
    _stderrLog = new StreamWriter(Path.Combine(logDir, $"stderr_{ts}.log")) { AutoFlush = true };

    _originalStdout = Console.Out;
    _originalStderr = Console.Error;

    _teeOut = new TeeTextWriter(_originalStdout, _stdoutLog);
    _teeErr = new TeeTextWriter(_originalStderr, _stderrLog);

    Console.SetOut(_teeOut);
    Console.SetError(_teeErr);

    WarnIfDebugBuild();
  }

  public static void Shutdown()
  {
    Console.SetOut(_originalStdout);
    Console.SetError(_originalStderr);
    _teeOut?.Dispose();
    _teeErr?.Dispose();
    _stdoutLog?.Dispose();
    _stderrLog?.Dispose();
  }

  [Conditional("DEBUG")]
  private static void WarnIfDebugBuild()
  {
    const string message = "WARNING: Debug build. The integration loop runs roughly five times slower. Switch to Release before rendering.";
    int width = message.Length + 4;

    Console.WriteLine("╔" + new string('═', width) + "╗");
    Console.WriteLine("║  " + message + "  ║");
    Console.WriteLine("╚" + new string('═', width) + "╝");
    Console.WriteLine();
  }

  public static void LogFrame(int frame, TraceStats stats, double totalFrameMS, double totalElapsedSeconds)
  {
    Console.Write($"  frame {frame + 1} / {Video.Frames}  |  {totalFrameMS:F0} ms  |  {stats.AverageSteps:F0} steps/ray");

    if (frame % 10 == 0 || frame == Video.Frames - 1)
    {
      double done = (frame + 1.0) / (double)Video.Frames;
      string elapsed = FormatSeconds((int)totalElapsedSeconds);
      double etaSecs = totalElapsedSeconds * (1.0 - done) / Math.Max(done, 1e-9);
      string eta = FormatSeconds((int)etaSecs);
      Console.WriteLine($"  |  {done:P0}  |  {elapsed} elapsed  |  {eta} remaining");
    }
    else
    {
      Console.WriteLine();
    }
  }

  private static string FormatSeconds(int seconds)
  {
    int hours = seconds / 3600;
    int minutes = (seconds % 3600) / 60;
    int secs = seconds % 60;

    return $"{hours:D2}:{minutes:D2}:{secs:D2}";
  }
}
