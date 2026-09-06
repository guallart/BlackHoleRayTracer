using BlackHoleRayTracer.Core;

using ILGPU;

namespace BlackHoleRayTracer.Rays;

/// <summary>
/// Converts the final RayState into the RayResult consumed by the shading stage.
/// </summary>
public static class RayResultBuilder
{
  public static void Build(Index1D i, ArrayView<RayState> rayStates, ArrayView<RayResult> results, int width)
  {
    RayState s = rayStates[i];

    RayResult result = new RayResult
    {
      PixelX = i % width,
      PixelY = i / width,
      Status = s.Status,
      FinalPr = s.Pr,
      BzGlobal = ComputeBz(s),
      StepsTaken = s.Steps,
      EscapeTheta = float.NaN,
      EscapePhi = float.NaN,
      DiskR = float.NaN,
      DiskPhi = float.NaN
    };

    switch (s.Status)
    {
      case RayStatus.Escaped:
      case RayStatus.MaxStepsReached:
        {
          // Sampling the skybox needs the asymptotic PROPAGATION direction, not the position direction. 
          // At finite r these differ; propagation direction converges much sooner:
          // residual error is O(rs/r), ~1e-3 rad with REscape = 1000*rs.
          // MaxStepsReached is filled the same way, as the best available estimate,
          // but stays flagged as suspect via Status.
          Vec3 v = s.Velocity();
          float len = v.Length;
          if (len > 0.0)
          {
            Vec3 d = v * (1.0f / len);
            result.EscapeTheta = MathF.Acos(GpuMath.Clamp(d.Z, -1.0f, 1.0f));
            result.EscapePhi = NormalizeAngle(MathF.Atan2(d.Y, d.X));
          }
          break;
        }

      case RayStatus.HitDisk:
        {
          // Impact point coordinates in the global equatorial plane, where the disk lives:
          // this is what the shading stage needs for the local orbital velocity (Doppler),
          // gravitational redshift, and brightness profile.
          Vec3 pos = s.Position();
          result.DiskR = s.R;
          result.DiskPhi = NormalizeAngle(MathF.Atan2(pos.Y, pos.X));
          break;
        }

      case RayStatus.Absorbed:
      default:
        break;
    }

    results[i] = result;
  }

  /// <summary>
  /// b_z = L_z/E of the physical photon.
  ///
  /// The TRACED ray's angular momentum is L*n̂ with n̂ = e1 × e2 (orbital plane normal),
  /// since r × v = R ê_r × (L/R) ê_phi = L (e1 × e2). The real photon travels opposite to
  /// the traced ray (k = -p), so its L_z carries the opposite sign. The ratio L_z/E is
  /// independent of the affine parameter's normalization.
  /// </summary>
  private static float ComputeBz(in RayState s)
  {
    if (s.E == 0.0f) return 0.0f;
    Vec3 normal = Vec3.Cross(s.PlaneE1, s.PlaneE2);
    return -(s.L * normal.Z) / s.E;
  }

  /// <summary>Wraps an angle into the range [0, 2π).</summary>
  private static float NormalizeAngle(float angle)
  {
    const float TwoPi = (float)(2.0 * Math.PI);
    angle %= TwoPi;
    return angle < 0.0f ? angle + TwoPi : angle;
  }
}