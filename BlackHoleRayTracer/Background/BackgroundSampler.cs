using BlackHoleRayTracer.Configuration;

using ILGPU;

public struct BackgroundSampler
{
  public int Width;
  public int Height;

  public const float TileX = 3;
  public const float TileY = 3;

  public readonly int RowStride => Width * Config.ChannelsFull;

  public void Sample(ArrayView<float> texels, float theta, float phi, out float r, out float g, out float b)
  {
    const float InvPi = 1.0f / MathF.PI;
    const float InvTwoPi = 0.5f / MathF.PI;

    float u = phi * InvTwoPi * TileX;
    float v = theta * InvPi * TileY;

    float fx = u * Width - 0.5f;
    float fy = v * Height - 0.5f;

    float fx0 = MathF.Floor(fx);
    float fy0 = MathF.Floor(fy);
    float tx = fx - fx0;
    float ty = fy - fy0;

    int x0 = Wrap((int)fx0, Width);
    int x1 = x0 + 1 == Width ? 0 : x0 + 1;
    int y0 = Wrap((int)fy0, Height);
    int y1 = y0 + 1 == Height ? 0 : y0 + 1;

    int row0 = y0 * RowStride;
    int row1 = y1 * RowStride;
    int c00 = row0 + x0 * Config.ChannelsFull;
    int c10 = row0 + x1 * Config.ChannelsFull;
    int c01 = row1 + x0 * Config.ChannelsFull;
    int c11 = row1 + x1 * Config.ChannelsFull;

    var t = texels;

    r = Bilinear(t[c00], t[c10], t[c01], t[c11], tx, ty);
    g = Bilinear(t[c00 + 1], t[c10 + 1], t[c01 + 1], t[c11 + 1], tx, ty);
    b = Bilinear(t[c00 + 2], t[c10 + 2], t[c01 + 2], t[c11 + 2], tx, ty);
  }

  private static float Bilinear(float v00, float v10, float v01, float v11, float tx, float ty)
  {
    float a = v00 + (v10 - v00) * tx;
    float b = v01 + (v11 - v01) * tx;
    return a + (b - a) * ty;
  }

  private static int Wrap(int i, int n)
  {
    int m = i % n;
    return m < 0 ? m + n : m;
  }
}