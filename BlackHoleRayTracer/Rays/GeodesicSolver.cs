using BlackHoleRayTracer.Configuration;

using ILGPU;

using System.Runtime.CompilerServices;

namespace BlackHoleRayTracer.Rays;

/// <summary>
/// Integrated system, equatorial null geodesic in the ray's own orbital plane:
///     dr/dlambda   = p_r
///     dphi/dlambda = L / r^2
///     dp_r/dlambda = L^2 (r - 1.5*rs) / r^4 = (L/r^2)^2 (r - 1.5*rs)
/// Single precision throughout: all state is float, all literals carry 'f',
/// and all math goes through MathF to avoid silent promotion to double.
/// </summary>
public static class GeodesicSolver
{
  /// <summary>
  /// Tolerance for deciding whether the orbital plane coincides with the disk plane.
  /// Must stay well above float epsilon (~1.2e-7): the basis components carry
  /// rounding noise of that order even for an exactly coplanar ray.
  /// </summary>
  private const float CoplanarTolerance = 1e-6f;

  /// <summary>Lower bound on r inside the RHS. Expressed as a fraction of rs so that
  /// r^2 and L/r^2 stay in range regardless of the unit system.</summary>
  private const float MinRadiusFraction = 1e-3f;

  public static void Integrate(Index1D i, ArrayView<RayState> rayStates)
  {
    RayState s = rayStates[i];

    if (s.Status != RayStatus.Active)
      return;

    float rEscape = Config.Solver.REscape;
    float rHorizon = Config.Rs * (1.0f + Config.Solver.HorizonEpsilon);
    bool diskEnabled = Config.Disk.DiskEnabled;
    int maxSteps = Config.Solver.MaxSteps;
    float diskInner = Config.Disk.DiskInner;
    float diskOuter = Config.Disk.DiskOuter;

    if (s.R <= rHorizon)
    { 
      s.Status = RayStatus.Absorbed;
      rayStates[i] = s;
      return;
    }

    if (s.R >= rEscape) {
      s.Status = RayStatus.Escaped;
      rayStates[i] = s;
      return;
    }

    float e1z = s.PlaneE1.Z, e2z = s.PlaneE2.Z;
    bool coplanar = diskEnabled
                    && MathF.Abs(e1z) < CoplanarTolerance
                    && MathF.Abs(e2z) < CoplanarTolerance;

    float zPrev = SignedHeight(s);

    while (s.Steps < maxSteps)
    {
      RayState prev = s;

      float h = AdaptStep(s);
      s = RK4Step(s, h);
      s.Lambda = prev.Lambda + h;
      s.Steps = prev.Steps + 1;

      if (!float.IsFinite(s.R) || !float.IsFinite(s.Phi) || !float.IsFinite(s.Pr))
      {
        prev.Status = RayStatus.MaxStepsReached;
        rayStates[i] = prev;
        return;
      }

      if (diskEnabled)
      {
        if (coplanar)
        {
          if (s.R >= diskInner && s.R <= diskOuter)
          {
            s.Status = RayStatus.HitDisk;
            rayStates[i] = s;
            return;
          }
        }
        else
        {
          float zCur = SignedHeight(s);
          bool crossed = (zPrev > 0.0f && zCur <= 0.0f) || (zPrev < 0.0f && zCur >= 0.0f);

          if (crossed)
          {
            // Signs differ, so |zPrev - zCur| >= |zPrev|: no cancellation here.
            float t = zPrev / (zPrev - zCur);
            float rHit = prev.R + t * (s.R - prev.R);

            if (rHit >= diskInner && rHit <= diskOuter)
            {
              s.R = rHit;
              s.Phi = prev.Phi + t * (s.Phi - prev.Phi);
              s.Pr = prev.Pr + t * (s.Pr - prev.Pr);
              s.Lambda = prev.Lambda + t * (s.Lambda - prev.Lambda);
              s.Status = RayStatus.HitDisk;
              rayStates[i] = s;
              return;
            }
          }

          zPrev = zCur;
        }
      }

      RayStatus status = CheckTermination(s);
      if (status != RayStatus.Active)
      {
        s.Status = status;
        rayStates[i] = s;
        return;
      }
    }

    s.Status = RayStatus.MaxStepsReached;
    rayStates[i] = s;
  }

