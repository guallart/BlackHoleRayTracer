using BlackHoleRayTracer.Core;

using ILGPU;

namespace BlackHoleRayTracer.Shader;

/// <summary>
/// Colorimetry. Converts black-body temperature to linear sRGB by integrating Planck's law
/// against the CIE 1931 color matching functions, and applies the final tone mapping.
///
/// The whole shading pipeline works in LINEAR sRGB (no gamma). Gamma encoding is applied
/// exactly once, at the end, in <see cref="EncodeSrgb"/>. Blending or averaging colors that
/// are already gamma encoded (when downsampling the supersampled buffer, for instance)
/// darkens the image and desaturates bright edges, which is precisely where the interest
/// lies here: the photon ring and the shadow edge.
/// </summary>
public static class Spectrum
{
  private const int LutSize = 1024;
  private const float LutMinT = 500.0f;
  private const float LutMaxT = 40000.0f;

  // LUT on a logarithmic temperature scale: chromaticity varies smoothly in log T.
  private static readonly float[] Lut = BuildLut();

  /// <summary>
  /// Black-body chromaticity at temperature T, normalized so the largest channel is 1.
  /// Brightness is NOT included here; the caller supplies it, typically as T^4.
  /// </summary>
  public static void BlackBodyChroma(ArrayView<float> lut, float temperature, out float r, out float g, out float b)
  {
    float t = MathF.Log(GpuMath.Clamp(temperature, LutMinT, LutMaxT) / LutMinT)
               / MathF.Log(LutMaxT / LutMinT) * (LutSize - 1);
    int i0 = GpuMath.Clamp((int)t, 0, LutSize - 2);
    float f = (t - i0);

    int a = i0 * 3;
    int c = (i0 + 1) * 3;
    r = lut[a    ] + (lut[c    ] - lut[a    ]) * f;
    g = lut[a + 1] + (lut[c + 1] - lut[a + 1]) * f;
    b = lut[a + 2] + (lut[c + 2] - lut[a + 2]) * f;
  }

  public static float[] BuildLut()
  {
    var lut = new float[LutSize * 3];
    for (int i = 0; i < LutSize; i++)
    {
      float t = LutMinT * MathF.Pow(LutMaxT / LutMinT, i / (LutSize - 1f));
      PlanckToLinearSrgb(t, out float r, out float g, out float b);
      lut[i * 3] = r;
      lut[i * 3 + 1] = g;
      lut[i * 3 + 2] = b;
    }
    return lut;
  }

  private static void PlanckToLinearSrgb(float temperature, out float r, out float g, out float b)
  {
    float x = 0.0f, y = 0.0f, z = 0.0f;

    for (float lambda = 380.0f; lambda <= 780.0f; lambda += 5.0f)
    {
      float s = Planck(lambda, temperature);
      Cie1931(lambda, out float cx, out float cy, out float cz);
      x += s * cx;
      y += s * cy;
      z += s * cz;
    }

    // XYZ (D65) -> linear sRGB
    r = 3.2404542f * x - 1.5371385f * y - 0.4985314f * z;
    g = -0.9692660f * x + 1.8760108f * y + 0.0415560f * z;
    b = 0.0556434f * x - 0.2040259f * y + 1.0572252f * z;

    // Negatives are colors outside the sRGB gamut; clipping them is equivalent to
    // desaturating toward the gamut boundary.
    r = Math.Max(r, 0.0f);
    g = Math.Max(g, 0.0f);
    b = Math.Max(b, 0.0f);

    float max = MathF.Max(r, MathF.Max(g, b));
    if (max > 0.0f) { 
      r /= max;
      g /= max;
      b /= max;
    }
  }

  /// <summary>Planck spectral radiance. lambda in nm, T in K. Arbitrary units.</summary>
  private static float Planck(float lambdaNm, float temperature)
  {
    // c2 = hc/k in nm*K  (standard 2nd radiation constant, scaled to nm)
    const float C2 = 1.4387769e7f;
    // c1' = 2*h*c^2, rescaled so lambda can stay in nm (extra 1e45 factor from nm^5 vs m^5)
    const float C1 = 1.1910429e29f;

    float exponent = C2 / (lambdaNm * temperature);
    if (exponent > 80f) return 0f;   // MathF.Exp overflows past ~88.7

    float l5 = lambdaNm * lambdaNm * lambdaNm * lambdaNm * lambdaNm;
    return C1 / (l5 * (MathF.Exp(exponent) - 1f));
  }

  /// <summary>
  /// CIE 1931 color matching functions, multi-lobe Gaussian fit from
  /// Wyman, Sloan &amp; Shirley (2013). Error &lt; 1%, and no tables to load.
  /// </summary>
  private static void Cie1931(float lambda, out float x, out float y, out float z)
  {
    x = 1.056f * G(lambda, 599.8f, 37.9f, 31.0f)
      + 0.362f * G(lambda, 442.0f, 16.0f, 26.7f)
      - 0.065f * G(lambda, 501.1f, 20.4f, 26.2f);
    y = 0.821f * G(lambda, 568.8f, 46.9f, 40.5f)
      + 0.286f * G(lambda, 530.9f, 16.3f, 31.1f);
    z = 1.217f * G(lambda, 437.0f, 11.8f, 36.0f)
      + 0.681f * G(lambda, 459.0f, 26.0f, 13.8f);
  }

  private static float G(float v, float mu, float sigma1, float sigma2)
  {
    float t = (v - mu) * (v < mu ? 1.0f / sigma1 : 1.0f / sigma2);
    return MathF.Exp(-0.5f * t * t);
  }
}