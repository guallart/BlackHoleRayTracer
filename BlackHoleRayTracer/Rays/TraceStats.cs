namespace BlackHoleRayTracer.Rays;

public readonly struct TraceStats
{
  public int Total { get; init; }
  public int Absorbed { get; init; }
  public int Escaped { get; init; }
  public int HitDisk { get; init; }
  public int MaxStepsReached { get; init; }
  public long TotalSteps { get; init; }
  public int MaxStepsSingleRay { get; init; }
  public double GenerationMs { get; init; }
  public double IntegrationMs { get; init; }

  public double AverageSteps => Total > 0 ? (double)TotalSteps / Total : 0.0;

  public static TraceStats From(RayResult[] results, double generationMs, double integrationMs)
  {
    int absorbed = 0, escaped = 0, disk = 0, maxed = 0, peak = 0;
    long steps = 0;

    for (int i = 0; i < results.Length; i++)
    {
      switch (results[i].Status)
      {
        case RayStatus.Absorbed: absorbed++; break;
        case RayStatus.Escaped: escaped++; break;
        case RayStatus.HitDisk: disk++; break;
        case RayStatus.MaxStepsReached: maxed++; break;
      }
      int st = results[i].StepsTaken;
      steps += st;
      if (st > peak) peak = st;
    }

    return new TraceStats
    {
      Total = results.Length,
      Absorbed = absorbed,
      Escaped = escaped,
      HitDisk = disk,
      MaxStepsReached = maxed,
      TotalSteps = steps,
      MaxStepsSingleRay = peak,
      GenerationMs = generationMs,
      IntegrationMs = integrationMs
    };
  }

  private double Pct(int n) => Total > 0 ? 100.0 * n / Total : 0.0;

  public void Print()
  {
    Console.WriteLine($"Total rays:     {Total,10:N0}");
    Console.WriteLine($"Absorbed:       {Absorbed,10:N0} ({Pct(Absorbed):F1}%)");
    Console.WriteLine($"Escaped:        {Escaped,10:N0} ({Pct(Escaped):F1}%)");
    Console.WriteLine($"Disk:           {HitDisk,10:N0} ({Pct(HitDisk):F1}%)");

    if (MaxStepsReached > 0)
      Console.WriteLine($"MaxSteps (!):  {MaxStepsReached,10:N0} ({Pct(MaxStepsReached):F1}%)  <- possible numerical error");

    Console.WriteLine();
    Console.WriteLine($"Integration steps: {TotalSteps:N0} (avg {AverageSteps:F0}/ray, max {MaxStepsSingleRay:N0})");
    Console.WriteLine($"Ray generation:    {GenerationMs:F0} ms");
    Console.WriteLine($"Total integration time: {IntegrationMs:F0} ms");

    if (IntegrationMs > 0)
      Console.WriteLine($"Throughput: {Total / IntegrationMs / 1000.0:F2} Mrays/s, {TotalSteps / IntegrationMs / 1000.0:F1} Msteps/s");
  }
}
