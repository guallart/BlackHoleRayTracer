using BlackHoleRayTracer.Core;

using ILGPU;

namespace BlackHoleRayTracer.Shader;

public class ToneMapper
{
  public static void ToneMap(Index1D iPix, ArrayView<float> linear, ArrayView<byte> rgb)
  {
    int chIn = Configuration.Config.ChannelsFull;
    int i = iPix * chIn; // offset in linear buffer
    int o = iPix * 3;    // offset in output buffer

    rgb[o    ] = EncodeSrgb(ToneMapAces(linear[i]));
    rgb[o + 1] = EncodeSrgb(ToneMapAces(linear[i + 1]));
    rgb[o + 2] = EncodeSrgb(ToneMapAces(linear[i + 2]));
  }

  /// <summary>
  /// ACES filmic curve (Narkowicz approximation). Compresses highlights without clipping,
  /// which is essential here: there are two orders of magnitude of brightness between the
  /// inner and outer edges of the disk, and another factor of ~70 between the approaching
  /// and receding sides from relativistic beaming.
  /// </summary>
  private static float ToneMapAces(float x)
  {
    if (x <= 0.0f) return 0.0f;

    const float A = 2.51f;
    const float B = 0.03f;
    const float C = 2.43f;
    const float D = 0.59f;
    const float E = 0.14f;

    float v = (x * (A * x + B)) / (x * (C * x + D) + E);
    return GpuMath.Clamp(v, 0.0f, 1.0f);
  }

  /// <summary>sRGB transfer encoding (linear -> non-linear).</summary>
  private static byte EncodeSrgb(float linear)
  {
    if (linear <= 0.0f) return 0;
    if (linear >= 1.0f) return 255;
    float v = linear <= 0.0031308f
        ? 12.92f * linear
        : 1.055f * MathF.Pow(linear, 1.0f / 2.4f) - 0.055f;
    return (byte)GpuMath.Clamp((int)(v * 255.0f + 0.5f), 0, 255);
  }
}

