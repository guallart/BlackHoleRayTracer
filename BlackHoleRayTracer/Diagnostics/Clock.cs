using System.Diagnostics;

namespace BlackHoleRayTracer.Diagnostics;

public static class Clock
{
  /// <summary>Runs the given action and returns the execution time in ms.</summary>
  public static double TimeIt(Action action)
  {
    Stopwatch sw = Stopwatch.StartNew();
    action();
    sw.Stop();
    return sw.Elapsed.TotalMilliseconds;
  }
}
