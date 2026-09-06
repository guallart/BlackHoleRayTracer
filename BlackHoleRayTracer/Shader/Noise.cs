namespace BlackHoleRayTracer.Shader;

/// <summary>
/// Deterministic value noise, periodic along the angular axis. The periodicity is
/// functional, not decorative: without it the disk pattern has a visible seam at
/// phi = 0 that rotates with the camera and is obvious in motion.
/// </summary>
public static class Noise
{
  public static float Fbm(float x, float y, int periodY, int octaves)
  {
    float sum = 0.0f, amp = 0.5f, norm = 0.0f;
    int period = Math.Max(periodY, 1);

    for (int o = 0; o < octaves; o++)
    {
      sum += amp * Value(x, y, period);
      norm += amp;
      x *= 2.0f; y *= 2.0f; period *= 2; amp *= 0.5f;
    }
    return norm > 0.0f ? sum / norm : 0.0f;
  }

  private static float Value(float x, float y, int periodY)
  {
    int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
    float fx = x - xi, fy = y - yi;
    float ux = fx * fx * (3.0f - 2.0f * fx);
    float uy = fy * fy * (3.0f - 2.0f * fy);

    float a = Hash(xi, Wrap(yi, periodY));
    float b = Hash(xi + 1, Wrap(yi, periodY));
    float c = Hash(xi, Wrap(yi + 1, periodY));
    float d = Hash(xi + 1, Wrap(yi + 1, periodY));

    return (a + (b - a) * ux) * (1.0f - uy) + (c + (d - c) * ux) * uy;
  }

  private static int Wrap(int v, int period)
  {
    int r = v % period;
    return r < 0 ? r + period : r;
  }

  private static float Hash(int x, int y)
  {
    ulong h = (ulong)(uint)x * 0x9E3779B97F4A7C15UL ^ (ulong)(uint)y * 0xC2B2AE3D27D4EB4FUL;
    h ^= h >> 29; h *= 0xBF58476D1CE4E5B9UL;
    h ^= h >> 32; h *= 0x94D049BB133111EBUL;
    h ^= h >> 31;
    return (h >> 40) * (1.0f / 16777216.0f);
  }
}