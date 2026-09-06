using BlackHoleRayTracer.Configuration;
using BlackHoleRayTracer.Diagnostics;


namespace BlackHoleRayTracer;

public static class Program
{
  public static int Main()
  {
    try
    {
      ConsoleLog.Setup();

      List<string> problems = ConfigValidation.ValidateCongif();
      if (problems.Count > 0)
      {
        Console.Error.WriteLine("Invalid configuration (check Config.cs):");
        foreach (string p in problems) Console.Error.WriteLine($"  - {p}");
        return 2;
      }

      Shader.Renderer.Render();
      return 0;
    }
    catch (Exception ex)
    {
      Console.Error.WriteLine($"Error: {ex.Message}");
      return 1;
    }
    finally
    {
      ConsoleLog.Shutdown();
    }
  }
}