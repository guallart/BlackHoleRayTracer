using BlackHoleRayTracer.Background;
using BlackHoleRayTracer.Core;
using BlackHoleRayTracer.Rays;

using ILGPU;

namespace BlackHoleRayTracer.Shader;

/// <summary>
/// Converts <c>RayResult[]</c> into a LINEAR sRGB framebuffer (float, 3 channels).
/// </summary>
public static class Shader
{
  public static void Shade(
    Index1D i,
    ArrayView<RayResult> rays,
    ArrayView<float> texels,
    ArrayView<float> pixels,
    ArrayView<float> lut,
    BackgroundSampler bgSampler,
    float profileMax
    )
  {
    RayResult ray = rays[i];
    long p = i * Configuration.Config.ChannelsFull;
    float r, g, b;

    if (Configuration.Config.Back.BackType == BackgroundType.Debug)
    {
      switch (ray.Status)
      {
        case RayStatus.Absorbed:        r = 0.05f; g = 0.0f;  b = 0.0f;  break;
        case RayStatus.HitDisk:         r = 1.0f;  g = 0.55f; b = 0.1f;  break;
        case RayStatus.Escaped:         r = 0.0f;  g = 0.12f; b = 0.30f; break;
        case RayStatus.MaxStepsReached: r = 1.0f;  g = 0.0f;  b = 1.0f;  break;
      }
      pixels[p] = r;
      pixels[p + 1] = g;
      pixels[p + 2] = b;
      return;
    }

    switch (ray.Status)
    {
      case RayStatus.HitDisk:
        ShadeDisk(ray, lut, profileMax, out r, out g, out b);
        pixels[p    ] = r;
        pixels[p + 1] = g;
        pixels[p + 2] = b;
        return;

      case RayStatus.Escaped:
      case RayStatus.MaxStepsReached:
        float theta = ray.EscapeTheta;
        float phi = ray.EscapePhi;

        if (float.IsNaN(theta) || float.IsNaN(phi))
        {
          pixels[p    ] = 0.0f;
          pixels[p + 1] = 0.0f;
          pixels[p + 2] = 0.0f;
          return; 
        }

        bgSampler.Sample(texels, theta, phi, out r, out g, out b);
        float k = Configuration.Config.Shading.SkyBrightness;
        pixels[p    ] = r * k;
        pixels[p + 1] = g * k;
        pixels[p + 2] = b * k;
        return;
        

      default:
        // Absorbed: the horizon does not emit. Exact black, not "near black".
        pixels[p    ] = 0.0f;
        pixels[p + 1] = 0.0f;
        pixels[p + 2] = 0.0f;
        return;
    }
  }