  private static RayState RK4Step(RayState s, float h)
  {
    float rs = Config.Rs;
    float L = s.L;
    float r = s.R;
    float phi = s.Phi;
    float pr = s.Pr;

    Rhs(                 r,                  pr, L, rs, out float k1r, out float k1f, out float k1p);
    Rhs(r + 0.5f * h * k1r, pr + 0.5f * h * k1p, L, rs, out float k2r, out float k2f, out float k2p);
    Rhs(r + 0.5f * h * k2r, pr + 0.5f * h * k2p, L, rs, out float k3r, out float k3f, out float k3p);
    Rhs(       r + h * k3r,        pr + h * k3p, L, rs, out float k4r, out float k4f, out float k4p);

    float sixth = h / 6.0f;
    s.R = r + sixth * (k1r + 2.0f * k2r + 2.0f * k3r + k4r);
    s.Phi = phi + sixth * (k1f + 2.0f * k2f + 2.0f * k3f + k4f);
    s.Pr = pr + sixth * (k1p + 2.0f * k2p + 2.0f * k3p + k4p);
    return s;
  }

  /// <summary>
  /// Right-hand side of the ODE system.
  /// dpr is written as (L/r^2)^2 * (r - 1.5*rs) rather than L^2*(r-1.5*rs)/r^4:
  /// algebraically identical, but it never forms r^4, which would overflow float
  /// at large r and flush to zero at small r.
  /// </summary>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private static void Rhs(float r, float pr, float L, float rs, out float dr, out float dphi, out float dpr)
  {
    float rMin = MinRadiusFraction * rs;
    if (r < rMin) r = rMin;

    float u = L / (r * r);

    dr = pr;
    dphi = u;
    dpr = u * u * (r - 1.5f * rs);
  }

  private static RayStatus CheckTermination(RayState s)
  {
    float rs = Config.Rs;
    float rEscape = Config.Solver.REscape;
    float horizonEps = Config.Solver.HorizonEpsilon;

    if (s.R <= rs * (1.0f + horizonEps)) return RayStatus.Absorbed;
    if (s.R >= rEscape) return RayStatus.Escaped;
    return RayStatus.Active;
  }

  public static float AdaptStep(RayState s)
  {
    if (!Config.Solver.UseAdaptiveStep)
      return Config.Solver.FixedLambdaStep;

    float r = s.R;
    float rs = Config.Rs;
    float safety = Config.Solver.AdaptiveSafety;
    float maxAnglePerStep = Config.Solver.MaxAnglePerStep;
    float minLambdaStep = Config.Solver.MinLambdaStep;
    float maxLambdaStep = Config.Solver.MaxLambdaStep;

    float distHorizon = MathF.Max(r - rs, 1e-4f * rs);
    float lengthScale = MathF.Min(r, distHorizon);

    float tangentialRate = s.L / r;
    float rate = MathF.Sqrt(s.Pr * s.Pr + tangentialRate * tangentialRate);

    float h = safety * lengthScale / (rate + 1e-30f);

    float dphi = MathF.Abs(s.L) / (r * r);
    if (dphi > 0.0f)
    {
      float hAngular = maxAnglePerStep / dphi;
      if (hAngular < h) h = hAngular;
    }

    float distPhotonSphere = MathF.Abs(r - 1.5f * rs);
    float band = 0.5f * rs;
    if (distPhotonSphere < band)
      h *= 0.25f + 0.75f * (distPhotonSphere / band);

    if (h < minLambdaStep) h = minLambdaStep;
    if (h > maxLambdaStep) h = maxLambdaStep;
    return h;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private static float SignedHeight(in RayState s)
    => s.R * (MathF.Cos(s.Phi) * s.PlaneE1.Z + MathF.Sin(s.Phi) * s.PlaneE2.Z);
}