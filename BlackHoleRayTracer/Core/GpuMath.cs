namespace BlackHoleRayTracer.Core;

public static class GpuMath
{
  public static float Clamp(float value, float min, float max)
  {
    if (value < min) return min;
    if (value > max) return max;
    return value;
  }

  public static int Clamp(int value, int min, int max)
  {
    if (value < min) return min;
    if (value > max) return max;
    return value;
  }

  public static int Round(float value)
  {
    return value >= 0 ? (int)(value + 0.5f) : (int)(value - 0.5f);
  }
}