  /// <summary>
  /// Disk emission as seen by the distant observer.
  ///
  /// Combined redshift factor (Doppler + gravitational) for a circular Keplerian
  /// orbit in the Schwarzschild equatorial plane:
  ///
  ///     u^t = 1/sqrt(1 - 3M/r),   Omega = sqrt(M/r^3)
  ///     E_emitted = -p_mu u^mu = u^t (E - Omega*L_z)
  ///     g = E_observed / E_emitted = sqrt(1 - 3M/r) / (1 - Omega*b_z),   b_z = L_z/E
  ///
  /// The disk emits as a black body at T_em(r). Since I_nu/nu^3 is invariant and
  /// Planck's law rescales onto itself, the observed result is EXACTLY a black body
  /// at T_obs = g*T_em, with bolometric brightness proportional to T_obs^4. In other
  /// words, the g^4 beaming factor doesn't need separate handling: it falls out
  /// automatically from raising T_obs to the fourth power.
  /// </summary>
  private static void ShadeDisk(
    in RayResult res,
    ArrayView<float> lut,
    float profileMax,
    out float r,
    out float g,
    out float b)
  {
    float radius = res.DiskR;
    if (float.IsNaN(radius) || radius <= 0.0f)
    { 
      r = g = b = 0.0f; 
      return;
    }

    const float m = Configuration.Config.Rs / 2.0f;
    float gFactor = 1.0f;

    if (!Configuration.Config.Disk.Relativistic)
    {
      float omega = MathF.Sqrt(m / (radius * radius * radius));
      float denom = 1.0f - omega * res.BzGlobal;

      // No stable circular orbits inside the ISCO, and 1 - 3M/r changes sign
      // there: clamp instead of producing NaN, since the disk shouldn't reach
      // that radius anyway.
      float num = MathF.Sqrt(Math.Max(1.0f - 3.0f * m / radius, 1e-5f));
      gFactor = MathF.Abs(denom) > 1e-5f ? num / denom : 0.0f;
      if (gFactor < 0.0f) gFactor = 0.0f;
    }

    float profile = Profile(radius) / profileMax;

    if (Configuration.Config.Disk.TextureStrength > 0.0)
    {
      // Co-rotating pattern: material at radius r orbits at Omega(r), so the
      // phase is phi - Omega(r)*t. This produces Keplerian shear (inner material
      // outpaces outer material), which is what reads as a spinning disk in
      // video rather than a texture stuck to the surface. Approximation: light
      // travel time across the disk is ignored, which at these radii is on the
      // order of tens of M.
      float omega = MathF.Sqrt(m / (radius * radius * radius));
      float phase = res.DiskPhi - omega * Configuration.Config.Disk.InitialTime;
      float x = MathF.Log(radius) * Configuration.Config.Disk.TextureScaleRadial;
      float y = phase * Configuration.Config.Disk.TextureScaleAngular / (2.0f * MathF.PI);
      int periodY = GpuMath.Round(Configuration.Config.Disk.TextureScaleAngular);
      float n = Noise.Fbm(x, y, periodY, 4);

      float textureStrength = Configuration.Config.Disk.TextureStrength;
      profile *= 1.0f + textureStrength * (n * 2.0f - 1.0f);
      if (profile < 0.0f) profile = 0.0f;
    }

    // Soft fade at the edges: a hard-edged disk produces aliasing that
    // supersampling doesn't fully resolve.
    profile *= EdgeFade(radius);

    float tEmitted = Configuration.Config.Disk.PeakTemperature * profile;
    if (tEmitted <= 1.0) { r = g = b = 0.0f; return; }

    float tObserved = gFactor * tEmitted;
    if (tObserved <= 1.0f) { r = g = b = 0.0f; return; }

    // Brightness proportional to T_obs^4 (Stefan-Boltzmann), normalized to peak temperature.
    float ratio = tObserved / Configuration.Config.Disk.PeakTemperature;
    float luminance = ratio * ratio * ratio * ratio * Configuration.Config.Shading.Exposure;

    Spectrum.BlackBodyChroma(lut, tObserved, out float cr, out float cg, out float cb);
    r = cr * luminance;
    g = cg * luminance;
    b = cb * luminance;
  }

  /// <summary>
  /// Radial temperature profile for a thin disk (Shakura-Sunyaev):
  /// T ~ r^(-3/4) * (1 - sqrt(r_in/r))^(1/4). Vanishes at the inner edge because
  /// the viscous torque vanishes there.
  /// </summary>
  private static float Profile(float radius)
  {
    float inner = Configuration.Config.Disk.DiskInner;
    if (radius <= inner) return 0.0f;
    float u = MathF.Sqrt(inner / radius);
    return MathF.Pow(inner / radius, 0.75f) * MathF.Pow(MathF.Max(1.0f - u, 0.0f), 0.25f);
  }

  /// <summary>
  /// Analytic maximum of the profile: differentiating in u = sqrt(r_in/r) gives
  /// u = 6/7, i.e. r = (49/36)*r_in. If the disk is too narrow to contain that
  /// radius, the maximum falls on the outer edge instead.
  /// </summary>
  public static float PeakProfile()
  {
    float outer = Configuration.Config.Disk.DiskOuter;
    float rPeak = 49.0f / 36.0f * Configuration.Config.Disk.DiskInner;
    return rPeak <= outer ? Profile(rPeak) : MathF.Max(Profile(outer), 1e-5f);
  }

  private static float EdgeFade(float radius)
  {
    float inner = Configuration.Config.Disk.DiskInner;
    float outer = Configuration.Config.Disk.DiskOuter;
    float width = 0.04f * (outer - inner);
    if (width <= 0.0f) return 1.0f;
    float a = SmoothStep((radius - inner) / width);
    float b = SmoothStep((outer - radius) / width);
    return a * b;
  }

  private static float SmoothStep(float t)
  {
    if (t <= 0.0f) return 0.0f;
    if (t >= 1.0f) return 1.0f;
    return t * t * (3.0f - 2.0f * t);
  }
}