using ILGPU;

namespace BlackHoleRayTracer.Shader;

public static class DownSampler
{
  /// <summary>
  /// Reduces a supersampled buffer by an integer factor, averaging in LINEAR space.
  /// Averaging here rather than after tone mapping is what gives correct antialiasing:
  /// the shadow edge and the photon ring span two orders of magnitude, and averaging
  /// them in gamma space darkens the transition.
  /// </summary>
  public static void DownSample(Index1D iDst, ArrayView<float> source, ArrayView<float> dest, int widthDst, int heightDst)
  {
    int factor = Configuration.Config.Shading.Supersample;
    int widthSrc = widthDst * factor;
    int ch = Configuration.Config.ChannelsFull;

    int x = iDst % widthDst;
    int y = iDst / widthDst;

    float r = 0f;
    float g = 0f;
    float b = 0f;
    float a = 0f;

    for (int dy = 0; dy < factor; dy++)
    {
      int row = (((y * factor + dy) * widthSrc) + (x * factor)) * ch;

      for (int dx = 0; dx < factor; dx++)
      {
        r += source[row];
        g += source[row + 1];
        b += source[row + 2];
        a += source[row + 3];
        row += ch;
      }
    }

    float inv = 1.0f / (factor * factor);
    int o = iDst * ch;
    dest[o] = r * inv;
    dest[o + 1] = g * inv;
    dest[o + 2] = b * inv;
    dest[o + 3] = a * inv;
  }
}
